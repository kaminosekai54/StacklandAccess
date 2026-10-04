using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StacklandAccessNS
{
    /// <summary>
    /// Screen reader navigation of every UI screen and dialog: our own focus over the screen's Selectables,
    /// in hierarchy order (which is the visual order of the game's layout groups).
    /// </summary>
    public class MenuNavigator
    {
        /// <summary>Button the mod is focusing, shown as hovered by a Harmony patch.</summary>
        public static CustomButton FocusedButton;

        private Transform root;
        private readonly List<Selectable> items = new List<Selectable>();
        private Selectable focused;
        private float refreshTimer;
        private TMP_InputField editing;
        private string editText = "";
        private int editStartFrame;
        private string pendingRelabel;
        private Selectable pendingRelabelItem;
        private float pendingRelabelTime;

        public bool IsEditing => editing != null;
        private static bool firstAnnounce = true;
        private string lastModalSig = "";
        private string lastWaitingText = "";
        private string lastItemsSig = "";

        private static string ModalSignature()
        {
            var gc = GameCanvas.instance;
            if (gc == null || !gc.ModalIsOpen || ModalScreen.instance == null) return "";
            var m = ModalScreen.instance;
            return (m.TitleText != null ? m.TitleText.text : "") + "|" + (m.TextText != null ? m.TextText.text : "");
        }

        private string ItemsSignature() => string.Join("|", items.Select(i => i.GetInstanceID().ToString()));

        public void Deactivate()
        {
            root = null;
            focused = null;
            FocusedButton = null;
            items.Clear();
            editing = null;
        }

        private static Transform CurrentRoot()
        {
            var gc = GameCanvas.instance;
            if (gc == null) return null;
            if (gc.ModalIsOpen && ModalScreen.instance != null) return ModalScreen.instance.transform;
            // Pop-ups that live inside a screen (update notes of the main menu...).
            if (gc.CurrentScreen is MainMenu mm && mm.UpdatePopup != null && mm.UpdatePopup.gameObject.activeInHierarchy)
                return mm.UpdatePopup.transform;
            return gc.CurrentScreen != null ? gc.CurrentScreen.transform : null;
        }

        public void Update()
        {
            var newRoot = CurrentRoot();
            if (newRoot == null) { Deactivate(); return; }
            string modalSig = ModalSignature();
            if (newRoot != root || modalSig != lastModalSig)
            {
                root = newRoot;
                lastModalSig = modalSig;
                editing = null;
                Rebuild();
                focused = PickInitialFocus();
                lastItemsSig = ItemsSignature();
                AnnounceRoot();
                return;
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f;
                Rebuild();
                if (focused == null || !items.Contains(focused))
                {
                    focused = PickInitialFocus();
                }
                // New choices appeared (cutscene with several answers, screen content changed...): read them.
                string sig = ItemsSignature();
                if (sig != lastItemsSig)
                {
                    bool cutscene = GameCanvas.instance.CurrentScreen is CutsceneScreen && !GameCanvas.instance.ModalIsOpen;
                    if (cutscene && items.Count > 1)
                        Speech.Say(Loc.T("choices", string.Join(", ", items.Select(i => Label(i)))), interrupt: false);
                    lastItemsSig = sig;
                }
            }
            FocusedButton = focused as CustomButton;

            if (pendingRelabelItem != null && Time.unscaledTime >= pendingRelabelTime)
            {
                if (pendingRelabelItem == focused && pendingRelabelItem != null && pendingRelabelItem.gameObject.activeInHierarchy)
                {
                    string now = Describe(focused, withPosition: false);
                    if (now != pendingRelabel) Speech.Say(now);
                }
                pendingRelabelItem = null;
            }

            if (editing != null)
            {
                HandleEditing();
                return;
            }
            // Controls screen: while the game waits for a new key, leave every key to the game.
            if (GameCanvas.instance.CurrentScreen is ControlsScreen cs && cs.IsRebinding)
            {
                string w = cs.WaitingForInputText != null ? TextCleaner.Clean(cs.WaitingForInputText.text) : "";
                if (w != lastWaitingText) { lastWaitingText = w; Speech.Say(w); }
                return;
            }
            lastWaitingText = "";
            HandleKeys();
        }

        // ------------------------------------------------------------------ items

        private void Rebuild()
        {
            items.Clear();
            if (root == null) return;
            var all = root.GetComponentsInChildren<Selectable>(false);
            foreach (var s in all)
            {
                if (s == null || !s.isActiveAndEnabled) continue;
                if (s is Scrollbar) continue;
                if (s.GetComponentInParent<TMP_Dropdown>() is TMP_Dropdown dd && dd != s) continue;
                // A slider inside a CustomButton is driven through the button.
                var parentSel = s.transform.parent != null ? s.transform.parent.GetComponentInParent<Selectable>() : null;
                if (parentSel != null && parentSel != s && parentSel is CustomButton) continue;
                if (!IsOnScreen(s)) continue;
                items.Add(s);
            }
            headings.Clear();
            string last = "";
            Walk(root, ref last);
        }

        /// <summary>Text that precedes a control on screen (section title such as "Moon length"), by hierarchy order.</summary>
        private readonly Dictionary<Selectable, string> headings = new Dictionary<Selectable, string>();
        private string lastHeadingSpoken = "";

        private void Walk(Transform t, ref string last)
        {
            if (!t.gameObject.activeInHierarchy) return;
            var sel = t.GetComponent<Selectable>();
            if (sel != null && items.Contains(sel))
            {
                headings[sel] = last;
                return;
            }
            if (sel == null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    string txt = TextCleaner.Clean(tmp.text);
                    if (IsHeadingText(txt)) last = txt;
                }
            }
            foreach (Transform child in t) Walk(child, ref last);
        }

        private static bool IsHeadingText(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt) || txt.Length > 120) return false;
            return !IsNumeric(txt);
        }

        /// <summary>"50%", "12", "3/8"... values, not labels.</summary>
        private static bool IsNumeric(string txt)
        {
            foreach (char c in txt)
            {
                if (char.IsLetter(c)) return false;
            }
            return true;
        }

        private string Heading(Selectable s) => s != null && headings.TryGetValue(s, out var h) ? h : "";

        /// <summary>Description used when the focus moves: adds the section title when it changes.</summary>
        private string DescribeFocus(Selectable s)
        {
            string desc = Describe(s, withPosition: true);
            string h = Heading(s);
            if (h != "" && h != lastHeadingSpoken && !desc.Contains(h))
            {
                lastHeadingSpoken = h;
                return TextCleaner.Join(h, desc);
            }
            lastHeadingSpoken = h;
            return desc;
        }

        private static bool IsOnScreen(Selectable s)
        {
            var cg = s.GetComponentInParent<CanvasGroup>();
            if (cg != null && cg.alpha <= 0.01f) return false;
            return true;
        }

        private Selectable PickInitialFocus()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null)
            {
                var sel = es.currentSelectedGameObject.GetComponent<Selectable>();
                if (sel != null && items.Contains(sel)) return sel;
            }
            return items.FirstOrDefault(IsEnabled) ?? items.FirstOrDefault();
        }

        private static bool IsEnabled(Selectable s)
        {
            if (s is CustomButton cb) return cb.ButtonEnabled && cb.interactable;
            return s.interactable;
        }

        // ------------------------------------------------------------------ announce

        private void AnnounceRoot()
        {
            var gc = GameCanvas.instance;
            string title = "";
            string body = "";
            var popup = root != null ? root.GetComponent<UpdatePopup>() : null;
            if (popup != null && !gc.ModalIsOpen)
            {
                title = popup.UpdateTitle != null ? popup.UpdateTitle.text : "";
                body = popup.UpdateText != null ? popup.UpdateText.text : "";
            }
            else if (gc.ModalIsOpen && ModalScreen.instance != null)
            {
                title = ModalScreen.instance.TitleText != null ? ModalScreen.instance.TitleText.text : Loc.T("modal");
                body = ModalScreen.instance.TextText != null ? ModalScreen.instance.TextText.text : "";
            }
            else if (gc.CurrentScreen is CutsceneScreen)
            {
                // The EventNarrator speaks the cutscene title, text and continue button: do not cut it.
                if (items.Count > 1) Speech.Say(Loc.T("choices", string.Join(", ", items.Select(i => Label(i)))), interrupt: false);
                firstAnnounce = false;
                return;
            }
            else if (gc.CurrentScreen != null)
            {
                string key = "scr_" + gc.CurrentScreen.GetType().Name;
                title = Loc.T(key);
                if (title == key) title = FirstText(root);
            }
            lastHeadingSpoken = "";
            string first = focused != null ? DescribeFocus(focused) : "";
            Speech.Say(TextCleaner.Join(title, body, first), interrupt: !firstAnnounce);
            firstAnnounce = false;
        }

        private static string FirstText(Transform t)
        {
            foreach (var tmp in t.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (tmp.GetComponentInParent<Selectable>() != null) continue;
                var s = TextCleaner.Clean(tmp.text);
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return "";
        }

        /// <summary>All texts of the screen that are not part of a control (R key).</summary>
        private string ScreenText()
        {
            var sb = new List<string>();
            if (root == null) return "";
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (tmp.GetComponentInParent<Selectable>() != null) continue;
                var s = TextCleaner.Clean(tmp.text);
                if (!string.IsNullOrWhiteSpace(s) && !sb.Contains(s)) sb.Add(s);
            }
            return TextCleaner.Join(sb.ToArray());
        }

        public string Describe(Selectable s, bool withPosition)
        {
            if (s == null) return "";
            var parts = new List<string>();
            string label = Label(s);
            if (label == "" || IsNumeric(label)) label = TextCleaner.Join(SpecialLabel(s) ?? Heading(s), label);
            parts.Add(label);
            string state = SpecialState(s);
            if (state != null) parts.Add(state);
            switch (s)
            {
                case Toggle tg:
                    parts.Add(tg.isOn ? Loc.T("checked") : Loc.T("unchecked"));
                    break;
                case Slider sl:
                    parts.Add(Loc.T("slider") + " " + SliderValue(sl));
                    break;
                case TMP_InputField inf:
                    parts.Add(Loc.T("edit"));
                    parts.Add(string.IsNullOrEmpty(inf.text) ? Loc.T("empty") : inf.text);
                    break;
                case TMP_Dropdown dd:
                    parts.Add(Loc.T("dropdown"));
                    break;
                case CustomButton cb:
                    var childSlider = cb.GetComponentInChildren<Slider>();
                    if (childSlider != null) parts.Add(Loc.T("slider") + " " + SliderValue(childSlider));
                    break;
            }
            if (!IsEnabled(s)) parts.Add(Loc.T("unavailable"));
            if (withPosition && items.Count > 1)
            {
                int i = items.IndexOf(s);
                if (i >= 0) parts.Add(Loc.T("of", i + 1, items.Count));
            }
            return TextCleaner.Join(parts.ToArray());
        }

        /// <summary>Names for controls that have no text label in the game.</summary>
        private static string SpecialLabel(Selectable s)
        {
            var opt = GameCanvas.instance != null ? GameCanvas.instance.CurrentScreen as OptionsScreen : null;
            if (opt != null)
            {
                if (s == opt.MusicSlider || (s is CustomButton && s.GetComponentInChildren<Slider>() == opt.MusicSlider)) return Loc.T("vol_music");
                if (s == opt.SfxSlider || (s is CustomButton && s.GetComponentInChildren<Slider>() == opt.SfxSlider)) return Loc.T("vol_sfx");
            }
            string n = s.gameObject.name.ToLowerInvariant();
            if (n.Contains("music")) return Loc.T("vol_music");
            if (n.Contains("sfx") || n.Contains("sound")) return Loc.T("vol_sfx");
            return null;
        }

        /// <summary>Selected state of option buttons that only show it with a colour.</summary>
        private static string SpecialState(Selectable s)
        {
            var ros = GameCanvas.instance != null ? GameCanvas.instance.CurrentScreen as RunOptionsScreen : null;
            if (ros == null) return null;
            bool? selected = null;
            if (s == ros.ShortMoon) selected = ros.CurMoonLength == MoonLength.Short;
            else if (s == ros.NormalMoon) selected = ros.CurMoonLength == MoonLength.Normal;
            else if (s == ros.LongMoon) selected = ros.CurMoonLength == MoonLength.Long;
            else if (s == ros.PeacefulModeOn) selected = ros.PeacefulMode;
            else if (s == ros.PeacefulModeOff) selected = !ros.PeacefulMode;
            if (selected == null) return null;
            return selected.Value ? Loc.T("selected") : Loc.T("not_selected");
        }

        private static string SliderValue(Slider sl)
        {
            if (sl.maxValue <= 1.0001f && sl.minValue >= 0f && !sl.wholeNumbers)
                return Mathf.RoundToInt(sl.normalizedValue * 100f) + " %";
            return sl.value.ToString("0.##");
        }

        private static readonly string[] WorkshopTags = { "Cards", "Gameplay", "Language", "Quality of Life", "Content", "Development" };

        public static string Label(Selectable s)
        {
            var entry = s.GetComponentInParent<CardopediaEntryElement>();
            if (entry != null && entry.MyCardData != null)
            {
                bool undiscovered = entry.UndiscoveredTransform != null && entry.UndiscoveredTransform.gameObject.activeSelf;
                return undiscovered ? "???" : TextCleaner.Clean(entry.MyCardData.FullName);
            }
            var ach = s.GetComponentInParent<AchievementElement>();
            if (ach != null && ach.MyQuest != null)
            {
                bool done = QuestManager.instance != null && QuestManager.instance.QuestIsComplete(ach.MyQuest);
                return TextCleaner.Join(TextCleaner.Clean(ach.MyQuest.Description), done ? Loc.T("quest_done") : "");
            }
            if (s is TMP_InputField inf)
            {
                string ph = inf.placeholder is TMP_Text pt ? TextCleaner.Clean(pt.text) : "";
                string near = NeighbourLabel(s.transform);
                return TextCleaner.Join(near, ph);
            }
            if (s is TMP_Dropdown dd)
            {
                return TextCleaner.Join(NeighbourLabel(s.transform), dd.captionText != null ? TextCleaner.Clean(dd.captionText.text) : "");
            }
            if (s is Slider || s is Toggle)
            {
                string own = OwnText(s.transform);
                return own != "" ? own : NeighbourLabel(s.transform);
            }
            if (s is CustomButton cb)
            {
                string raw = cb.TextMeshPro != null ? cb.TextMeshPro.text ?? "" : "";
                string t = TextCleaner.Clean(raw);
                // Struck-through text means "off" (e.g. the Workshop tag picker of the mod upload screen).
                if (t != "" && raw.Contains("<s>")) return TextCleaner.Join(t, Loc.T("btn_off"));
                if (t != "" && GameCanvas.instance != null && GameCanvas.instance.CurrentScreen is ModUploadScreen &&
                    System.Array.IndexOf(WorkshopTags, t) >= 0) return TextCleaner.Join(t, Loc.T("btn_on"));
                if (t == "") t = TextCleaner.Clean(cb.TooltipText);
                if (t == "") t = OwnText(cb.transform);
                if (t == "") t = Humanize(cb.gameObject.name);
                return t;
            }
            string o = OwnText(s.transform);
            return o != "" ? o : Humanize(s.gameObject.name);
        }

        private static string OwnText(Transform t)
        {
            var texts = t.GetComponentsInChildren<TMP_Text>(false)
                .Select(x => TextCleaner.Clean(x.text)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            return TextCleaner.Join(texts);
        }

        /// <summary>Text label placed next to a control (same parent, not inside another control).</summary>
        private static string NeighbourLabel(Transform t)
        {
            var p = t.parent;
            for (int depth = 0; depth < 2 && p != null; depth++, p = p.parent)
            {
                foreach (Transform child in p)
                {
                    if (child == t || child.GetComponentInChildren<Selectable>() != null) continue;
                    var tmp = child.GetComponentInChildren<TMP_Text>();
                    if (tmp != null)
                    {
                        var s = TextCleaner.Clean(tmp.text);
                        if (s != "" && !IsNumeric(s)) return s;
                    }
                }
            }
            return "";
        }

        private static string Humanize(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            n = n.Replace("Button", "").Replace("(Clone)", "").Replace("_", " ");
            var sb = new StringBuilder();
            for (int i = 0; i < n.Length; i++)
            {
                if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
                sb.Append(n[i]);
            }
            return sb.ToString().Trim();
        }

        // ------------------------------------------------------------------ keys

        private void HandleKeys()
        {
            if (items.Count == 0)
            {
                if (Keys.Down(Key.UpArrow) || Keys.Down(Key.DownArrow) || Keys.EnterDown)
                {
                    string txt = ScreenText();
                    Speech.Say(txt != "" ? txt : Loc.T("no_items"));
                }
                if (Keys.Letter('r')) Speech.Say(ScreenText());
                return;
            }
            if (Keys.Repeat(Key.DownArrow) || (Keys.Down(Key.Tab) && !Keys.Shift)) MoveFocus(+1);
            else if (Keys.Repeat(Key.UpArrow) || (Keys.Down(Key.Tab) && Keys.Shift)) MoveFocus(-1);
            else if (Keys.Down(Key.Home)) SetFocus(0);
            else if (Keys.Down(Key.End)) SetFocus(items.Count - 1);
            else if (Keys.EnterDown) ActivateFocused();
            else if (Keys.Repeat(Key.RightArrow)) Adjust(+1);
            else if (Keys.Repeat(Key.LeftArrow)) Adjust(-1);
            else if (Keys.Letter('i')) Speech.Say(Info(focused));
            else if (Keys.Letter('r')) Speech.Say(Loc.T("screen_text", ScreenText()));
        }

        private void MoveFocus(int delta)
        {
            int i = focused != null ? items.IndexOf(focused) : -1;
            int ni = Mathf.Clamp(i + delta, 0, items.Count - 1);
            SetFocus(ni);
        }

        private void SetFocus(int index)
        {
            if (items.Count == 0) return;
            focused = items[Mathf.Clamp(index, 0, items.Count - 1)];
            FocusedButton = focused as CustomButton;
            ScrollIntoView(focused);
            Speech.Say(DescribeFocus(focused));
        }

        private static void ScrollIntoView(Selectable s)
        {
            try
            {
                var sr = s.GetComponentInParent<ScrollRect>();
                if (sr != null && s.transform is RectTransform rt) GameCanvas.SetScrollRectPosition(sr, rt);
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private string Info(Selectable s)
        {
            if (s == null) return Loc.T("no_info");
            var entry = s.GetComponentInParent<CardopediaEntryElement>();
            if (entry != null && entry.MyCardData != null)
            {
                var scr = GameCanvas.instance.CurrentScreen as CardopediaScreen;
                if (scr != null && scr.CardDescription != null) return TextCleaner.Join(Label(s), TextCleaner.Clean(scr.CardDescription.text));
                return TextCleaner.Join(Label(s), TextCleaner.Clean(entry.MyCardData.Description));
            }
            if (s is CustomButton cb && !string.IsNullOrWhiteSpace(cb.TooltipText)) return TextCleaner.Clean(cb.TooltipText);
            var tip = s.GetComponent<ShowTooltip>();
            if (tip != null)
            {
                string t = !string.IsNullOrWhiteSpace(tip.MyTooltipText) ? tip.MyTooltipText : (!string.IsNullOrEmpty(tip.MyTooltipTerm) ? SokLoc.Translate(tip.MyTooltipTerm) : "");
                if (!string.IsNullOrWhiteSpace(t)) return TextCleaner.Clean(t);
            }
            return Loc.T("no_info");
        }

        private void ActivateFocused()
        {
            var s = focused;
            if (s == null) return;
            if (!IsEnabled(s)) { Speech.Say(Describe(s, false)); return; }
            string before = Describe(s, withPosition: false);
            switch (s)
            {
                case CustomButton cb:
                    Traverse.Create(cb).Method("SubmitClick").GetValue();
                    break;
                case Toggle tg:
                    tg.isOn = !tg.isOn;
                    break;
                case TMP_InputField inf:
                    editing = inf;
                    editText = inf.text ?? "";
                    editStartFrame = Time.frameCount;
                    EventSystem.current?.SetSelectedGameObject(inf.gameObject);
                    inf.ActivateInputField();
                    Speech.Say(TextCleaner.Join(Label(inf), Loc.T("editing"), editText));
                    return;
                case TMP_Dropdown dd:
                    Adjust(+1);
                    return;
                case Button b:
                    b.onClick.Invoke();
                    break;
                default:
                    ExecuteEvents.Execute(s.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                    break;
            }
            // Many option buttons change their own text when clicked (On/Off...): read it again.
            if (GameCanvas.instance.CurrentScreen is CutsceneScreen && !GameCanvas.instance.ModalIsOpen) return; // next text is read by the EventNarrator
            pendingRelabel = before;
            pendingRelabelItem = s;
            pendingRelabelTime = Time.unscaledTime + 0.15f;
        }

        private void Adjust(int dir)
        {
            var s = focused;
            Slider slider = s as Slider;
            if (slider == null && s is CustomButton) slider = s.GetComponentInChildren<Slider>();
            if (slider != null)
            {
                float step = slider.wholeNumbers ? 1f : (slider.maxValue - slider.minValue) * 0.05f;
                slider.value = Mathf.Clamp(slider.value + dir * step, slider.minValue, slider.maxValue);
                Speech.Say(SliderValue(slider));
                return;
            }
            if (s is TMP_Dropdown dd && dd.options.Count > 0)
            {
                dd.value = (dd.value + dir + dd.options.Count) % dd.options.Count;
                dd.RefreshShownValue();
                Speech.Say(TextCleaner.Clean(dd.options[dd.value].text));
                return;
            }
            if (s is Toggle tg)
            {
                tg.isOn = dir > 0;
                Speech.Say(tg.isOn ? Loc.T("checked") : Loc.T("unchecked"));
            }
        }

        private void HandleEditing()
        {
            var inf = editing;
            if (inf == null || !inf.gameObject.activeInHierarchy) { editing = null; return; }
            string now = inf.text ?? "";
            if (now != editText)
            {
                if (now.Length > editText.Length && now.StartsWith(editText)) Speech.Say(now.Substring(editText.Length));
                else if (now.Length < editText.Length && editText.StartsWith(now)) Speech.Say(Loc.T("deleted", editText.Substring(now.Length)));
                else Speech.Say(now);
                editText = now;
            }
            bool lostFocus = !inf.isFocused && Time.frameCount - editStartFrame > 3;
            if (Keys.EnterDown || Keys.Down(Key.Escape) || lostFocus)
            {
                if (inf.isFocused) inf.DeactivateInputField();
                editing = null;
                Speech.Say(Loc.T("edit_done", string.IsNullOrEmpty(inf.text) ? Loc.T("empty") : inf.text));
            }
        }
    }
}
