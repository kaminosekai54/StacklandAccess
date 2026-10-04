using HarmonyLib;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>Harmony patches: key capture and hooks on game events.</summary>
    [HarmonyPatch]
    public static class Patches
    {
        /// <summary>Escape is used by the mod (cancel a held card, close a list): hide it from the game then.</summary>
        [HarmonyPatch(typeof(InputController), nameof(InputController.CancelTriggered))]
        [HarmonyPrefix]
        public static bool CancelTriggered_Prefix(ref bool __result)
        {
            if (AccessController.CapturesEscape && Keys.Down(Key.Escape))
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(InputController), nameof(InputController.PauseTriggered))]
        [HarmonyPrefix]
        public static bool PauseTriggered_Prefix(ref bool __result)
        {
            if (AccessController.CapturesEscape && Keys.Down(Key.Escape))
            {
                __result = false;
                return false;
            }
            return true;
        }

        /// <summary>Enter belongs to the mod: never let the game treat it as "submit" (avoids double activation).</summary>
        [HarmonyPatch(typeof(InputController), nameof(InputController.SubmitTriggered))]
        [HarmonyPrefix]
        public static bool SubmitTriggered_Prefix(ref bool __result)
        {
            if (Keys.Down(Key.Enter) || Keys.Down(Key.NumpadEnter))
            {
                __result = false;
                return false;
            }
            return true;
        }

        /// <summary>Combat follow-up: every hit or miss.</summary>
        [HarmonyPatch(typeof(Combatable), nameof(Combatable.PerformAttack))]
        [HarmonyPostfix]
        public static void PerformAttack_Postfix(Combatable __instance, Combatable target)
        {
            try { EventNarrator.OnAttack(__instance, target); } catch (System.Exception ex) { Log.Once(ex); }
        }

        /// <summary>Notifications (quest completed...) are spoken and kept in the history.</summary>
        [HarmonyPatch(typeof(GameScreen), nameof(GameScreen.AddNotification))]
        [HarmonyPostfix]
        public static void AddNotification_Postfix(string title, string text)
        {
            EventNarrator.Announce(Loc.T("notif", title, text), important: true);
        }

        /// <summary>Make the button focused by the mod count as hovered (cardopedia preview, tooltips, highlight).</summary>
        [HarmonyPatch(typeof(GameCanvas), "UpdateCustomButtonHovered")]
        [HarmonyPostfix]
        public static void UpdateCustomButtonHovered_Postfix(GameCanvas __instance)
        {
            var b = MenuNavigator.FocusedButton;
            if (b == null || !b.gameObject.activeInHierarchy) return;
            b.IsHovered = true;
            Traverse.Create(__instance).Field("previousHovered").SetValue(b);
        }
    }
}
