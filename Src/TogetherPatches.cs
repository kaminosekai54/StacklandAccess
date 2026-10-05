using System.Reflection;
using HarmonyLib;

namespace StacklandAccessNS
{
    /// <summary>
    /// On a Stacklands Together client, combat runs on the host: PerformAttack never runs here,
    /// but the host replays every hit text. Hits are announced from there instead.
    /// </summary>
    [HarmonyPatch]
    public static class ClientHitTextPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Combatable), "ShowHitText");

        private static void Postfix(Combatable origin, Combatable effectTarget, bool isHit, int damage)
        {
            try
            {
                if (!Together.IsClient) return;
                EventNarrator.OnRemoteAttack(origin, effectTarget, isHit, damage);
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }
    }
}
