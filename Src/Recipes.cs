using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Finds which known ideas can be made with the cards on the board, and builds the stack for one of them.
    /// </summary>
    public static class Recipes
    {
        public class Plan
        {
            public Blueprint Blueprint;
            public Subprint Subprint;
            public List<GameCard> Cards = new List<GameCard>();
            public List<string> Missing = new List<string>();
            public bool Possible => Missing.Count == 0;
            public bool AlreadyInPlace;
        }

        private static MethodInfo parseCardId;

        private static string[] AllowedIds(Subprint sp, string requirement)
        {
            try
            {
                if (parseCardId == null) parseCardId = AccessTools.Method(typeof(Subprint), "ParseCardId");
                string parsed = (string)parseCardId.Invoke(sp, new object[] { requirement });
                return CardStringSplitter.me.Split(parsed);
            }
            catch
            {
                return new[] { requirement };
            }
        }

        /// <summary>Readable name of a requirement: "any_villager" is a villager, not the first villager-like card (a dog).</summary>
        private static string RequirementName(string req, string[] allowed)
        {
            switch (req)
            {
                case "any_villager":
                case "any_villager_young":
                case "any_villager_old":
                case "breedable_villager":
                    return PrefabName("villager");
                case "any_worker": return PrefabName("worker");
                case "any_educated_worker": return PrefabName("educated_worker");
                case "fish": return Loc.T("any_fish");
            }
            if (allowed.Contains(req)) return PrefabName(req);
            return PrefabName(allowed.FirstOrDefault() ?? req);
        }

        private static string PrefabName(string id)
        {
            try
            {
                var prefab = WorldManager.instance.GetCardPrefab(id);
                if (prefab != null) return TextCleaner.Clean(prefab.Name);
            }
            catch (System.Exception ex) { Log.Once(ex); }
            return id;
        }

        /// <summary>Cards that can be used for a recipe.</summary>
        private static List<GameCard> Pool()
        {
            var pool = new List<GameCard>();
            foreach (var root in BoardNavigator.AllObjects().OfType<GameCard>())
            {
                if (root.GetCardInCombatInStack() != null) continue;
                // Cards already used by another craft (a timer runs on their pile) are left alone.
                if (IsBusy(root)) continue;
                foreach (var c in root.GetAllCardsInStack())
                {
                    if (c == null || c.Destroyed || !c.FaceUp || c.IsEquipped || c.IsWorking || c.BeingDragged) continue;
                    if (c.CardData is Blueprint) continue;
                    pool.Add(c);
                }
            }
            return pool;
        }

        public static bool IsBusy(GameCard root)
        {
            try { return root.GetCardWithStatusInStack() != null || root.TimerRunningInStack; }
            catch (System.Exception ex) { Log.Once(ex); return false; }
        }

        /// <summary>Lower is better: free cards first, cards of busy stacks last.</summary>
        private static int Cost(GameCard c)
        {
            int cost = 0;
            if (c.Parent != null || c.Child != null) cost += 1;
            if (c.GetRootCard().GetCardWithStatusInStack() != null) cost += 4;
            if (!c.CanBeDragged()) cost += 2;
            return cost;
        }

        private static Plan Assign(Blueprint bp, Subprint sp, List<GameCard> pool)
        {
            var plan = new Plan { Blueprint = bp, Subprint = sp };
            var used = new HashSet<GameCard>();
            foreach (var req in sp.RequiredCards ?? new string[0])
            {
                var allowed = AllowedIds(sp, req);
                var pick = pool.Where(c => !used.Contains(c) && allowed.Contains(c.CardData.Id)).OrderBy(Cost).FirstOrDefault();
                if (pick == null)
                {
                    string missing = RequirementName(req, allowed);
                    // The card exists but is busy with another craft: say so.
                    bool busy = BoardNavigator.AllObjects().OfType<GameCard>().SelectMany(r => r.GetAllCardsInStack())
                        .Any(c => c != null && !c.Destroyed && c.CardData != null && allowed.Contains(c.CardData.Id) && !used.Contains(c));
                    plan.Missing.Add(busy ? Loc.T("missing_busy", missing) : missing);
                }
                else { used.Add(pick); plan.Cards.Add(pick); }
            }
            if (plan.Possible && plan.Cards.Count > 0)
            {
                var root = plan.Cards[0].GetRootCard();
                var stack = root.GetAllCardsInStack();
                if (plan.Cards.All(c => c.GetRootCard() == root) && stack.Count == plan.Cards.Count) plan.AlreadyInPlace = true;
            }
            return plan;
        }

        /// <summary>Best plan for a blueprint: the first subprint that can be made, else the one missing the fewest cards.</summary>
        public static Plan Analyze(Blueprint bp, List<GameCard> pool)
        {
            Plan best = null;
            if (bp.Subprints == null) return null;
            // Same rules as the game (CardData): the idea may be blocked right now.
            bool blocked = false;
            try { blocked = !bp.CanCurrentlyBeMade; } catch (System.Exception ex) { Log.Once(ex); }
            foreach (var sp in bp.Subprints)
            {
                if (sp == null || sp.RequiredCards == null || sp.RequiredCards.Length == 0) continue;
                var plan = Assign(bp, sp, pool);
                if (blocked) plan.Missing.Add(Loc.T("recipe_blocked"));
                else if (bp.HasMaxAmountOnBoard && !string.IsNullOrEmpty(sp.ResultCard))
                {
                    try
                    {
                        if (WorldManager.instance.GetCardCount(sp.ResultCard) >= bp.MaxAmountOnBoard)
                            plan.Missing.Add(Loc.T("recipe_max", bp.MaxAmountOnBoard));
                    }
                    catch (System.Exception ex) { Log.Once(ex); }
                }
                if (plan.Possible) return plan;
                if (best == null || plan.Missing.Count < best.Missing.Count) best = plan;
            }
            return best;
        }

        public static List<Blueprint> KnownBlueprints()
        {
            var wm = WorldManager.instance;
            var list = new List<Blueprint>();
            if (wm == null) return list;
            bool cities = wm.CurrentBoard != null && wm.CurrentBoard.Id == "cities";
            try { Subprint.GetSpecialCardIds(); } catch (System.Exception ex) { Log.Once(ex); }
            foreach (var bp in wm.BlueprintPrefabs)
            {
                if (bp == null || bp.HideFromIdeasTab || !wm.HasFoundCard(bp.Id)) continue;
                var c = wm.GameDataLoader.GetCardFromId(bp.Id);
                if (c == null || (c.CardUpdateType == CardUpdateType.Cities) != cities) continue;
                list.Add(bp);
            }
            return list.OrderBy(b => TextCleaner.Clean(b.KnowledgeName)).ToList();
        }

        private static string Ingredients(Subprint sp)
        {
            try { return TextCleaner.Clean(sp.DefaultText()).Replace(". ", ", "); } catch (System.Exception ex) { Log.Once(ex); return ""; }
        }

        private static string Duration(Subprint sp) => sp.Time > 0 ? CardInfo.Seconds(sp.Time) : "";

        private static string ResultInfo(Blueprint bp)
        {
            string text = "";
            try { text = bp.KnowledgeText; } catch (System.Exception ex) { Log.Once(ex); }
            return TextCleaner.Join(TextCleaner.Clean(bp.KnowledgeName), TextCleaner.Clean(text));
        }

        /// <summary>Pages for the L key: recipes possible now, then all known ideas.</summary>
        private static string GroupName(BlueprintGroup g)
        {
            string s = SokLoc.Translate("ideagroup_" + g.ToString().ToLowerInvariant());
            return string.IsNullOrEmpty(s) || s.StartsWith("---") ? g.ToString() : TextCleaner.Clean(s);
        }

        /// <summary>
        /// Pages of the L key: recipes possible now, production buildings, then one page per idea group
        /// (possible ideas first, then the others, each in alphabetical order).
        /// </summary>
        public static List<ListBrowser.Page> Pages(BoardNavigator board)
        {
            var pool = Pool();
            var possible = new List<(string name, ListBrowser.Entry entry)>();
            var groups = new Dictionary<BlueprintGroup, List<(bool ok, string name, ListBrowser.Entry entry)>>();
            foreach (var bp in KnownBlueprints())
            {
                Plan plan = null;
                try { plan = Analyze(bp, pool); } catch (System.Exception ex) { Log.Once(ex); }
                string name = TextCleaner.Clean(bp.KnowledgeName);
                var b = bp;
                bool ok = plan != null && plan.Possible;
                if (ok)
                {
                    var p = plan;
                    string state = p.AlreadyInPlace ? Loc.T("recipe_in_place") : "";
                    possible.Add((name, new ListBrowser.Entry(TextCleaner.Join(name + " : " + Ingredients(p.Subprint), Duration(p.Subprint), state))
                    {
                        OnActivate = () => Build(p, board),
                        Info = () => ResultInfo(b),
                    }));
                }
                string status = plan == null ? "" : ok ? Loc.T("recipe_possible") : Loc.T("recipe_missing", string.Join(", ", plan.Missing));
                string ingredients = plan != null ? Ingredients(plan.Subprint) : "";
                var entry = new ListBrowser.Entry(TextCleaner.Join(name + (ingredients != "" ? " : " + ingredients : ""), status))
                {
                    Info = () => ResultInfo(b),
                };
                if (ok)
                {
                    var p2 = plan;
                    entry.OnActivate = () => Build(p2, board);
                }
                if (!groups.TryGetValue(bp.BlueprintGroup, out var list)) groups[bp.BlueprintGroup] = list = new List<(bool, string, ListBrowser.Entry)>();
                list.Add((ok, name, entry));
            }
            var cmp = System.StringComparer.CurrentCultureIgnoreCase;
            var pages = new List<ListBrowser.Page>
            {
                new ListBrowser.Page(Loc.T("list_possible"), possible.OrderBy(x => x.name, cmp).Select(x => x.entry).ToList(), Loc.T("no_possible")),
                new ListBrowser.Page(Loc.T("list_production"), Production.Entries(board), Loc.T("no_production")),
            };
            foreach (var g in groups.Keys.OrderBy(k => (int)k))
            {
                var entries = groups[g].OrderBy(x => x.ok ? 0 : 1).ThenBy(x => x.name, cmp).Select(x => x.entry).ToList();
                int n = groups[g].Count(x => x.ok);
                pages.Add(new ListBrowser.Page(GroupName(g) + (n > 0 ? ", " + Loc.N("n_possible", n, n) : ""), entries));
            }
            return pages;
        }

        /// <summary>Takes the cards of the plan out of their stacks and stacks them together.</summary>
        public static void Build(Plan plan, BoardNavigator board)
        {
            var wm = WorldManager.instance;
            if (wm == null || !wm.CanInteract) { Speech.Say(Loc.T("not_now")); return; }
            BoardNavigator.FixStuckDrag();
            if (plan.AlreadyInPlace) { Speech.Say(Loc.T("recipe_in_place")); return; }
            if (plan.Cards.Any(c => c == null || c.Destroyed))
            {
                Speech.Say(Loc.T("recipe_changed"));
                return;
            }
            BoardNavigator.InvalidateCache();
            foreach (var c in plan.Cards) Extract(c);

            // Try each card as the bottom of the pile (cards that cannot move first, villagers last),
            // then add the others in any order the game accepts (only the card on top decides).
            // The order is planned first and the cards are moved after: as a multiplayer client
            // every move is a request to the host, so there is no trial and error on the board.
            var roots = plan.Cards
                .OrderBy(c => c.CanBeDragged() ? 1 : 0)
                .ThenBy(c => c.CardData.MyCardType == CardType.Humans ? 1 : 0)
                .ToList();
            GameCard built = null;
            List<GameCard> order = null;
            List<GameCard> leftovers = null;
            foreach (var root in roots)
            {
                var placed = new List<GameCard> { root };
                var rest = plan.Cards.Where(c => c != root).OrderBy(c => c.CardData.MyCardType == CardType.Humans ? 1 : 0).ToList();
                bool progress = true;
                while (rest.Count > 0 && progress)
                {
                    progress = false;
                    var top = placed[placed.Count - 1];
                    foreach (var c in rest)
                    {
                        if (!top.CardData.CanHaveCardOnTop(c.CardData)) continue;
                        placed.Add(c);
                        rest.Remove(c);
                        progress = true;
                        break;
                    }
                }
                if (rest.Count == 0) { order = placed; break; }
                if (leftovers == null || rest.Count < leftovers.Count) leftovers = rest;
            }
            if (order != null)
            {
                built = order[0];
                for (int i = 1; i < order.Count; i++)
                {
                    if (Mover.MoveChain(order[i], order[i - 1])) continue;
                    // The game refused this card on the pile (pile rules depend on its bottom card).
                    leftovers = order.Skip(i).ToList();
                    built = null;
                    break;
                }
            }

            string name = TextCleaner.Clean(plan.Blueprint.KnowledgeName);
            if (built == null)
            {
                Speech.Say(Loc.T("recipe_partial", name, string.Join(", ", (leftovers ?? new List<GameCard>()).Select(CardInfo.Name))));
                return;
            }
            built.StackUpdate = true;
            AutoStacker.MarkManual(built);
            BoardNavigator.LastDropRoot = built;
            BoardNavigator.LastDropTime = Time.unscaledTime;
            board.FocusStack(built);
            // Check with the game's own matching that this idea will start.
            bool matches = true;
            try { matches = plan.Blueprint.GetMatchingSubprint(built, out _) != null; }
            catch (System.Exception ex) { Log.Once(ex); }
            Speech.Say(TextCleaner.Join(Loc.T("recipe_built", name, CardInfo.StackLine(built)), matches ? "" : Loc.T("recipe_no_match")));
        }

        /// <summary>Removes one card from its stack, closing the gap (the cards above go down on the card below).</summary>
        private static void Extract(GameCard c) => Mover.Extract(c);
    }
}
