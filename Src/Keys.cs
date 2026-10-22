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
        /// <summary>Fixed navigation keys used by the mod (physical keys). The other keys come from <see cref="Bindings"/>.</summary>
        public static readonly string[] ReservedPaths =
        {
            "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
            "<Keyboard>/pageUp", "<Keyboard>/pageDown", "<Keyboard>/home", "<Keyboard>/end",
            "<Keyboard>/enter", "<Keyboard>/numpadEnter",
        };

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
        /// <summary>Game bindings turned off by the mod, with the player's own rebinding to put back (null: none).</summary>
        private static readonly List<(UnityEngine.InputSystem.InputAction action, int index, string previous)> overridden = new List<(UnityEngine.InputSystem.InputAction, int, string)>();

        /// <summary>The mod keys changed: give the game back its keys and turn off the ones the mod now uses.</summary>
        public static void RefreshConflictingBindings()
        {
            foreach (var (action, index, previous) in overridden)
            {
                try
                {
                    if (previous == null) action.RemoveBindingOverride(index);
                    else action.ApplyBindingOverride(index, previous);
                }
                catch (System.Exception ex) { Log.Once(ex); }
            }
            overridden.Clear();
            bindingsDone = false;
            letterCache.Clear();
            DisableConflictingBindings();
        }

        /// <summary>
        /// Logs every keyboard binding of the game and disables the ones on keys used by the mod
        /// (camera movement on the arrows, etc). Overrides are not saved, the game's own rebinding is untouched.
        /// </summary>
        public static void DisableConflictingBindings()
        {
            if (bindingsDone) return;
            var ic = InputController.instance;
            if (ic == null || ic.PlayerInput == null || ic.PlayerInput.actions == null) return;
            bindingsDone = true;
            var reserved = new HashSet<string>(ReservedPaths);
            foreach (var path in Bindings.UsedPaths()) reserved.Add(path);
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
                        overridden.Add((action, i, b.overridePath));
                        action.ApplyBindingOverride(i, "");
                    }
                }
            }
            Debug.Log(log.ToString());
        }
    }
}
