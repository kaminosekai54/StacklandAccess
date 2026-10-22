using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>
    /// A spoken one-line text field for the mod's own panels (the screen reader cannot see them):
    /// each typed character is spoken, Backspace erases, Enter confirms, Escape cancels.
    /// </summary>
    public static class TextPrompt
    {
        public static bool Active { get; private set; }

        /// <summary>For the visual menu: the field's name, and its text (stars for a password).</summary>
        public static string Label => label ?? "";
        public static string DisplayText => hidden ? new string('*', text.Length) : text.ToString();

        private static string label;
        private static readonly StringBuilder text = new StringBuilder();
        private static bool hidden;
        private static Action<string> onDone;
        private static Action onCancel;
        private static int openFrame;
        private static Keyboard hooked;

        public static void Open(string fieldLabel, string initial, bool hideText, Action<string> done, Action cancel = null)
        {
            Close();
            label = fieldLabel;
            text.Clear();
            text.Append(initial ?? "");
            hidden = hideText;
            onDone = done;
            onCancel = cancel;
            openFrame = Time.frameCount;
            hooked = Keyboard.current;
            if (hooked != null) hooked.onTextInput += OnChar;
            Active = true;
            Speech.Say(TextCleaner.Join(Loc.T("edit_start", label, Shown()), Loc.Hint("edit_hint")));
        }

        private static string Shown()
        {
            if (text.Length == 0) return Loc.T("edit_empty");
            return hidden ? Loc.N("edit_hidden_count", text.Length, text.Length) : text.ToString();
        }

        private static string Spoken(char c)
        {
            if (hidden) return Loc.T("edit_hidden");
            if (c == ' ') return Loc.T("edit_space");
            return c.ToString();
        }

        private static void OnChar(char c)
        {
            if (!Active || Time.frameCount == openFrame || char.IsControl(c)) return;
            if (text.Length >= 80) return;
            text.Append(c);
            Speech.Say(Spoken(c));
        }

        /// <summary>Called every frame while active, before any other key handling.</summary>
        public static void Update()
        {
            if (!Active || Time.frameCount == openFrame) return;
            if (Keys.Down(Key.Escape))
            {
                var cancel = onCancel;
                Close();
                Speech.Say(Loc.T("edit_cancel"));
                cancel?.Invoke();
                return;
            }
            if (Keys.EnterDown)
            {
                var done = onDone;
                string value = text.ToString().Trim();
                Close();
                done?.Invoke(value);
                return;
            }
            if (Keys.Repeat(Key.Backspace))
            {
                if (text.Length == 0) { Speech.Say(Loc.T("edit_empty")); return; }
                char c = text[text.Length - 1];
                text.Length--;
                Speech.Say(Loc.T("edit_deleted", Spoken(c)));
                return;
            }
            if (Keys.Down(Key.UpArrow) || Keys.Down(Key.DownArrow) || Keys.Down(Key.F2))
            {
                Speech.Say(TextCleaner.Join(label, Shown()));
            }
        }

        public static void Close()
        {
            if (hooked != null) hooked.onTextInput -= OnChar;
            hooked = null;
            Active = false;
        }
    }
}
