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
        private static List<GameCard> Inputs(GameCard building) => Inputs(building, false);

        private static List<GameCard> Inputs(GameCard building, bool people)
        {
            var list = new List<GameCard>();
            foreach (var root in BoardNavigator.AllObjects().OfType<GameCard>())
            {
                if (root == building.GetRootCard()) continue;
                if (Recipes.IsBusy(root) || root.GetCardInCombatInStack() != null) continue;
                foreach (var c in root.GetAllCardsInStack())
                {
                    if (c == null || c.Destroyed || !c.FaceUp || c.IsEquipped || c.IsWorking) continue;
                    if ((!people && c.CardData.MyCardType == CardType.Humans) || c.CardData is Blueprint) continue;
                    if (!c.CanBeDragged() || c.BeingDragged) continue;
                    // An empty coin chest gives nothing to the building that takes coins.
                    if (c.CardData is Chest chest && chest.CoinCount <= 0) continue;
                    if (Accepts(building.CardData, c.CardData)) list.Add(c);
                }
            }
            // Loose cards first, then the top of piles (taking them does not break the pile under them).
            return list.OrderBy(c => c.Parent == null && c.Child == null ? 0 : c.Child == null ? 1 : 2).ToList();
        }

        /// <summary>One thing a building can make now.</summary>
        private class Option
        {
            public string Title;
            public System.Action Run;
            public System.Func<string> Info;
        }

        /// <summary>
        /// The production page: one line per building that can make something right now with the cards of the board.
        /// Enter makes it; with several choices (a stove: omelette, quiche...), Enter opens the list of choices.
        /// </summary>
        public static List<ListBrowser.Entry> Entries(BoardNavigator board)
        {
            var options = new Dictionary<GameCard, List<Option>>();
            var order = new List<GameCard>();
            void Add(GameCard building, Option o)
            {
                if (!options.TryGetValue(building, out var l)) { options[building] = l = new List<Option>(); order.Add(building); }
                if (l.Any(x => x.Title == o.Title)) return;
                l.Add(o);
            }

            foreach (var card in BoardNavigator.AllObjects().OfType<GameCard>().SelectMany(r => r.GetAllCardsInStack()))
            {
                if (card == null || card.Destroyed || card.CardData == null) continue;
                var kind = Kinds.FirstOrDefault(k => k.Type.IsInstanceOfType(card.CardData));
                if (kind == null) continue;
                var building = card;
                var inputs = Inputs(building);
                // Only buildings that can really produce now.
                if (inputs.Count < kind.PerCycle) continue;
                string output = kind.Output != null ? Name(kind.Output) : (building.CardData is Farmland f && !string.IsNullOrEmpty(f.HarvestCardId) ? Name(f.HarvestCardId) : "");
                string inputNames = string.Join(", ", inputs.Select(c => CardInfo.Name(c)).Distinct());
                Add(building, new Option
                {
                    Title = TextCleaner.Join(Loc.T("prod_rule", kind.PerCycle, output), Loc.T("prod_available", inputs.Count, inputNames)),
                    Run = () => Feed(building, kind, board),
                    Info = () => CardInfo.Details(building),
                });
            }
            foreach (var (building, o) in OtherOptions(board)) Add(building, o);
            foreach (var (building, o) in SingleUseOptions(board)) Add(building, o);
            foreach (var bp in Recipes.BuildingPlans())
            {
                var plan = bp.Plan;
                var b = bp.Blueprint;
                string idea = TextCleaner.Clean(b.KnowledgeName);
                string result = Recipes.ResultName(b, plan.Subprint);
                string title = TextCleaner.Join(result == idea ? result : idea + " → " + result, Recipes.Ingredients(plan.Subprint), Recipes.Duration(plan.Subprint));
                Add(bp.Building, new Option { Title = title, Run = () => Recipes.Build(plan, board), Info = () => Recipes.Info(b) });
            }

            var cmp = System.StringComparer.CurrentCultureIgnoreCase;
            var entries = new List<ListBrowser.Entry>();
            var log = new System.Text.StringBuilder("[StacklandAccess] Production page:");
            foreach (var building in order.OrderBy(b => CardInfo.Name(b), cmp))
            {
                var list = options[building].OrderBy(o => o.Title, cmp).ToList();
                var b = building;
                string name = CardInfo.Name(b);
                string status = CardInfo.StackStatus(b.GetRootCard());
                log.Append("\n  ").Append(name).Append(" (").Append(b.CardData.GetType().Name).Append("): ").Append(string.Join(" | ", list.Select(o => o.Title)));
                if (list.Count == 1)
                {
                    var o = list[0];
                    entries.Add(new ListBrowser.Entry(TextCleaner.Join(name + " : " + o.Title, status)) { OnActivate = o.Run, Info = o.Info });
                    continue;
                }
                string choices = string.Join(", ", list.Select(o => FirstPart(o.Title)));
                entries.Add(new ListBrowser.Entry(TextCleaner.Join(name + " : " + Loc.N("prod_choices", list.Count, list.Count), choices, status))
                {
                    KeepOpen = true,
                    OnActivate = () => OpenChoices(board, b, list),
                    Info = () => CardInfo.Details(b),
                });
            }
            Debug.Log(log.ToString());
            return entries;
        }

        private static string FirstPart(string title)
        {
            int i = title.IndexOf(". ", System.StringComparison.Ordinal);
            return i > 0 ? title.Substring(0, i) : title;
        }

        /// <summary>The choices of one building; Escape goes back to the production page.</summary>
        private static void OpenChoices(BoardNavigator board, GameCard building, List<Option> list)
        {
            var browser = board.List;
            if (browser == null) return;
            int line = browser.Index;
            var entries = list.Select(o => new ListBrowser.Entry(o.Title) { OnActivate = o.Run, Info = o.Info }).ToList();
            browser.OpenPagesAt(new List<ListBrowser.Page> { new ListBrowser.Page(Loc.T("prod_choose", CardInfo.Name(building)), entries) }, 0, 0,
                speak: true, toggleAction: "recipes",
                onBack: () => browser.OpenPagesAt(Recipes.Pages(board), Recipes.ProductionPage, line, speak: true, toggleAction: "recipes"));
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

        /// <summary>Gardens, farms and greenhouses (one choice per plant available), fish traps, animal pens, breeding pens, hotpots.</summary>
        private static List<(GameCard, Option)> OtherOptions(BoardNavigator board)
        {
            var result = new List<(GameCard, Option)>();
            foreach (var card in BoardNavigator.AllObjects().OfType<GameCard>().SelectMany(r => r.GetAllCardsInStack()))
            {
                if (card == null || card.Destroyed || card.CardData == null) continue;
                var d = card.CardData;
                bool grow = IsGrowBuilding(d);
                int amount = FeedAmount(d);
                if (!grow && amount < 0) continue;
                var building = card;
                var inputs = Inputs(building);
                if (grow)
                {
                    foreach (var sp in GrowthPrints(d.Id))
                    {
                        string seed = sp.RequiredCards[0];
                        var seeds = inputs.Where(c => c.CardData.Id == seed).ToList();
                        if (seeds.Count == 0) continue;
                        var extra = sp.ExtraResultCards ?? new string[0];
                        string res = extra.Length == 0 ? "" : (extra.Length > 1 ? extra.Length + " " : "") + Name(extra[0]);
                        result.Add((building, new Option
                        {
                            Title = TextCleaner.Join(Loc.T("prod_plant_short", Name(seed), res, CardInfo.Seconds(sp.Time)), Loc.T("prod_count", seeds.Count)),
                            Run = () => Put(building, seed, 1, board),
                            Info = () => CardInfo.Details(building),
                        }));
                    }
                    continue;
                }
                // Animal and breeding pens: animals only (the pen also takes wheat, eggs or soil, which make nothing there).
                if (d is AnimalPen || d is BreedingPen) inputs = inputs.Where(c => c.CardData is Animal).ToList();
                foreach (var group in inputs.GroupBy(c => c.CardData.Id))
                {
                    if (amount > 1 && group.Count() < amount) continue;
                    string id = group.Key;
                    result.Add((building, new Option
                    {
                        Title = TextCleaner.Join(Loc.T("prod_put_short", CardInfo.Name(group.First())), Loc.T("prod_count", group.Count())),
                        Run = () => Put(building, id, amount, board),
                        Info = () => CardInfo.Details(building),
                    }));
                }
            }
            return result;
        }

        /// <summary>
        /// Buildings that turn the cards put on them into something by their own code (not by an idea recipe).
        /// Other buildings only accept cards for their recipes (campfire, oven, well...), which come from the
        /// recipe list, or to house or staff them, which produces nothing.
        /// </summary>
        private static readonly HashSet<string> CodeProcessors = new HashSet<string>
        {
            "University", "Academy", "Temple", "Charity", "WishingWell", "Cathedral", "Tavern", "IndustrialSmelter",
            "SlaughterHouse", "ExportCenter", "Royal", "Shaman", "IndustrialRevolution", "NamingStone", "Altar",
            "Merchant", "TimeMachine",
        };

        private static bool HasOwnRule(CardData d)
        {
            if (d.Id == "laboratory") return true;
            for (var t = d.GetType(); t != null && t != typeof(CardData); t = t.BaseType)
                if (CodeProcessors.Contains(t.Name)) return true;
            return false;
        }

        /// <summary>True when an idea recipe uses this building with this card (then the recipe list offers it, with all its cards).</summary>
        private static readonly Dictionary<string, bool> inRecipe = new Dictionary<string, bool>();

        private static bool InRecipe(string buildingId, string cardId)
        {
            string key = buildingId + "\n" + cardId;
            if (!inRecipe.TryGetValue(key, out bool r)) inRecipe[key] = r = FindInRecipe(buildingId, cardId);
            return r;
        }

        private static bool FindInRecipe(string buildingId, string cardId)
        {
            var wm = WorldManager.instance;
            if (wm == null) return false;
            foreach (var bp in wm.BlueprintPrefabs)
            {
                if (bp?.Subprints == null) continue;
                foreach (var sp in bp.Subprints)
                {
                    if (sp?.RequiredCards == null) continue;
                    if (sp.RequiredCards.Any(r => r != null && Recipes.AllowedIds(sp, r).Contains(buildingId))
                        && sp.RequiredCards.Any(r => r != null && Recipes.AllowedIds(sp, r).Contains(cardId))) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Buildings that work with their own rule (university, temple, altar...): one choice per kind of card
        /// they turn into something (people one at a time, other cards as many as the building takes).
        /// </summary>
        private static List<(GameCard, Option)> SingleUseOptions(BoardNavigator board)
        {
            var result = new List<(GameCard, Option)>();
            foreach (var card in BoardNavigator.AllObjects().OfType<GameCard>().SelectMany(r => r.GetAllCardsInStack()))
            {
                if (card == null || card.Destroyed || card.CardData == null) continue;
                var d = card.CardData;
                if (BoardNavigator.CategoryOf(card) != BoardNavigator.Category.Buildings) continue;
                if (Kinds.Any(k => k.Type.IsInstanceOfType(d)) || IsGrowBuilding(d) || FeedAmount(d) >= 0) continue;
                if (!HasOwnRule(d)) continue;
                var building = card;
                // Workers only staff a building; cards of an idea recipe come from the recipes.
                var inputs = Inputs(building, true).Where(c => !(c.CardData is Worker) && !InRecipe(d.Id, c.CardData.Id)).ToList();
                foreach (var group in inputs.GroupBy(c => c.CardData.Id))
                {
                    string id = group.Key;
                    int amount = group.First().CardData.MyCardType == CardType.Humans ? 1 : 0;
                    result.Add((building, new Option
                    {
                        Title = TextCleaner.Join(Loc.T("prod_put_short", CardInfo.Name(group.First())), Loc.T("prod_count", group.Count())),
                        Run = () => Put(building, id, amount, board, true),
                        Info = () => CardInfo.Details(building),
                    }));
                }
            }
            return result;
        }

        /// <summary>Puts cards of one kind on the building: a given number, or as many as it accepts (amount 0).</summary>
        private static void Put(GameCard building, string id, int amount, BoardNavigator board, bool people = false)
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
            var cards = Inputs(building, people).Where(c => c.CardData.Id == id).ToList();
            int max = amount > 0 ? amount : 30;
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
