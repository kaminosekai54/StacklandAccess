using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace StacklandAccessNS
{
    /// <summary>
    /// Host of a Stacklands Together session: announces what the other players do (buy, open, sell, stack, craft).
    /// Together handles every request of a player in NetworkManager.ReceiveHost; while it runs, the player is known,
    /// and the game methods it calls (sell box, buy box, pack, release on a pile) are announced with that name.
    /// </summary>
    public static class RemoteActions
    {
        /// <summary>Player whose request the host is handling right now, or -1.</summary>
        public static int CurrentPlayer = -1;
        private static bool patched;

        public static bool Active => CurrentPlayer >= 0 && AccessController.AnnounceOtherPlayers && Together.IsHost;

        public static string CurrentName
        {
            get
            {
                var p = Together.Players().FirstOrDefault(x => x.id == CurrentPlayer);
                return string.IsNullOrWhiteSpace(p.name) ? Loc.T("mp_other_player") : p.name;
            }
        }

        /// <summary>Patches Together itself (only possible once its assembly is loaded).</summary>
        public static void TryPatch()
        {
            if (patched || !Together.Installed) return;
            patched = true;
            try
            {
                var harmony = StacklandAccess.Instance?.ModHarmony;
                var asm = Together.Assembly;
                if (harmony == null || asm == null) return;
                var net = asm.GetType("StacklandsTogetherNS.Networking.NetworkManager");
                var cards = asm.GetType("StacklandsTogetherNS.Synchronization.CardSynchronizer");
                var receive = net?.GetMethod("ReceiveHost", BindingFlags.NonPublic | BindingFlags.Instance);
                var release = cards?.GetMethod("ApplyRemoteRelease", BindingFlags.Public | BindingFlags.Instance);
                if (receive != null)
                    harmony.Patch(receive, prefix: new HarmonyMethod(typeof(RemoteActions), nameof(ReceivePrefix)),
                        finalizer: new HarmonyMethod(typeof(RemoteActions), nameof(ReceiveFinalizer)));
                if (release != null)
                    harmony.Patch(release, postfix: new HarmonyMethod(typeof(RemoteActions), nameof(ReleasePostfix)));
                Log.Info("Stacklands Together hooks: " + (receive != null) + ", " + (release != null));
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private static void ReceivePrefix(object __instance, object peer)
        {
            CurrentPlayer = -1;
            try
            {
                if (!(Together.Get(__instance, "peerPlayers") is Dictionary<int, int> map)) return;
                if (Together.Get(peer, "Id") is int peerId && map.TryGetValue(peerId, out int player)) CurrentPlayer = player;
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private static System.Exception ReceiveFinalizer(System.Exception __exception)
        {
            CurrentPlayer = -1;
            return __exception;
        }

        /// <summary>A player let go of cards: announce it when they went onto a pile, and follow the craft it starts.</summary>
        private static void ReleasePostfix(string uniqueId, string parentUniqueId)
        {
            try
            {
                if (!Active || string.IsNullOrEmpty(parentUniqueId) || parentUniqueId == "@sell") return;
                var card = WorldManager.instance?.AllCards.FirstOrDefault(c => c != null && !c.Destroyed && c.CardData != null && c.CardData.UniqueId == uniqueId);
                if (card == null || card.Parent == null || card.MyBoard == null || !card.MyBoard.IsCurrent) return;
                var moved = new List<GameCard>();
                for (var c = card; c != null; c = c.Child) moved.Add(c);
                var root = card.GetRootCard();
                string who = CurrentName;
                EventNarrator.Announce(Loc.T("mp_stacked", who, CardInfo.Summary(moved), CardInfo.Name(root)));
                EventNarrator.WatchRemoteDrop(root, who);
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }
    }

    /// <summary>Game methods called by Together while it handles another player's request on the host.</summary>
    [HarmonyPatch]
    public static class RemoteGamePatches
    {
        [HarmonyPatch(typeof(SellBox), nameof(SellBox.CardDropped))]
        [HarmonyPrefix]
        public static void Sell_Prefix(GameCard card)
        {
            try
            {
                if (!RemoteActions.Active || card == null) return;
                var sold = new List<GameCard>();
                for (var c = card; c != null; c = c.Child) sold.Add(c);
                EventNarrator.Announce(Loc.T("mp_sold", RemoteActions.CurrentName, CardInfo.Summary(sold)));
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private static int boughtBefore;

        [HarmonyPatch(typeof(BuyBoosterBox), nameof(BuyBoosterBox.CardDropped))]
        [HarmonyPrefix]
        public static void Buy_Prefix()
        {
            boughtBefore = WorldManager.instance != null ? WorldManager.instance.BoughtBoosterIds.Count : 0;
        }

        [HarmonyPatch(typeof(BuyBoosterBox), nameof(BuyBoosterBox.CardDropped))]
        [HarmonyPostfix]
        public static void Buy_Postfix(BuyBoosterBox __instance)
        {
            try
            {
                if (!RemoteActions.Active || __instance == null || WorldManager.instance == null) return;
                bool bought = WorldManager.instance.BoughtBoosterIds.Count > boughtBefore;
                EventNarrator.Announce(Loc.T(bought ? "mp_bought" : "mp_paid", RemoteActions.CurrentName, CardInfo.Name(__instance)));
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        [HarmonyPatch(typeof(Boosterpack), nameof(Boosterpack.Clicked))]
        [HarmonyPrefix]
        public static void Open_Prefix(Boosterpack __instance)
        {
            try
            {
                if (!RemoteActions.Active || __instance == null) return;
                EventNarrator.Announce(Loc.T("mp_opened", RemoteActions.CurrentName, CardInfo.Name(__instance)));
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }
    }
}
