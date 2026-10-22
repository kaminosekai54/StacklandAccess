using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>
    /// Controller support. The face buttons, shoulders and directions act like the mod's keys;
    /// holding a trigger (LT or RT) gives a second and a third set of actions on the same buttons.
    ///   Directions (cross or left stick): arrows. A: Enter. B: Escape. X: information (I). Y: recipes (L).
    ///   LB / RB: Page up / Page down. Start: options. Select: help. L3: repeat. R3: resources (or read the screen in menus).
    /// While controller support is on, the game's own controller bindings are turned off (the mod drives the game).
    /// </summary>
    public static class Pad
    {
        public enum Button { None, A, B, X, Y, LB, RB, Start, Select, L3, R3, Up, Down, Left, Right }
        public enum Layer { None, LT, RT }

        public struct Input
        {
            public Button Button;
            public Layer Layer;
            public Input(Button b, Layer l = Layer.None) { Button = b; Layer = l; }
            public bool IsNone => Button == Button.None;

            public string Spoken()
            {
                if (IsNone) return "";
                string b;
                switch (Button)
                {
                    case Button.Up: b = Loc.T("pad_up"); break;
                    case Button.Down: b = Loc.T("pad_down"); break;
                    case Button.Left: b = Loc.T("pad_left"); break;
                    case Button.Right: b = Loc.T("pad_right"); break;
                    default: b = Button.ToString(); break;
                }
                return Layer == Layer.None ? b : Layer + "+" + b;
            }
        }

        public static bool Enabled = true;

        private const float StickThreshold = 0.6f;
        private const float TriggerThreshold = 0.5f;
        private const float RepeatDelay = 0.45f;
        private const float RepeatRate = 0.12f;

        private static int frame = -1;
        private static Layer layer;
        private static readonly HashSet<Button> down = new HashSet<Button>();
        private static readonly HashSet<Button> first = new HashSet<Button>();
        private static readonly HashSet<Button> repeat = new HashSet<Button>();
        private static readonly Dictionary<Button, float> heldSince = new Dictionary<Button, float>();
        private static readonly Dictionary<Button, float> lastRepeat = new Dictionary<Button, float>();

        public static bool Connected => Gamepad.current != null;

        /// <summary>Reads the controller once per frame (whoever asks first).</summary>
        private static void Refresh()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            first.Clear();
            repeat.Clear();
            var gp = Enabled ? Gamepad.current : null;
            if (gp == null) { down.Clear(); layer = Layer.None; return; }
            layer = gp.leftTrigger.ReadValue() > TriggerThreshold ? Layer.LT : gp.rightTrigger.ReadValue() > TriggerThreshold ? Layer.RT : Layer.None;
            Vector2 stick = gp.leftStick.ReadValue();
            float now = Time.unscaledTime;
            Check(Button.A, gp.buttonSouth.isPressed, now, false);
            Check(Button.B, gp.buttonEast.isPressed, now, false);
            Check(Button.X, gp.buttonWest.isPressed, now, false);
            Check(Button.Y, gp.buttonNorth.isPressed, now, false);
            Check(Button.LB, gp.leftShoulder.isPressed, now, false);
            Check(Button.RB, gp.rightShoulder.isPressed, now, false);
            Check(Button.Start, gp.startButton.isPressed, now, false);
            Check(Button.Select, gp.selectButton.isPressed, now, false);
            Check(Button.L3, gp.leftStickButton.isPressed, now, false);
            Check(Button.R3, gp.rightStickButton.isPressed, now, false);
            Check(Button.Up, gp.dpad.up.isPressed || stick.y > StickThreshold, now, true);
            Check(Button.Down, gp.dpad.down.isPressed || stick.y < -StickThreshold, now, true);
            Check(Button.Left, gp.dpad.left.isPressed || stick.x < -StickThreshold, now, true);
            Check(Button.Right, gp.dpad.right.isPressed || stick.x > StickThreshold, now, true);
        }

        private static void Check(Button b, bool isDown, float now, bool repeats)
        {
            bool was = down.Contains(b);
            if (!isDown) { if (was) down.Remove(b); return; }
            if (!was)
            {
                down.Add(b);
                first.Add(b);
                repeat.Add(b);
                heldSince[b] = now;
                lastRepeat[b] = now;
                return;
            }
            if (repeats && now - heldSince[b] > RepeatDelay && now - lastRepeat[b] > RepeatRate)
            {
                lastRepeat[b] = now;
                repeat.Add(b);
            }
        }

        /// <summary>The button was pressed this frame, with this trigger held (or none).</summary>
        public static bool Pressed(Input input)
        {
            if (input.IsNone || !Enabled) return false;
            Refresh();
            return layer == input.Layer && first.Contains(input.Button);
        }

        public static bool Pressed(Button b, Layer l = Layer.None) => Pressed(new Input(b, l));

        /// <summary>The button was pressed this frame whatever trigger is held (to keep it from the game).</summary>
        public static bool PressedAnyLayer(Button b)
        {
            if (!Enabled) return false;
            Refresh();
            return first.Contains(b);
        }

        public static bool AnyLayerHeld { get { Refresh(); return layer != Layer.None; } }

        /// <summary>Controller equivalent of a navigation key: pressed this frame (or auto-repeated).</summary>
        public static bool NavKey(UnityEngine.InputSystem.Key key, bool withRepeat)
        {
            if (!Enabled) return false;
            Refresh();
            var set = withRepeat ? repeat : first;
            switch (key)
            {
                case UnityEngine.InputSystem.Key.UpArrow: return layer == Layer.None && set.Contains(Button.Up);
                case UnityEngine.InputSystem.Key.DownArrow: return layer == Layer.None && set.Contains(Button.Down);
                case UnityEngine.InputSystem.Key.LeftArrow: return layer == Layer.None && set.Contains(Button.Left);
                case UnityEngine.InputSystem.Key.RightArrow: return layer == Layer.None && set.Contains(Button.Right);
                case UnityEngine.InputSystem.Key.Enter: return layer == Layer.None && first.Contains(Button.A);
                case UnityEngine.InputSystem.Key.Escape: return layer == Layer.None && first.Contains(Button.B);
                case UnityEngine.InputSystem.Key.PageUp: return layer == Layer.None && first.Contains(Button.LB);
                case UnityEngine.InputSystem.Key.PageDown: return layer == Layer.None && first.Contains(Button.RB);
                case UnityEngine.InputSystem.Key.Home: return layer == Layer.LT && first.Contains(Button.LB);
                case UnityEngine.InputSystem.Key.End: return layer == Layer.LT && first.Contains(Button.RB);
            }
            return false;
        }

        /// <summary>Controller buttons of the mod's actions (see <see cref="Bindings"/>).</summary>
        public static Input Default(string actionId)
        {
            switch (actionId)
            {
                case "help": return new Input(Button.Select);
                case "repeat": return new Input(Button.L3);
                case "options": return new Input(Button.Start);
                case "toggle_narration": return new Input(Button.Start, Layer.RT);
                case "recipes": return new Input(Button.Y);
                case "details": return new Input(Button.X);
                case "list_info": return new Input(Button.X);
                case "menu_info": return new Input(Button.X);
                case "menu_read": return new Input(Button.R3);
                case "resources": return new Input(Button.R3);
                case "click": return new Input(Button.X, Layer.LT);
                case "quests": return new Input(Button.Y, Layer.LT);
                case "drop_alone": return new Input(Button.B, Layer.LT);
                case "time": return new Input(Button.Up, Layer.LT);
                case "history": return new Input(Button.Down, Layer.LT);
                case "villagers": return new Input(Button.Left, Layer.LT);
                case "connections": return new Input(Button.Right, Layer.LT);
                case "go_held": return new Input(Button.A, Layer.RT);
                case "sell": return new Input(Button.B, Layer.RT);
                case "restack": return new Input(Button.X, Layer.RT);
                case "restack_category": return new Input(Button.Y, Layer.RT);
                case "speed_down": return new Input(Button.Left, Layer.RT);
                case "speed_up": return new Input(Button.Right, Layer.RT);
                case "speed_pause": return new Input(Button.Down, Layer.RT);
                case "speed_2": return new Input(Button.Up, Layer.RT);
            }
            return new Input(Button.None);
        }

        /// <summary>LT+A: move a stack on the board (Ctrl+Enter on the keyboard).</summary>
        public static bool MoveModePressed => Pressed(Button.A, Layer.LT);
    }
}
