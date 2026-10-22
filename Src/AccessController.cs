using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>
    /// Single update loop of the mod: picks the right mode (menu, board, list) each frame and routes the keys.
    /// </summary>
    public class AccessController : MonoBehaviour
    {
        public static AccessController Instance;

        // Settings (synchronised from the mod config).
        public static bool AnnounceNewCards = true;
        public static bool AnnounceCombat = true;
        public static bool AnnounceHits = true;
        public static bool AnnounceActions = true;
        public static int MoonWarningSeconds = 20;
        public static bool CameraFollowEnabled = true;
        public static bool AutoStackEnabled = true;
        public static bool SortByName = true;
        public static bool BeepIdleVillager = true;
        public static bool AnnounceOtherPlayers = true;
        public static float BeepVolume = 0.8f;

        private readonly MenuNavigator menu = new MenuNavigator();
        private readonly BoardNavigator board = new BoardNavigator();
        private readonly ListBrowser list = new ListBrowser();
        private readonly EventNarrator events = new EventNarrator();

        private bool wasBoardMode;
        private int escapeCapturedFrame = -1;
        private bool greeted;
        private bool errorSpoken;

        private bool StateCapturesEscape => TextPrompt.Active || Bindings.Capturing || list.IsOpen || board.IsHolding || board.IsMoving || menu.IsEditing;

        public static bool VisualMenusEnabled = true;

        /// <summary>True when Escape must not reach the game this frame (see Patches).</summary>
        public static bool CapturesEscape
        {
            get
            {
                var i = Instance;
                if (i == null) return false;
                return i.StateCapturesEscape || i.escapeCapturedFrame == Time.frameCount;
            }
        }

        private void Awake()
        {
            Instance = this;
            board.List = list;
        }

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (System.Exception e)
            {
                Log.Once(e);
                if (!errorSpoken)
                {
                    errorSpoken = true;
                    Speech.Say(Loc.T("mod_error"), interrupt: false);
                }
            }
        }

        private void Tick()
        {
            if (Keyboard.current == null) return;
            if (StateCapturesEscape && Keys.Down(Key.Escape)) escapeCapturedFrame = Time.frameCount;

            Keys.DisableConflictingBindings();
            var es = EventSystem.current;
            if (es != null && es.sendNavigationEvents) es.sendNavigationEvents = false;

            var gc = GameCanvas.instance;
            if (gc == null) return;

            if (!greeted && gc.CurrentScreen != null)
            {
                greeted = true;
                Speech.Say(Loc.T("mod_ready"), false);
                Log.CheckCompatibility();
            }

            events.Update();
            Together.Update(list);
            FirstLaunch.Update();
            VisualMenu.Update(list);

            // Waiting for a new key in the options: every key goes there.
            if (Bindings.Capturing) { Bindings.UpdateCapture(); return; }

            // A text field of the mod (multiplayer panel) takes every key.
            if (TextPrompt.Active) { TextPrompt.Update(); return; }

            if (TransitionScreen.InTransition) return;

            // Global keys
            if (Bindings.Pressed("toggle_narration"))
            {
                StacklandAccess.SetNarration(!Speech.Enabled);
                return;
            }
            if (Bindings.Pressed("repeat")) { Speech.Say(Speech.LastSpoken); return; }
            if (Bindings.Pressed("toggle_autostack"))
            {
                AutoStackEnabled = !AutoStackEnabled;
                StacklandAccess.SaveSetting("auto_stack", AutoStackEnabled);
                Speech.Say(Loc.T(AutoStackEnabled ? "autostack_on" : "autostack_off"));
                return;
            }
            if (Bindings.Pressed("toggle_new_cards"))
            {
                AnnounceNewCards = !AnnounceNewCards;
                StacklandAccess.SaveSetting("announce_new_cards", AnnounceNewCards);
                Speech.Say(Loc.T(AnnounceNewCards ? "new_cards_on" : "new_cards_off"));
                return;
            }

            var wm = WorldManager.instance;
            bool boardMode = !gc.ModalIsOpen && wm != null && wm.CurrentBoard != null &&
                             (gc.CurrentScreen is GameScreen || (gc.CurrentScreen is CutsceneScreen && wm.RemovingCards));

            if (!menu.IsEditing)
            {
                // Public website, all languages.
                if (Bindings.Pressed("export_site")) { CardExport.Run(site: true); return; }
                if (Bindings.Pressed("export")) { CardExport.Run(site: false); return; }
                if (Bindings.Pressed("help"))
                {
                    list.Open(Loc.T("list_help"), ListBrowser.Help(), "help");
                    return;
                }
                if (Bindings.Pressed("options") && !list.IsOpen)
                {
                    OptionsMenu.Open(list);
                    return;
                }
            }

            if (list.IsOpen)
            {
                list.HandleInput();
                return;
            }

            if (boardMode)
            {
                if (!wasBoardMode)
                {
                    menu.Deactivate();
                    MenuNavigator.FocusedButton = null;
                    if (gc.CurrentScreen is GameScreen)
                    {
                        Speech.Say(Loc.T("board_name", EventNarrator.BoardName(wm.CurrentBoard.Id)));
                        board.AnnounceCurrent();
                    }
                }
                wasBoardMode = true;
                if (HandleBoardShortcuts(wm)) return;
                board.HandleInput();
                AutoPauseWhileHolding(wm);
                AutoStacker.Update(board);
            }
            else
            {
                wasBoardMode = false;
                menu.Update();
            }
        }

        /// <summary>Speeds offered by - and +: the game's own (pause, 1, 5) plus slower and intermediate ones.</summary>
        private static readonly float[] Speeds = { 0f, 0.5f, 1f, 2f, 5f };

        private void SetSpeed(WorldManager wm, int index)
        {
            if (Together.IsClient) { Speech.Say(Loc.T("mp_speed_host")); return; }
            if (GameCanvas.instance.ModalIsOpen || !(GameCanvas.instance.CurrentScreen is GameScreen)) { Speech.Say(Loc.T("not_now")); return; }
            pausedByHold = false;
            float before = wm.SpeedUp;
            wm.SpeedUp = Speeds[Mathf.Clamp(index, 0, Speeds.Length - 1)];
            if (Mathf.Approximately(before, wm.SpeedUp)) Speech.Say(TimeText(wm));
        }

        private void ChangeSpeed(WorldManager wm, int dir)
        {
            if (Together.IsClient) { Speech.Say(Loc.T("mp_speed_host")); return; }
            if (GameCanvas.instance.ModalIsOpen || !(GameCanvas.instance.CurrentScreen is GameScreen)) { Speech.Say(Loc.T("not_now")); return; }
            float cur = wm.SpeedUp;
            int idx = 0;
            for (int i = 0; i < Speeds.Length; i++) if (Mathf.Abs(Speeds[i] - cur) < Mathf.Abs(Speeds[idx] - cur)) idx = i;
            int ni = Mathf.Clamp(idx + dir, 0, Speeds.Length - 1);
            pausedByHold = false;
            wm.SpeedUp = Speeds[ni];
            // The EventNarrator announces the new speed.
            if (ni == idx) Speech.Say(TimeText(wm));
        }

        private bool pausedByHold;
        private float speedBeforeHold = 1f;

        /// <summary>Follows the game's own "auto pause when dragging with keyboard and mouse" setting.</summary>
        private void AutoPauseWhileHolding(WorldManager wm)
        {
            // A multiplayer client cannot pause: the host runs the game.
            bool want = AccessibilityScreen.AutoPauseWhenUsingKeyboardMouse && board.IsHolding && !Together.IsClient;
            if (want && !pausedByHold)
            {
                pausedByHold = true;
                speedBeforeHold = wm.SpeedUp;
                wm.SpeedUp = 0f;
            }
            else if (!want && pausedByHold)
            {
                pausedByHold = false;
                if (wm.SpeedUp == 0f) wm.SpeedUp = speedBeforeHold;
            }
        }

        private bool HandleBoardShortcuts(WorldManager wm)
        {
            if (Bindings.Pressed("villagers"))
            {
                list.Open(Loc.T("list_villagers"), ListBrowser.Villagers(board), "villagers");
                return true;
            }
            bool restackCategory = Bindings.Pressed("restack_category");
            if (restackCategory || Bindings.Pressed("restack"))
            {
                if (!wm.CanInteract) { Speech.Say(Loc.T("not_now")); return true; }
                // Only the category being browsed.
                BoardNavigator.Category? only = restackCategory ? board.CurrentCategory : (BoardNavigator.Category?)null;
                int moved = AutoStacker.RestackAll(board, only);
                string where = only.HasValue && only.Value != BoardNavigator.Category.All ? BoardNavigator.CategoryName(only.Value) : "";
                Speech.Say(TextCleaner.Join(where, moved > 0 ? Loc.N("restacked", moved, moved) : Loc.T("restacked_none")));
                return true;
            }
            // Numeric keypad: 0 pause, 1 to 4 the speeds (0.5, 1, 2, 5).
            if (Bindings.Pressed("speed_pause")) { SetSpeed(wm, 0); return true; }
            if (Bindings.Pressed("speed_1")) { SetSpeed(wm, 1); return true; }
            if (Bindings.Pressed("speed_2")) { SetSpeed(wm, 2); return true; }
            if (Bindings.Pressed("speed_3")) { SetSpeed(wm, 3); return true; }
            if (Bindings.Pressed("speed_4")) { SetSpeed(wm, 4); return true; }
            if (Bindings.Pressed("speed_up")) { ChangeSpeed(wm, +1); return true; }
            if (Bindings.Pressed("speed_down")) { ChangeSpeed(wm, -1); return true; }
            if (Bindings.Pressed("resources")) { Speech.Say(ResourcesText(wm)); return true; }
            if (Bindings.Pressed("time")) { Speech.Say(TimeText(wm)); return true; }
            if (Bindings.Pressed("quests"))
            {
                var entries = ListBrowser.Quests(out int start);
                list.Open(Loc.T("list_quests"), entries, "quests", start);
                return true;
            }
            if (Bindings.Pressed("recipes")) { list.OpenPages(Recipes.Pages(board), "recipes"); return true; }
            if (Bindings.Pressed("history")) { list.Open(Loc.T("list_history"), ListBrowser.History(), "history"); return true; }
            return false;
        }

        // ------------------------------------------------------------------ status texts

        private static string Of(string a, string b) => Loc.T("of", a, b);

        public static string ResourcesText(WorldManager wm)
        {
            var gs = GameScreen.instance;
            var parts = new List<string>();
            if (gs != null)
            {
                if (gs.ShowInfoBoxMoney != null && gs.ShowInfoBoxMoney.gameObject.activeInHierarchy) parts.Add(TextCleaner.Clean(gs.MoneyText.text));
                if (gs.ShowInfoBoxDollar != null && gs.ShowInfoBoxDollar.gameObject.activeInHierarchy) parts.Add(TextCleaner.Clean(gs.DollarText.text));
            }
            try
            {
                if (!wm.ForestMoonEnabled)
                {
                    parts.Add(Loc.T("icon_food") + " " + Of(wm.GetFoodCount().ToString(), wm.GetRequiredFoodCount().ToString()));
                    parts.Add(Loc.T("icon_card") + " " + Of(wm.GetCardCount().ToString(), wm.GetMaxCardCount().ToString()));
                }
            }
            catch (System.Exception ex) { Log.Once(ex); }
            if (gs != null)
            {
                if (gs.HappinessText != null && gs.HappinessText.isActiveAndEnabled) parts.Add(Slash(gs.HappinessText.text));
                if (gs.WellbeingText != null && gs.WellbeingText.isActiveAndEnabled) parts.Add(Slash(gs.WellbeingText.text));
                if (gs.WorkerText != null && gs.WorkerText.isActiveAndEnabled) parts.Add(Slash(gs.WorkerText.text));
            }
            return TextCleaner.Join(parts.ToArray());
        }

        private static string Slash(string s)
        {
            s = TextCleaner.Clean(s);
            int i = s.IndexOf('/');
            if (i <= 0) return s;
            return Loc.T("of", s.Substring(0, i).Trim(), s.Substring(i + 1).Trim());
        }

        public static string TimeText(WorldManager wm)
        {
            string moon;
            if (wm.CurrentRunOptions != null && wm.CurrentRunOptions.IsPeacefulMode) moon = Loc.T("moon_peaceful", wm.CurrentMonth);
            else if (wm.ForestMoonEnabled) moon = Loc.T("moon_forest", wm.CurrentRunVariables != null ? wm.CurrentRunVariables.ForestWave : 0);
            else moon = Loc.T("moon", wm.CurrentMonth, CardInfo.Seconds(Mathf.Max(0f, wm.MonthTime - wm.MonthTimer)));
            string speed = wm.SpeedUp <= 0f ? Loc.T("speed_pause") : Loc.T("speed", "x" + wm.SpeedUp.ToString("0.#"));
            return TextCleaner.Join(moon, speed);
        }
    }
}
