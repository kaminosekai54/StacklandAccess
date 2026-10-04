using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Keeps the board tidy: idle piles of the same resource or food are merged into one pile.
    /// Piles built by the player are never touched (neither moved nor used as a target):
    /// new cards then go on the "source" pile, the one the mod manages.
    /// </summary>
    public static class AutoStacker
    {
        /// <summary>Unique ids (saved with the game) of the cards of piles built by the player.</summary>
        private static readonly HashSet<string> manual = new HashSet<string>();
        private static float timer;
        private const string SaveKey = "stacklandaccess_manual_piles";
        private static object loadedFrom;

        private static string Id(GameCard c) => c != null && c.CardData != null ? c.CardData.UniqueId : null;

        /// <summary>Remembers that the player built this stack by hand.</summary>
        public static void MarkManual(GameCard anyCardOfStack)
        {
            if (anyCardOfStack == null) return;
            SyncWithSave();
            foreach (var c in anyCardOfStack.GetRootCard().GetAllCardsInStack())
            {
                string id = Id(c);
                if (!string.IsNullOrEmpty(id)) manual.Add(id);
            }
            Save();
        }

        public static void Reset()
        {
            manual.Clear();
        }

        /// <summary>Reloads the list when another save or run was loaded.</summary>
        private static void SyncWithSave()
        {
            var wm = WorldManager.instance;
            if (wm == null || wm.RoundExtraKeyValues == null) return;
            if (ReferenceEquals(loadedFrom, wm.RoundExtraKeyValues)) return;
            loadedFrom = wm.RoundExtraKeyValues;
            manual.Clear();
            var kv = wm.RoundExtraKeyValues.Find(x => x != null && x.Key == SaveKey);
            if (kv != null && !string.IsNullOrEmpty(kv.Value))
                foreach (var id in kv.Value.Split(',')) if (id != "") manual.Add(id);
        }

        private static void Save()
        {
            var wm = WorldManager.instance;
            if (wm == null || wm.RoundExtraKeyValues == null) return;
            var list = wm.RoundExtraKeyValues;
            var kv = list.Find(x => x != null && x.Key == SaveKey);
            if (kv == null) { kv = new SerializedKeyValuePair { Key = SaveKey }; list.Add(kv); }
            kv.Value = string.Join(",", manual);
        }

        private static bool Eligible(CardData cd)
        {
            if (cd == null) return false;
            if (cd is Harvestable || cd is Combatable || cd is FoodWarehouse || cd is Chest || cd is ResourceChest || cd is Blueprint) return false;
            if (cd.HasInventory) return false;
            if (!(cd is Resource || cd is Food)) return false;
            return cd.MyCardType == CardType.Resources || cd.MyCardType == CardType.Food || cd.MyCardType == CardType.Fish;
        }

        /// <summary>A pile the mod may manage: same card everywhere, idle, not built by the player.</summary>
        private static bool IsAutoPile(GameCard root, GameCard held)
        {
            if (root == null || root.Destroyed || root.Parent != null || !root.FaceUp) return false;
            if (root.BeingDragged || root.IsEquipped || root.IsWorking || root.BounceTarget != null || root.Velocity.HasValue) return false;
            if (root.GetCardWithStatusInStack() != null || root.TimerRunningInStack) return false;
            if (root.GetCardInCombatInStack() != null) return false;
            var stack = root.GetAllCardsInStack();
            string id = root.CardData.Id;
            foreach (var c in stack)
            {
                if (c == null || c.Destroyed || c.CardData == null || c.CardData.Id != id || !c.FaceUp) return false;
                if (manual.Contains(Id(c) ?? "")) return false;
                if (held != null && c == held) return false;
            }
            return Eligible(root.CardData);
        }

        /// <summary>
        /// F5: regroups every idle resource and food of the same kind into full piles (30 cards),
        /// manual piles included (they become managed piles again). Returns how many cards moved.
        /// </summary>
        public static int RestackAll(BoardNavigator board)
        {
            var wm = WorldManager.instance;
            if (wm == null) return 0;
            SyncWithSave();
            var held = board.IsHolding ? board.Held : null;
            int moved = 0;
            var roots = BoardNavigator.AllObjects().OfType<GameCard>().Where(r =>
            {
                if (r == null || r.Destroyed || r.Parent != null || !r.FaceUp || r.BeingDragged || r.Velocity.HasValue) return false;
                if (r.GetCardWithStatusInStack() != null || r.TimerRunningInStack || r.GetCardInCombatInStack() != null) return false;
                var stack = r.GetAllCardsInStack();
                string id = r.CardData.Id;
                return Eligible(r.CardData) && stack.All(c => c != null && c.CardData != null && c.CardData.Id == id && c != held && c.FaceUp);
            }).ToList();
            foreach (var group in roots.GroupBy(r => r.CardData.Id))
            {
                // Biggest piles receive the smaller ones.
                var piles = group.OrderByDescending(r => r.GetChildCount()).ToList();
                foreach (var c in piles.SelectMany(r => r.GetAllCardsInStack())) { string id = Id(c); if (id != null) manual.Remove(id); }
                int t = 0;
                for (int i = 1; i < piles.Count; i++)
                {
                    var other = piles[i];
                    while (t < i)
                    {
                        var leaf = piles[t].GetLeafCard();
                        if (leaf.CardData.CanHaveCardOnTop(other.CardData))
                        {
                            moved += other.GetChildCount() + 1;
                            other.SetParent(leaf);
                            other.StackUpdate = true;
                            break;
                        }
                        t++;
                    }
                    if (t >= i) t = i; // this pile could not go anywhere: it becomes a target itself
                }
            }
            Save();
            BoardNavigator.InvalidateCache();
            return moved;
        }

        public static void Update(BoardNavigator board)
        {
            if (!AccessController.AutoStackEnabled) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 1f;
            var wm = WorldManager.instance;
            if (wm == null || !wm.CanInteract || wm.InAnimation || wm.DraggingDraggable != null) return;
            if (!(GameCanvas.instance.CurrentScreen is GameScreen)) return;

            SyncWithSave();
            // Forget cards that no longer exist (on any board: cards on other islands are kept).
            var alive = new HashSet<string>(wm.AllCards.Where(c => c != null && !c.Destroyed).Select(Id).Where(x => !string.IsNullOrEmpty(x)));
            if (manual.RemoveWhere(id => !alive.Contains(id)) > 0) Save();

            var held = board.IsHolding ? board.Held : null;
            var piles = BoardNavigator.AllObjects().OfType<GameCard>().Where(r => IsAutoPile(r, held)).ToList();
            foreach (var group in piles.GroupBy(r => r.CardData.Id))
            {
                var list = group.OrderByDescending(r => r.GetChildCount()).ToList();
                if (list.Count < 2) continue;
                var target = list[0];
                foreach (var other in list.Skip(1))
                {
                    var leaf = target.GetLeafCard();
                    if (!leaf.CardData.CanHaveCardOnTop(other.CardData)) break; // pile full (30 cards)
                    other.SetParent(leaf);
                    other.StackUpdate = true;
                    BoardNavigator.InvalidateCache();
                }
                target.StackUpdate = true;
            }
        }
    }
}
