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

        private bool StateCapturesEscape => TextPrompt.Active || list.IsOpen || board.IsHolding || board.IsMoving || menu.IsEditing;

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

            // A text field of the mod (multiplayer panel) takes every key.
            if (TextPrompt.Active) { TextPrompt.Update(); return; }

            if (TransitionScreen.InTransition) return;

            // Global keys
            if (Keys.Down(Key.F2)) { Speech.Say(Speech.LastSpoken); return; }
            if (Keys.Down(Key.F4))
            {
                AutoStackEnabled = !AutoStackEnabled;
                StacklandAccess.SaveSetting("auto_stack", AutoStackEnabled);
                Speech.Say(Loc.T(AutoStackEnabled ? "autostack_on" : "autostack_off"));
                return;
            }
            if (Keys.Down(Key.F3))
            {
                AnnounceNewCards = !AnnounceNewCards;
                StacklandAccess.SaveSetting("announce_new_cards", AnnounceNewCards);
                Speech.Say(Loc.T(AnnounceNewCards ? "new_cards_on" : "new_cards_off"));
                return;
            }

            var wm = WorldManager.instance;
            bool boardMode = !gc.ModalIsOpen && wm != null && wm.CurrentBoard != null &&
                             (gc.CurrentScreen is GameScreen || (gc.CurrentScreen is CutsceneScreen && wm.RemovingCards));

            if (Keys.Down(Key.F6) && !menu.IsEditing)
            {
                // Shift+F6: public website, all languages.
                CardExport.Run(site: Keys.Shift);
                return;
            }
            if (Keys.Down(Key.F1) && !menu.IsEditing)
            {
                list.Open(Loc.T("list_help"), ListBrowser.Help(), Key.F1);
                return;
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
            if (Keys.Ctrl && Keys.Letter('p'))
            {
                list.Open(Loc.T("list_villagers"), ListBrowser.Villagers(board), 'p');
                return true;
            }
            if (Keys.Down(Key.F5))
            {
                if (!wm.CanInteract) { Speech.Say(Loc.T("not_now")); return true; }
                // Ctrl+F5: only the category being browsed.
                BoardNavigator.Category? only = Keys.Ctrl ? board.CurrentCategory : (BoardNavigator.Category?)null;
                int moved = AutoStacker.RestackAll(board, only);
                string where = only.HasValue && only.Value != BoardNavigator.Category.All ? BoardNavigator.CategoryName(only.Value) : "";
                Speech.Say(TextCleaner.Join(where, moved > 0 ? Loc.N("restacked", moved, moved) : Loc.T("restacked_none")));
                return true;
            }
            // Numeric keypad: 0 pause, 1 to 4 the speeds (0.5, 1, 2, 5).
            if (Keys.Down(Key.Numpad0)) { SetSpeed(wm, 0); return true; }
            if (Keys.Down(Key.Numpad1)) { SetSpeed(wm, 1); return true; }
            if (Keys.Down(Key.Numpad2)) { SetSpeed(wm, 2); return true; }
            if (Keys.Down(Key.Numpad3)) { SetSpeed(wm, 3); return true; }
            if (Keys.Down(Key.Numpad4)) { SetSpeed(wm, 4); return true; }
            if (Keys.SpeedUp) { ChangeSpeed(wm, +1); return true; }
            if (Keys.SpeedDown) { ChangeSpeed(wm, -1); return true; }
            if (Keys.Letter('r')) { Speech.Say(ResourcesText(wm)); return true; }
            if (Keys.Letter('t')) { Speech.Say(TimeText(wm)); return true; }
            if (Keys.Letter('q'))
            {
                var entries = ListBrowser.Quests(out int start);
                list.Open(Loc.T("list_quests"), entries, 'q', start);
                return true;
            }
            if (Keys.Letter('l')) { list.OpenPages(Recipes.Pages(board), 'l'); return true; }
            if (Keys.Letter('n')) { list.Open(Loc.T("list_history"), ListBrowser.History(), 'n'); return true; }
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
