using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Error reporting: every problem is written once to Player.log with its place in the code,
    /// so a failing action is never silent for whoever reads the log.
    /// </summary>
    public static class Log
    {
        private static readonly HashSet<string> seen = new HashSet<string>();

        public static void Info(string message) => Debug.Log("[StacklandAccess] " + message);

        /// <summary>Logs an exception once per place in the code.</summary>
        public static void Once(Exception e, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            string where = Path.GetFileName(file) + ":" + line;
            string key = where + "|" + e.GetType().Name;
            if (!seen.Add(key)) return;
            Debug.LogWarning("[StacklandAccess] Error at " + where + ": " + e);
        }

        /// <summary>
        /// Checks the private parts of the game the mod relies on. If a game update renamed one of them,
        /// the player is told once which feature may not work.
        /// </summary>
        public static void CheckCompatibility()
        {
            var missing = new List<string>();
            void Field(Type t, string name, string feature) { if (AccessTools.Field(t, name) == null) missing.Add(feature + " (" + t.Name + "." + name + ")"); }
            void Method(Type t, string name, string feature) { if (AccessTools.Method(t, name) == null) missing.Add(feature + " (" + t.Name + "." + name + ")"); }
            Field(typeof(Draggable), "dragStartTime", Loc.T("feat_move"));
            Method(typeof(CustomButton), "SubmitClick", Loc.T("feat_buttons"));
            Field(typeof(GameCanvas), "previousHovered", Loc.T("feat_focus"));
            Method(typeof(GameCanvas), "UpdateCustomButtonHovered", Loc.T("feat_focus"));
            Method(typeof(Subprint), "ParseCardId", Loc.T("feat_recipes"));
            Method(typeof(GameCamera), "GetCurrentGroundOffset", Loc.T("feat_camera"));
            if (missing.Count == 0)
            {
                Info("Compatibility check OK");
                return;
            }
            Debug.LogWarning("[StacklandAccess] Missing game members: " + string.Join(", ", missing));
            Speech.Say(Loc.T("compat_warning", string.Join(", ", missing)), interrupt: false);
        }
    }
}
