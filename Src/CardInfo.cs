using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>Builds the spoken descriptions of cards, stacks, packs and boxes.</summary>
    public static class CardInfo
    {
        public static string Name(GameCard card)
        {
            if (card == null || card.CardData == null) return "";
            return TextCleaner.Clean(card.CardData.FullName);
        }

        public static string Name(Draggable d)
        {
            switch (d)
            {
                case GameCard gc: return Name(gc);
                case Boosterpack bp: return TextCleaner.Clean(bp.Name);
                case BuyBoosterBox bb: return bb.Booster != null && bb.Booster.IsUnlocked ? TextCleaner.Clean(bb.Booster.Name) : Loc.T("buybox_locked");
                case SellBox sb: return TextCleaner.Clean(sb.gameObject.name);
                default: return d != null ? TextCleaner.Clean(d.gameObject.name) : "";
            }
        }

        /// <summary>Short status of a stack: current action with time left, or combat.</summary>
        public static string StackStatus(GameCard root)
        {
            if (root == null) return "";
            var withStatus = root.GetCardWithStatusInStack();
            if (withStatus != null && withStatus.TimerRunning && !string.IsNullOrEmpty(withStatus.Status))
            {
                float left = Mathf.Max(0f, withStatus.TargetTimerTime - withStatus.CurrentTimerTime);
                return Loc.T("busy", TextCleaner.Clean(withStatus.Status), Seconds(left));
            }
            if (root.GetCardInCombatInStack() != null) return Loc.T("in_combat");
            return "";
        }

        /// <summary>"Villager, 3 Wood" (in stack order, the game says "1x Villager, 3x Wood").</summary>
        public static string Summary(List<GameCard> cards)
        {
            var names = cards.Where(c => c != null && c.CardData != null).Select(Name).ToList();
            return string.Join(", ", names.Distinct().Select(n =>
            {
                int k = names.Count(x => x == n);
                return k > 1 ? k + " " + n : n;
            }));
        }

        public static string Seconds(float s)
        {
            int n = Mathf.CeilToInt(s);
            if (n >= 60)
            {
                int m = n / 60, r = n % 60;
                return r == 0 ? $"{m} min" : $"{m} min {r} s";
            }
            return Loc.N("seconds", n, n);
        }

        /// <summary>One line for a stack in the board list.</summary>
        public static string StackLine(GameCard root)
        {
            if (root == null) return "";
            if (!root.FaceUp) return Loc.T("face_down");
            var cards = root.GetAllCardsInStack();
            string head = Name(root);
            string hp = HealthShort(root.CardData);
            string stack = "";
            if (cards.Count > 1)
            {
                // "stack of 4: 1x Villager, 3x Wood" -> skip the root repeated alone
                stack = Loc.T("stack_of", cards.Count) + " : " + Summary(cards);
            }
            string content = Contents(root.CardData);
            return TextCleaner.Join(head + (hp != "" ? ", " + hp : "") + (content != "" ? ", " + content : ""), EffectNames(root.CardData), stack, StackStatus(root));
        }

        /// <summary>One line for any navigable object.</summary>
        public static string ObjectLine(Draggable d)
        {
            switch (d)
            {
                case GameCard gc when gc.CardData is TravellingCart tc:
                    return TextCleaner.Join(StackLine(gc), Loc.T("cart_cost", tc.GoldToUse));
                case GameCard gc when gc.CardData is Merchant m:
                    return TextCleaner.Join(StackLine(gc), Loc.T("merchant_given", m.AmountGiven, m.AmountNeeded));
                case GameCard gc when gc.CardData is StrangePortal sp:
                    return TextCleaner.Join(StackLine(gc), Loc.T("portal_info", sp.SpawnsRemaining));
                case GameCard gc when gc.CardData is IndustrialRevolution || gc.CardData.Id == "event_industrial_revolution":
                    return TextCleaner.Join(StackLine(gc), Loc.T("city_gate_info"));
                case GameCard gc: return StackLine(gc);
                case Boosterpack bp:
                    {
                        int left = bp.TotalCardsInPack - bp.TimesOpened;
                        if (!bp.WasClicked) return Loc.T("pack_closed", Name(bp));
                        return Loc.N("pack", Mathf.Max(0, left), Name(bp), Mathf.Max(0, left));
                    }
                case BuyBoosterBox bb:
                    {
                        if (bb.Booster == null || !bb.Booster.IsUnlocked) return Loc.T("buybox_locked");
                        return Loc.T("buybox", Name(bb), TextCleaner.Clean(bb.BuyText != null ? bb.BuyText.text : bb.GetCurrentCost().ToString()));
                    }
                case SellBox sb: return Loc.T("sellbox", Name(sb));
                default: return Name(d);
            }
        }

        private static readonly HashSet<string> QuietEffects = new HashSet<string> { "StatusEffect_MaxOnBoard", "StatusEffect_Demand", "StatusEffect_Space", "StatusEffect_CardOff", "StatusEffect_Radar" };

        public static bool IsQuietEffect(StatusEffect e) => e == null || QuietEffects.Contains(e.GetType().Name);

        /// <summary>Names of the status effects of a card (poisoned, sick, on fire...).</summary>
        public static string EffectNames(CardData cd)
        {
            if (cd == null || cd.StatusEffects == null || cd.StatusEffects.Count == 0) return "";
            var names = cd.StatusEffects.Where(e => e != null).Select(e => { try { return TextCleaner.Clean(e.Name); } catch (System.Exception ex) { Log.Once(ex); return ""; } })
                .Where(n => n != "").Distinct().ToList();
            return names.Count == 0 ? "" : Loc.T("effects", string.Join(", ", names));
        }

        /// <summary>Content of storage cards: coin chest, resource chest, food warehouse, credit card.</summary>
        public static string Contents(CardData cd)
        {
            try
            {
                switch (cd)
                {
                    case Chest ch: return Loc.T("contains", ch.CoinCount, Loc.Word(ch.HeldCardId == "shell" ? "icon_shell" : "icon_gold", ch.CoinCount));
                    case ResourceChest rc: return Loc.T("contains", rc.ResourceCount, string.IsNullOrEmpty(rc.HeldCardId) ? Loc.T("nothing") : Recipes_PrefabName(rc.HeldCardId));
                    case FoodWarehouse fw: return Loc.T("contains_food", fw.FoodValue);
                    case Creditcard cc: return Loc.T("contains", cc.DollarCount, Loc.Word("icon_dollar", cc.DollarCount));
                }
            }
            catch (System.Exception ex) { Log.Once(ex); }
            return "";
        }

        private static string Recipes_PrefabName(string id)
        {
            try { var p = WorldManager.instance.GetCardPrefab(id); if (p != null) return TextCleaner.Clean(p.Name); } catch (System.Exception ex) { Log.Once(ex); }
            return id;
        }

        public static string HealthShort(CardData cd)
        {
            if (cd is Combatable c)
            {
                try { return Loc.T("hp", c.HealthPoints, c.ProcessedCombatStats.MaxHealth); } catch (System.Exception ex) { Log.Once(ex); }
            }
            return "";
        }

        /// <summary>Line for one card inside a stack (Up/Down navigation).</summary>
        public static string CardInStack(GameCard card, int index, int count)
        {
            string pos = Loc.T("card_pos", index + 1, count);
            string hp = HealthShort(card.CardData);
            string content = Contents(card.CardData);
            return TextCleaner.Join(Name(card) + (hp != "" ? ", " + hp : "") + (content != "" ? ", " + content : ""), pos);
        }

        /// <summary>Everything we know about a card, as a single text.</summary>
        public static string Details(Draggable d)
        {
            return TextCleaner.Join(DetailLines(d).Select(e => e.Detail == null ? e.Title : e.Title + " " + e.Detail).ToArray());
        }

        public static string Currency(int count = 2)
        {
            var b = WorldManager.instance != null ? WorldManager.instance.CurrentBoard : null;
            if (b != null && b.Location == Location.Island) return Loc.Word("icon_shell", count);
            if (b != null && b.Location == Location.Cities) return Loc.Word("icon_dollar", count);
            return Loc.Word("icon_gold", count);
        }

        private static void Add(List<ListBrowser.Entry> list, string text)
        {
            text = TextCleaner.Clean(text);
            if (!string.IsNullOrWhiteSpace(text)) list.Add(new ListBrowser.Entry(text));
        }

        /// <summary>Everything we know about a card, one line per fact (I key opens it as a list).</summary>
        public static List<ListBrowser.Entry> DetailLines(Draggable d)
        {
            var lines = new List<ListBrowser.Entry>();
            if (d is GameCard card)
            {
                var cd = card.CardData;
                Add(lines, Name(card) + (card.FaceUp ? "" : ", " + Loc.T("face_down")));
                Add(lines, Loc.T("type", TypeName(cd.MyCardType)));
                Add(lines, Contents(cd));

                // Selling price of this card, and of everything from this card to the top of the stack.
                int value = cd.GetValue();
                if (value == -1) Add(lines, Loc.T("cant_sell"));
                else Add(lines, Loc.T("sell_price", value, Currency(value)));
                var stack = card.GetAllCardsInStack();
                int idx = stack.IndexOf(card);
                if (stack.Count > 1)
                {
                    Add(lines, Loc.T("in_stack", idx + 1, stack.Count, Summary(stack)));
                    try
                    {
                        var root = card.GetRootCard();
                        int total = WorldManager.instance.GetStackValue(root).TotalValue;
                        Add(lines, Loc.T("stack_price", total, Currency(total)));
                    }
                    catch (System.Exception ex) { Log.Once(ex); }
                }
                if (cd is Food food && food.FoodValue > 0) Add(lines, Loc.T("food_value", food.FoodValue));
                if (cd is Combatable cb)
                {
                    Add(lines, HealthShort(cd));
                    try { Add(lines, cb.GetCombatableDescriptionAdvanced()); } catch (System.Exception ex) { Log.Once(ex); }
                }
                if (card.EquipmentChildren != null && card.EquipmentChildren.Count > 0)
                    Add(lines, Loc.T("equipped", string.Join(", ", card.EquipmentChildren.Where(x => x != null).Select(Name))));
                string status = StackStatus(card.GetRootCard());
                if (status != "") Add(lines, status);
                if (cd.StatusEffects != null)
                {
                    foreach (var e in cd.StatusEffects.Where(x => x != null))
                    {
                        try { Add(lines, TextCleaner.Join(e.Name, e.Description)); } catch (System.Exception ex) { Log.Once(ex); }
                    }
                }
                if (cd.HasInventory) Add(lines, Loc.T("has_inventory"));
                if (card.WorkerChildren != null && card.WorkerChildren.Count > 0)
                    Add(lines, Loc.T("workers", string.Join(", ", card.WorkerChildren.Where(x => x != null).Select(Name))));
                // The description may contain several paragraphs: one line each.
                foreach (var part in (cd.Description ?? "").Replace("\\d", "\n").Split('\n'))
                    Add(lines, part);
                try
                {
                    if (cd.RequirementHolders != null && cd.RequirementHolders.Count > 0)
                        Add(lines, cd.GetRequirementDescription(card, 1, onlyShowCurrentlySatisfied: false));
                }
                catch (System.Exception ex) { Log.Once(ex); }
                return lines;
            }
            if (d is Boosterpack bp)
            {
                Add(lines, ObjectLine(bp));
                Add(lines, SokLoc.Translate("label_click_this_pack"));
                return lines;
            }
            if (d is BuyBoosterBox bb && bb.Booster != null && bb.Booster.IsUnlocked)
            {
                Add(lines, ObjectLine(bb));
                try { Add(lines, bb.GetTooltipText()); } catch (System.Exception ex) { Log.Once(ex); }
                try { if (bb.Booster.UndiscoveredCardCount > 0) Add(lines, Loc.N("undiscovered", bb.Booster.UndiscoveredCardCount, bb.Booster.UndiscoveredCardCount)); } catch (System.Exception ex) { Log.Once(ex); }
                return lines;
            }
            Add(lines, ObjectLine(d));
            if (d is Interactable it) { try { Add(lines, it.GetTooltipText()); } catch (System.Exception ex) { Log.Once(ex); } }
            return lines;
        }

        public static string TypeName(CardType t)
        {
            string term = "cardtype_" + t.ToString().ToLowerInvariant();
            string s = SokLoc.Translate(term);
            if (string.IsNullOrEmpty(s) || s.Contains("cardtype_") || s.StartsWith("---")) return t.ToString();
            return s;
        }
    }
}
