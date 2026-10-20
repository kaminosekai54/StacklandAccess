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
            entries.AddRange(OtherEntries(board));
            return entries;
        }

        // ------------------------------------------------------------------ growing and feeding buildings

        private static readonly string[] GrowBuildings = { "garden", "farm", "greenhouse" };

        /// <summary>How many cards of one kind go on the building per use (0 = as many as it takes).</summary>
        private static int FeedAmount(CardData building)
        {
            if (building is FishTrap) return 1;
            if (building is BreedingPen) return 2;
            if (building is AnimalPen || building is Hotpot) return 0;
            return -1; // not a feeding building
        }

        private static bool IsGrowBuilding(CardData d) => d is Garden || d is Greenhouse || GrowBuildings.Contains(d.Id);

        /// <summary>Growth recipes of the game (seed + garden, farm or greenhouse), by building id.</summary>
        private static List<Subprint> GrowthPrints(string buildingId)
        {
            var list = new List<Subprint>();
            var wm = WorldManager.instance;
            if (wm == null) return list;
            foreach (var bp in wm.BlueprintPrefabs.OfType<BlueprintGrowth>())
            {
                if (bp.Subprints == null) continue;
                list.AddRange(bp.Subprints.Where(sp => sp != null && sp.RequiredCards != null && sp.RequiredCards.Length == 2 && sp.RequiredCards[1] == buildingId));
            }
            return list;
        }

        /// <summary>Gardens, farms and greenhouses (one line per plant available), fish traps, animal pens, breeding pens, hotpots.</summary>
        private static List<ListBrowser.Entry> OtherEntries(BoardNavigator board)
        {
            var entries = new List<ListBrowser.Entry>();
            foreach (var card in BoardNavigator.AllObjects().OfType<GameCard>().SelectMany(r => r.GetAllCardsInStack()))
            {
                if (card == null || card.Destroyed || card.CardData == null) continue;
                var d = card.CardData;
                bool grow = IsGrowBuilding(d);
                int amount = FeedAmount(d);
                if (!grow && amount < 0) continue;
                var building = card;
                string status = CardInfo.StackStatus(building.GetRootCard());
                var inputs = Inputs(building);
                int before = entries.Count;
                if (grow)
                {
                    foreach (var sp in GrowthPrints(d.Id))
                    {
                        string seed = sp.RequiredCards[0];
                        var seeds = inputs.Where(c => c.CardData.Id == seed).ToList();
                        if (seeds.Count == 0) continue;
                        var extra = sp.ExtraResultCards ?? new string[0];
                        string result = extra.Length == 0 ? "" : (extra.Length > 1 ? extra.Length + " " : "") + Name(extra[0]);
                        string title = TextCleaner.Join(Loc.T("prod_plant", CardInfo.Name(building), Name(seed), result, CardInfo.Seconds(sp.Time)),
                            Loc.T("prod_count", seeds.Count), status);
                        entries.Add(new ListBrowser.Entry(title)
                        {
                            OnActivate = () => Put(building, seed, 1, board),
                            Info = () => CardInfo.Details(building),
                        });
                    }
                }
                else
                {
                    foreach (var group in inputs.GroupBy(c => c.CardData.Id))
                    {
                        if (amount > 1 && group.Count() < amount) continue;
                        string id = group.Key;
                        string title = TextCleaner.Join(Loc.T("prod_put", CardInfo.Name(building), CardInfo.Name(group.First())),
                            Loc.T("prod_count", group.Count()), status);
                        entries.Add(new ListBrowser.Entry(title)
                        {
                            OnActivate = () => Put(building, id, amount, board),
                            Info = () => CardInfo.Details(building),
                        });
                    }
                }
                if (entries.Count == before)
                {
                    entries.Add(new ListBrowser.Entry(TextCleaner.Join(CardInfo.Name(building), Loc.T("prod_none"), status))
                    {
                        Info = () => CardInfo.Details(building),
                    });
                }
            }
            return entries;
        }

        /// <summary>Puts cards of one kind on the building: a given number, or as many as it accepts (amount 0).</summary>
        private static void Put(GameCard building, string id, int amount, BoardNavigator board)
        {
            var wm = WorldManager.instance;
            if (wm == null || !wm.CanInteract || building == null || building.Destroyed) { Speech.Say(Loc.T("not_now")); return; }
            BoardNavigator.FixStuckDrag();
            BoardNavigator.InvalidateCache();
            var root = building.GetRootCard();
            var withStatus = root.GetCardWithStatusInStack();
            if (withStatus != null && !withStatus.CardData.CanHaveCardsWhileHasStatus())
            {
                Speech.Say(Loc.T("target_busy", CardInfo.Name(building)));
                return;
            }
            var cards = Inputs(building).Where(c => c.CardData.Id == id).ToList();
            int max = amount > 0 ? amount : MaxFeed;
            if (cards.Count == 0 || (amount > 1 && cards.Count < amount))
            {
                Speech.Say(Loc.T("prod_not_enough", CardInfo.Name(building), Mathf.Max(1, amount)));
                return;
            }
            var given = new List<GameCard>();
            foreach (var c in cards.Take(max))
            {
                Mover.Extract(c);
                if (!Mover.MoveChain(c, root)) break;
                given.Add(c);
            }
            root.StackUpdate = true;
            AutoStacker.MarkManual(building);
            BoardNavigator.LastDropRoot = root;
            BoardNavigator.LastDropTime = Time.unscaledTime;
            board.FocusStack(building);
            if (given.Count == 0) Speech.Say(Loc.T("cant_drop", CardInfo.Name(cards[0]), CardInfo.Name(building)));
            else Speech.Say(Loc.T("prod_fed", CardInfo.Name(building), CardInfo.Summary(given)));
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
