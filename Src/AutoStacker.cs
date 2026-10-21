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

        /// <summary>Cards F5 may regroup: anything that piles up, except people, animals, buildings and places.</summary>
        private static bool Restackable(CardData cd)
        {
            if (cd == null) return false;
            if (cd is Harvestable || cd is Combatable || cd is FoodWarehouse || cd is Chest || cd is ResourceChest || cd is Blueprint || cd is Hotpot) return false;
            if (cd.HasInventory) return false;
            switch (cd.MyCardType)
            {
                case CardType.Resources:
                case CardType.Food:
                case CardType.Fish:
                case CardType.Equipable:
                    return true;
                default:
                    return false;
            }
        }

        private static HashSet<string> selfRecipes;

        /// <summary>Card ids that make something when stacked with themselves (left alone by F5).</summary>
        private static bool MakesSomethingWithItself(string id)
        {
            if (selfRecipes == null)
            {
                selfRecipes = new HashSet<string>();
                try
                {
                    foreach (var bp in WorldManager.instance.BlueprintPrefabs)
                    {
                        if (bp?.Subprints == null) continue;
                        foreach (var sp in bp.Subprints)
                        {
                            var req = sp?.RequiredCards;
                            if (req == null || req.Length < 2) continue;
                            var first = req[0].Split('|');
                            foreach (var candidate in first)
                            {
                                string c = candidate.Trim();
                                if (c != "" && req.All(r => r.Split('|').Select(x => x.Trim()).Contains(c))) selfRecipes.Add(c);
                            }
                        }
                    }
                }
                catch (System.Exception ex) { Log.Once(ex); }
            }
            return selfRecipes.Contains(id);
        }

        /// <summary>A card F5 may move: idle (its pile is not crafting, fighting or moving), not held.</summary>
        private static bool Free(GameCard c, GameCard held)
        {
            if (c == null || c.Destroyed || c.CardData == null || !c.FaceUp) return false;
            if (c.BeingDragged || c.IsEquipped || c.IsWorking || c == held) return false;
            var root = c.GetRootCard();
            if (root.BeingDragged || root.BounceTarget != null || root.Velocity.HasValue) return false;
            if (held != null && root.GetAllCardsInStack().Contains(held)) return false;
            if (Recipes.IsBusy(root) || root.GetCardInCombatInStack() != null) return false;
            return Restackable(c.CardData) && !MakesSomethingWithItself(c.CardData.Id);
        }

        /// <summary>
        /// F5: regroups every idle identical card into full piles (taken out of mixed piles too).
        /// Cards of a pile that is crafting are left alone, and so are cards that make something together.
        /// With a category, only the cards of that category. Returns how many cards moved.
        /// </summary>
        public static int RestackAll(BoardNavigator board, BoardNavigator.Category? only = null)
        {
            var wm = WorldManager.instance;
            if (wm == null) return 0;
            SyncWithSave();
            var held = board.IsHolding ? board.Held : null;
            int moved = 0;
            var cards = BoardNavigator.AllObjects().OfType<GameCard>()
                .SelectMany(r => r.GetAllCardsInStack())
                .Where(c => Free(c, held) && (only == null || only == BoardNavigator.Category.All || BoardNavigator.CategoryOf(c) == only))
                .ToList();
            foreach (var group in cards.GroupBy(c => c.CardData.Id))
            {
                var all = group.ToList();
                if (all.Count < 2) continue;
                // A pile made only of this card moves in one go; other cards are taken out one by one.
                bool Pure(GameCard root) => root.GetAllCardsInStack().All(x => x.CardData != null && x.CardData.Id == group.Key && all.Contains(x));
                var pureRoots = all.Select(c => c.GetRootCard()).Distinct().Where(Pure).OrderByDescending(r => r.GetChildCount()).ToList();
                var singles = all.Where(c => !pureRoots.Contains(c.GetRootCard())).ToList();
                foreach (var c in all) { string id = Id(c); if (id != null) manual.Remove(id); }

                GameCard target = pureRoots.Count > 0 ? pureRoots[0] : null;
                var pieces = pureRoots.Skip(1).ToList();
                foreach (var single in singles)
                {
                    if (target == null)
                    {
                        Mover.Extract(single);
                        target = single;
                        moved++;
                        continue;
                    }
                    Mover.Extract(single);
                    pieces.Add(single);
                }
                foreach (var piece in pieces)
                {
                    if (piece == null || piece.Destroyed || piece == target) continue;
                    int n = piece.GetAllCardsInStack().Count;
                    if (Mover.MoveChain(piece, target)) { moved += singles.Contains(piece) ? 1 : n; continue; }
                    // The target pile is full: this piece starts the next one.
                    target = piece;
                    if (singles.Contains(piece)) moved++;
                }
            }
            Save();
            BoardNavigator.InvalidateCache();
            return moved;
        }

        public static void Update(BoardNavigator board)
        {
            if (!AccessController.AutoStackEnabled) return;
            // In a multiplayer session other players move cards too: never restack behind their back.
            if (Together.InSession) return;
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
