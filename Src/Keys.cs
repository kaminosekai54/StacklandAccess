using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace StacklandAccessNS
{
    /// <summary>Keyboard helpers and removal of game bindings that clash with the mod keys.</summary>
    public static class Keys
    {
        /// <summary>Non-letter keys used by the mod (physical keys).</summary>
        public static readonly string[] ReservedPaths =
        {
            "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
            "<Keyboard>/pageUp", "<Keyboard>/pageDown", "<Keyboard>/home", "<Keyboard>/end",
            "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/backspace", "<Keyboard>/delete",
            "<Keyboard>/f1", "<Keyboard>/f2", "<Keyboard>/f3", "<Keyboard>/f4",
            "<Keyboard>/numpadMinus", "<Keyboard>/numpadPlus", "<Keyboard>/f5",
            "<Keyboard>/numpad0", "<Keyboard>/numpad1", "<Keyboard>/numpad2", "<Keyboard>/numpad3", "<Keyboard>/numpad4",
        };

        /// <summary>Symbols used by the mod (game speed), matched by printed character like the letters.</summary>
        public const string ModSymbols = "-=+";

        public static bool SpeedDown => Down(Key.NumpadMinus) || Letter('-');
        public static bool SpeedUp => Down(Key.NumpadPlus) || Letter('+') || Letter('=');

        /// <summary>
        /// Letters used by the mod. They are matched by the character printed on the key
        /// (so Q is the Q key on an AZERTY keyboard too), not by the physical position.
        /// </summary>
        public const string ModLetters = "irtqlncpbhfxgmaokujy";

        private static readonly Dictionary<char, KeyControl> letterCache = new Dictionary<char, KeyControl>();
        private static string cachedLayout;

        public static KeyControl LetterKey(char c)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            if (cachedLayout != kb.keyboardLayout)
            {
                cachedLayout = kb.keyboardLayout;
                letterCache.Clear();
            }
            if (letterCache.TryGetValue(c, out var k)) return k;
            try
            {
                k = kb.FindKeyOnCurrentKeyboardLayout(c.ToString()) ?? kb.FindKeyOnCurrentKeyboardLayout(char.ToUpperInvariant(c).ToString());
            }
            catch { k = null; }
            if (k == null)
            {
                // Fallback: physical key with the same name (US layout).
                try { k = kb[(Key)System.Enum.Parse(typeof(Key), char.ToUpperInvariant(c).ToString())]; } catch { k = null; }
            }
            letterCache[c] = k;
            return k;
        }

        /// <summary>Letter key (by printed character) pressed this frame.</summary>
        public static bool Letter(char c)
        {
            var k = LetterKey(c);
            return k != null && k.wasPressedThisFrame;
        }

        public static Keyboard Kb => Keyboard.current;

        public static bool Down(Key k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[k].wasPressedThisFrame;
        }

        /// <summary>Pressed this frame, or held long enough to auto repeat.</summary>
        private static readonly Dictionary<Key, float> heldSince = new Dictionary<Key, float>();
        private static readonly Dictionary<Key, float> lastRepeat = new Dictionary<Key, float>();

        public static bool Repeat(Key k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var c = kb[k];
            float now = Time.unscaledTime;
            if (c.wasPressedThisFrame)
            {
                heldSince[k] = now;
                lastRepeat[k] = now;
                return true;
            }
            if (c.isPressed && heldSince.TryGetValue(k, out var since) && now - since > 0.45f)
            {
                if (now - lastRepeat[k] > 0.09f)
                {
                    lastRepeat[k] = now;
                    return true;
                }
            }
            return false;
        }

        public static bool Shift
        {
            get { var kb = Keyboard.current; return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed); }
        }

        public static bool Ctrl
        {
            get { var kb = Keyboard.current; return kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed); }
        }

        public static bool EnterDown => Down(Key.Enter) || Down(Key.NumpadEnter);

        private static bool bindingsDone;

        /// <summary>
        /// Logs every keyboard binding of the game and disables the ones on keys reserved by the mod
        /// (camera movement on the arrows, etc). Overrides are not saved, the game's own rebinding is untouched.
        /// </summary>
        public static void DisableConflictingBindings()
        {
            if (bindingsDone) return;
            var ic = InputController.instance;
            if (ic == null || ic.PlayerInput == null || ic.PlayerInput.actions == null) return;
            bindingsDone = true;
            var reserved = new HashSet<string>(ReservedPaths);
            foreach (char c in ModLetters + ModSymbols)
            {
                var k = LetterKey(c);
                if (k != null) reserved.Add("<Keyboard>/" + k.name);
            }
            var log = new StringBuilder("[StacklandAccess] Game keyboard bindings:\n");
            foreach (var action in ic.PlayerInput.actions)
            {
                if (action.actionMap != null && action.actionMap.name == "UI") continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    string path = b.effectivePath ?? b.path;
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>")) continue;
                    bool clash = reserved.Contains(path);
                    log.Append("  ").Append(action.name).Append(" = ").Append(path).Append(clash ? "  -> disabled by mod\n" : "\n");
                    if (clash)
                    {
                        // Keep "cancel" on Escape and "submit" logic intact: we only remove our own keys.
                        action.ApplyBindingOverride(i, "");
                    }
                }
            }
            Debug.Log(log.ToString());
        }
    }
}
