using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace StacklandAccessNS
{
    /// <summary>
    /// The mod's remappable keys. Each action has one or more keys; a key is either a physical key
    /// (F5, Delete, numpad 0...) or a printed character (the L key is the key that prints "l" on the
    /// player's keyboard layout), with Ctrl and Shift.
    /// Navigation keys (arrows, Enter, Escape, Home, End, Page up, Page down, Tab) are fixed.
    /// Saved in StacklandAccess_keys.txt next to the game's save files, one line per changed action.
    /// </summary>
    public static class Bindings
    {
        public enum Context { Global, Board, List, Menu }

        public class Spec
        {
            public bool Ctrl, Shift;
            public Key Key = Key.None;
            public char Char;

            public bool IsChar => Char != '\0';

            public bool SameKey(Spec o) => o != null && Ctrl == o.Ctrl && Shift == o.Shift && Key == o.Key && Char == o.Char;

            public override string ToString()
            {
                var sb = new StringBuilder();
                if (Ctrl) sb.Append("ctrl+");
                if (Shift) sb.Append("shift+");
                if (IsChar) sb.Append("char:").Append(((int)Char).ToString("x"));
                else sb.Append("key:").Append(Key.ToString());
                return sb.ToString();
            }

            public static Spec Parse(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var spec = new Spec();
                s = s.Trim();
                while (true)
                {
                    if (s.StartsWith("ctrl+")) { spec.Ctrl = true; s = s.Substring(5); continue; }
                    if (s.StartsWith("shift+")) { spec.Shift = true; s = s.Substring(6); continue; }
                    break;
                }
                try
                {
                    if (s.StartsWith("char:")) { spec.Char = (char)System.Convert.ToInt32(s.Substring(5), 16); return spec; }
                    if (s.StartsWith("key:") && System.Enum.TryParse(s.Substring(4), out Key k) && k != Key.None) { spec.Key = k; return spec; }
                }
                catch { }
                return null;
            }

            /// <summary>Shortcut for the defaults: "l", "ctrl+p", "F5", "shift+F6", "NumpadPlus".</summary>
            public static Spec Short(string s)
            {
                var spec = new Spec();
                while (true)
                {
                    if (s.StartsWith("ctrl+") && s.Length > 5) { spec.Ctrl = true; s = s.Substring(5); continue; }
                    if (s.StartsWith("shift+") && s.Length > 6) { spec.Shift = true; s = s.Substring(6); continue; }
                    break;
                }
                if (s.Length == 1) spec.Char = s[0];
                else spec.Key = (Key)System.Enum.Parse(typeof(Key), s);
                return spec;
            }

            /// <summary>The keyboard control this key is on (for the printed character: on the current layout).</summary>
            public KeyControl Control
            {
                get
                {
                    var kb = Keyboard.current;
                    if (kb == null) return null;
                    if (IsChar) return Keys.LetterKey(Char);
                    try { return kb[Key]; } catch { return null; }
                }
            }

            public bool PressedThisFrame
            {
                get
                {
                    if (Keys.Ctrl != Ctrl) return false;
                    // A printed character may need Shift on some layouts ("+" on an AZERTY keyboard):
                    // Shift only counts when the binding asks for it, or for a physical key.
                    if ((Shift || !IsChar) && Keys.Shift != Shift) return false;
                    var c = Control;
                    return c != null && c.wasPressedThisFrame;
                }
            }

            public string Spoken()
            {
                var parts = new List<string>();
                if (Ctrl) parts.Add(Loc.T("key_ctrl"));
                if (Shift) parts.Add(Loc.T("key_shift"));
                if (IsChar) parts.Add(char.ToUpperInvariant(Char).ToString());
                else
                {
                    string name = null;
                    try { var c = Control; if (c != null) name = c.displayName; } catch { }
                    if (string.IsNullOrEmpty(name)) name = Key.ToString();
                    if (Key >= Key.Numpad0 && Key <= Key.Numpad9) name = Loc.T("key_numpad", (int)(Key - Key.Numpad0));
                    else if (Key == Key.NumpadPlus) name = Loc.T("key_numpad", "+");
                    else if (Key == Key.NumpadMinus) name = Loc.T("key_numpad", "-");
                    parts.Add(name);
                }
                return string.Join("+", parts);
            }
        }

        public class Action
        {
            public string Id;
            public Context Context;
            /// <summary>Name in the options (a Loc key).</summary>
            public string NameKey;
            public Spec[] Defaults;
            public List<Spec> Specs;

            public string Name => Loc.T(NameKey);
            public string KeysSpoken => Specs.Count == 0 ? Loc.T("key_none") : string.Join(Loc.T("key_or"), Specs.Select(s => s.Spoken()));
        }

        private static readonly List<Action> actions = new List<Action>();
        private static readonly Dictionary<string, Action> byId = new Dictionary<string, Action>();

        public static IReadOnlyList<Action> All => actions;

        private static void Add(string id, Context ctx, string nameKey, params string[] defaults)
        {
            var a = new Action { Id = id, Context = ctx, NameKey = nameKey, Defaults = defaults.Select(Spec.Short).ToArray() };
            a.Specs = a.Defaults.ToList();
            actions.Add(a);
            byId[id] = a;
        }

        static Bindings()
        {
            // Everywhere
            Add("help", Context.Global, "act_help", "F1");
            Add("repeat", Context.Global, "act_repeat", "F2");
            Add("toggle_new_cards", Context.Global, "act_toggle_new_cards", "F3");
            Add("toggle_autostack", Context.Global, "act_toggle_autostack", "F4");
            Add("export", Context.Global, "act_export", "F6");
            Add("export_site", Context.Global, "act_export_site", "shift+F6");
            Add("options", Context.Global, "act_options", "F7");
            Add("toggle_narration", Context.Global, "act_toggle_narration", "ctrl+F2");
            // Board
            Add("restack", Context.Board, "act_restack", "F5");
            Add("restack_category", Context.Board, "act_restack_category", "ctrl+F5");
            Add("villagers", Context.Board, "act_villagers", "ctrl+p");
            Add("resources", Context.Board, "act_resources", "r");
            Add("time", Context.Board, "act_time", "t");
            Add("quests", Context.Board, "act_quests", "q");
            Add("recipes", Context.Board, "act_recipes", "l");
            Add("history", Context.Board, "act_history", "n");
            Add("details", Context.Board, "act_details", "i");
            Add("click", Context.Board, "act_click", "c");
            Add("go_held", Context.Board, "act_go_held", "m");
            Add("connections", Context.Board, "act_connections", "e");
            Add("sell", Context.Board, "act_sell", "Delete");
            Add("drop_alone", Context.Board, "act_drop_alone", "Backspace");
            Add("cat_people", Context.Board, "cat_people", "p");
            Add("cat_shop", Context.Board, "cat_shop", "b");
            Add("cat_buildings", Context.Board, "cat_buildings", "h");
            Add("cat_food", Context.Board, "cat_food", "f");
            Add("cat_resources", Context.Board, "cat_resources", "g");
            Add("cat_enemies", Context.Board, "cat_enemies", "x");
            Add("cat_animals", Context.Board, "cat_animals", "a");
            Add("cat_equipment", Context.Board, "cat_equipment", "o");
            Add("cat_ideas", Context.Board, "cat_ideas", "k");
            Add("cat_locations", Context.Board, "cat_locations", "u");
            Add("cat_other", Context.Board, "cat_other", "j");
            Add("cat_all", Context.Board, "cat_all", "y");
            Add("speed_pause", Context.Board, "act_speed_pause", "Numpad0");
            Add("speed_1", Context.Board, "act_speed_1", "Numpad1");
            Add("speed_2", Context.Board, "act_speed_2", "Numpad2");
            Add("speed_3", Context.Board, "act_speed_3", "Numpad3");
            Add("speed_4", Context.Board, "act_speed_4", "Numpad4");
            Add("speed_down", Context.Board, "act_speed_down", "NumpadMinus", "-");
            Add("speed_up", Context.Board, "act_speed_up", "NumpadPlus", "+", "=");
            // Lists and game menus
            Add("list_info", Context.List, "act_list_info", "i");
            Add("menu_info", Context.Menu, "act_menu_info", "i");
            Add("menu_read", Context.Menu, "act_menu_read", "r");
            Load();
        }

        public static Action Get(string id) => byId.TryGetValue(id, out var a) ? a : null;

        /// <summary>True when one of the action's keys was pressed this frame.</summary>
        public static bool Pressed(string id)
        {
            if (Capturing) return false;
            var a = Get(id);
            if (a == null) return false;
            for (int i = 0; i < a.Specs.Count; i++) if (a.Specs[i].PressedThisFrame) return true;
            return false;
        }

        public static string Spoken(string id) { var a = Get(id); return a == null ? "" : a.KeysSpoken; }

        /// <summary>Keyboard keys used by the mod (physical paths), so the game's own bindings on them can be turned off.</summary>
        public static IEnumerable<string> UsedPaths()
        {
            foreach (var a in actions)
                foreach (var s in a.Specs)
                {
                    KeyControl c = null;
                    try { c = s.Control; } catch { }
                    if (c != null) yield return "<Keyboard>/" + c.name;
                }
        }

        // ------------------------------------------------------------------ saving

        private static string FilePath => Path.Combine(Application.persistentDataPath, "StacklandAccess_keys.txt");

        private static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var cols = line.Split('\t');
                    if (cols.Length < 1) continue;
                    var a = Get(cols[0].Trim());
                    if (a == null) continue;
                    a.Specs = cols.Skip(1).Select(Spec.Parse).Where(s => s != null).ToList();
                }
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (var a in actions)
                {
                    bool same = a.Specs.Count == a.Defaults.Length && a.Specs.Zip(a.Defaults, (x, y) => x.SameKey(y)).All(b => b);
                    if (same) continue;
                    lines.Add(a.Id + "\t" + string.Join("\t", a.Specs.Select(s => s.ToString())));
                }
                if (lines.Count == 0) { if (File.Exists(FilePath)) File.Delete(FilePath); }
                else File.WriteAllLines(FilePath, lines, Encoding.UTF8);
            }
            catch (System.Exception ex) { Log.Once(ex); }
            Keys.RefreshConflictingBindings();
        }

        public static void ResetAll()
        {
            foreach (var a in actions) a.Specs = a.Defaults.ToList();
            Save();
        }

        public static void Reset(string id)
        {
            var a = Get(id);
            if (a == null) return;
            a.Specs = a.Defaults.ToList();
            Save();
        }

        /// <summary>Two actions clash when they can be pressed in the same place.</summary>
        private static bool Clash(Context x, Context y)
        {
            if (x == Context.Global || y == Context.Global || x == y) return true;
            // A list opened from the board is closed by the key that opened it.
            return (x == Context.Board && y == Context.List) || (x == Context.List && y == Context.Board);
        }

        // ------------------------------------------------------------------ capturing a new key

        public static bool Capturing { get; private set; }
        private static Action capturing;
        private static int captureFrame;
        private static System.Action<string> captureDone;

        private static readonly HashSet<Key> Fixed = new HashSet<Key>
        {
            Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Enter, Key.NumpadEnter, Key.Escape,
            Key.Home, Key.End, Key.PageUp, Key.PageDown, Key.Tab, Key.F8,
        };

        private static readonly HashSet<Key> Modifiers = new HashSet<Key>
        {
            Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
            Key.LeftMeta, Key.RightMeta, Key.CapsLock, Key.NumLock, Key.ScrollLock, Key.ContextMenu, Key.PrintScreen,
        };

        /// <summary>Waits for the next key; Escape cancels. done receives the sentence to speak.</summary>
        public static void BeginCapture(string id, System.Action<string> done)
        {
            capturing = Get(id);
            if (capturing == null) return;
            Capturing = true;
            captureFrame = Time.frameCount;
            captureDone = done;
            Speech.Say(Loc.T("key_capture", capturing.Name, capturing.KeysSpoken));
        }

        /// <summary>Called every frame while capturing, before any other key handling.</summary>
        public static void UpdateCapture()
        {
            if (!Capturing || Time.frameCount == captureFrame) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Finish(Loc.T("key_capture_cancel")); return; }
            KeyControl pressed = null;
            foreach (var k in kb.allKeys)
            {
                if (k == null || !k.wasPressedThisFrame || Modifiers.Contains(k.keyCode)) continue;
                pressed = k;
                break;
            }
            if (pressed == null) return;
            if (Fixed.Contains(pressed.keyCode)) { Speech.Say(Loc.T("key_reserved", Short(pressed))); return; }
            var spec = new Spec { Ctrl = Keys.Ctrl, Shift = Keys.Shift };
            char ch = PrintedChar(pressed);
            if (ch != '\0') spec.Char = ch; else spec.Key = pressed.keyCode;
            // A printed character with Shift is matched without Shift ("+" may need Shift): keep the Shift only for physical keys.
            if (spec.IsChar && spec.Shift && char.IsLetter(ch) == false) spec.Shift = false;
            Assign(capturing, spec);
        }

        private static string Short(KeyControl k) => string.IsNullOrEmpty(k.displayName) ? k.keyCode.ToString() : k.displayName;

        /// <summary>The character printed on a letter, digit or punctuation key (not the numeric keypad).</summary>
        private static char PrintedChar(KeyControl k)
        {
            var code = k.keyCode;
            bool printable = (code >= Key.A && code <= Key.Z) || (code >= Key.Digit1 && code <= Key.Digit0)
                || code == Key.Semicolon || code == Key.Quote || code == Key.Comma || code == Key.Period || code == Key.Slash
                || code == Key.Backslash || code == Key.LeftBracket || code == Key.RightBracket || code == Key.Minus
                || code == Key.Equals || code == Key.Backquote || (code >= Key.OEM1 && code <= Key.OEM5);
            if (!printable) return '\0';
            string d = k.displayName;
            if (string.IsNullOrEmpty(d) || d.Length != 1) return '\0';
            return char.ToLowerInvariant(d[0]);
        }

        private static void Assign(Action a, Spec spec)
        {
            var old = a.Specs.ToList();
            string extra = "";
            foreach (var other in actions)
            {
                if (other == a || !Clash(a.Context, other.Context)) continue;
                int idx = other.Specs.FindIndex(s => s.SameKey(spec));
                if (idx < 0) continue;
                // Swap: the other action takes the key this one had.
                other.Specs.RemoveAt(idx);
                foreach (var o in old) if (!other.Specs.Any(s => s.SameKey(o))) other.Specs.Add(o);
                extra = Loc.T("key_swapped", other.Name, other.KeysSpoken);
            }
            a.Specs = new List<Spec> { spec };
            Save();
            Finish(TextCleaner.Join(Loc.T("key_set", a.Name, spec.Spoken()), extra));
        }

        private static void Finish(string message)
        {
            Capturing = false;
            var done = captureDone;
            captureDone = null;
            capturing = null;
            if (done != null) done(message); else Speech.Say(message);
        }
    }
}
