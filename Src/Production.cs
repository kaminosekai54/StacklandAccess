using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Production buildings that are not ideas (sawmill, brickyard, composter, farmland):
    /// they work when their input cards are put on them. The L list can feed them.
    /// </summary>
    public static class Production
    {
        private class Kind
        {
            public System.Type Type;
            public int PerCycle;
            public string Output;
        }

        private static readonly List<Kind> Kinds = new List<Kind>
        {
            new Kind { Type = typeof(Sawmill), PerCycle = 2, Output = "plank" },
            new Kind { Type = typeof(Brickyard), PerCycle = 2, Output = "brick" },
            new Kind { Type = typeof(Composter), PerCycle = 5, Output = "soil" },
            new Kind { Type = typeof(Farmland), PerCycle = 1, Output = null },
        };

        private const int MaxFeed = 20;

        private static bool Accepts(CardData building, CardData input)
        {
            try { return Traverse.Create(building).Method("CanHaveCard", input).GetValue<bool>(); }
            catch (System.Exception ex) { Log.Once(ex); return false; }
        }

        private static string Name(string id)
        {
            try { var p = WorldManager.instance.GetCardPrefab(id); if (p != null) return TextCleaner.Clean(p.Name); }
            catch (System.Exception ex) { Log.Once(ex); }
            return id;
        }

        /// <summary>Idle cards that the building accepts (not used by another craft, not already on it).</summary>
        private static List<GameCard> Inputs(GameCard building)
        {
            var list = new List<GameCard>();
            foreach (var root in BoardNavigator.AllObjects().OfType<GameCard>())
            {
                if (root == building.GetRootCard()) continue;
                if (Recipes.IsBusy(root) || root.GetCardInCombatInStack() != null) continue;
                foreach (var c in root.GetAllCardsInStack())
                {
                    if (c == null || c.Destroyed || !c.FaceUp || c.IsEquipped || c.IsWorking) continue;
                    if (c.CardData.MyCardType == CardType.Humans || c.CardData is Blueprint) continue;
                    if (!c.CanBeDragged() || c.BeingDragged) continue;
                    if (Accepts(building.CardData, c.CardData)) list.Add(c);
                }
            }
            // Loose cards first, then the top of piles (taking them does not break the pile under them).
            return list.OrderBy(c => c.Parent == null && c.Child == null ? 0 : c.Child == null ? 1 : 2).ToList();
        }

        public static List<ListBrowser.Entry> Entries(BoardNavigator board)
        {
            var entries = new List<ListBrowser.Entry>();
            foreach (var card in BoardNavigator.AllObjects().OfType<GameCard>().SelectMany(r => r.GetAllCardsInStack()))
            {
                if (card == null || card.Destroyed || card.CardData == null) continue;
                var kind = Kinds.FirstOrDefault(k => k.Type.IsInstanceOfType(card.CardData));
                if (kind == null) continue;
                var building = card;
                var inputs = Inputs(building);
                string output = kind.Output != null ? Name(kind.Output) : (building.CardData is Farmland f && !string.IsNullOrEmpty(f.HarvestCardId) ? Name(f.HarvestCardId) : "");
                string inputNames = string.Join(", ", inputs.Select(c => CardInfo.Name(c)).Distinct());
                string status = CardInfo.StackStatus(building.GetRootCard());
                string title = TextCleaner.Join(
                    CardInfo.Name(building),
                    Loc.T("prod_rule", kind.PerCycle, output),
                    inputs.Count > 0 ? Loc.T("prod_available", inputs.Count, inputNames) : Loc.T("prod_none"),
                    status);
                entries.Add(new ListBrowser.Entry(title)
                {
                    OnActivate = () => Feed(building, kind, board),
                    Info = () => CardInfo.Details(building),
                });
            }
            return entries;
        }

        /// <summary>Puts the available input cards on the building (as many as useful, at most 20).</summary>
        private static void Feed(GameCard building, Kind kind, BoardNavigator board)
        {
            var wm = WorldManager.instance;
            if (wm == null || !wm.CanInteract || building == null || building.Destroyed) { Speech.Say(Loc.T("not_now")); return; }
            BoardNavigator.FixStuckDrag();
            BoardNavigator.InvalidateCache();
            var inputs = Inputs(building);
            if (inputs.Count < kind.PerCycle)
            {
                Speech.Say(Loc.T("prod_not_enough", CardInfo.Name(building), kind.PerCycle));
                return;
            }
            int count = Mathf.Min(MaxFeed, inputs.Count - inputs.Count % kind.PerCycle);
            var given = new List<string>();
            foreach (var c in inputs.Take(count))
            {
                var leaf = building.GetRootCard().GetLeafCard();
                if (!leaf.CardData.CanHaveCardOnTop(c.CardData)) break;
                Mover.Extract(c);
                if (!Mover.MoveChain(c, building.GetRootCard())) break;
                given.Add(CardInfo.Name(c));
            }
            building.GetRootCard().StackUpdate = true;
            AutoStacker.MarkManual(building);
            BoardNavigator.LastDropRoot = building.GetRootCard();
            BoardNavigator.LastDropTime = Time.unscaledTime;
            board.FocusStack(building);
            if (given.Count == 0) Speech.Say(Loc.T("cant_drop", "", CardInfo.Name(building)));
            else Speech.Say(Loc.T("prod_fed", CardInfo.Name(building), CardInfo.Summary(inputs.Take(given.Count).ToList())));
        }
    }
}
