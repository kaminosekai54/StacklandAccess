using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>Board actions: pick up, drop, sell, open and buy packs, pay merchants.</summary>
    public partial class BoardNavigator
    {
        // ------------------------------------------------------------------ actions

        /// <summary>Clears "being dragged" flags left on cards when nothing is actually dragged.</summary>
        public static void FixStuckDrag()
        {
            var wm = WorldManager.instance;
            if (wm == null || wm.DraggingDraggable != null) return;
            // In multiplayer, cards held by other players are flagged too: leave them alone.
            if (Together.InSession) return;
            foreach (var c in wm.AllCards)
            {
                if (c != null && !c.Destroyed && c.BeingDragged) c.BeingDragged = false;
            }
        }

        private static bool CanAct()
        {
            FixStuckDrag();
            var wm = WorldManager.instance;
            if (wm == null || !wm.CanInteract)
            {
                string why = "";
                if (wm != null)
                {
                    if (GameCanvas.instance != null && GameCanvas.instance.ModalIsOpen) why = Loc.T("why_modal");
                    else if (!wm.IsPlaying) why = Loc.T("why_paused_menu");
                    else if (wm.InAnimation) why = Loc.T("why_animation");
                }
                Speech.Say(TextCleaner.Join(Loc.T("not_now"), why));
                return false;
            }
            return true;
        }

        private void Activate()
        {
            Sync();
            if (current == null) { Speech.Say(Loc.T("cat_empty", CategoryName(category))); return; }
            if (!CanAct()) return;

            if (IsHolding)
            {
                // Multiplayer: another player may have grabbed the card since it was picked up here.
                if (Together.InSession && (Held.BeingDragged || !Held.CanBeDragged()))
                {
                    Speech.Say(Loc.T("mp_card_taken", CardInfo.Name(Held)));
                    Held = null;
                    return;
                }
                if (current is GameCard targetRoot) StackOnto(Held, targetRoot);
                else if (current is CardTarget ct) DropOnTarget(Held, ct, announce: true);
                else Speech.Say(Loc.T("cant_drop", CardInfo.Name(Held), CardInfo.Name(current)));
                return;
            }

            switch (current)
            {
                case GameCard root when stackIndex == 0 && IsMerchant(root.CardData):
                    PayMerchant(root);
                    break;
                case GameCard _:
                    {
                        var card = CurrentCard();
                        if (card == null) return;
                        if (!card.CanBeDragged()) { Speech.Say(Loc.T("cant_pick", CardInfo.Name(card))); return; }
                        Held = card;
                        int above = card.GetAllCardsInStack().Count - card.GetAllCardsInStack().IndexOf(card) - 1;
                        string msg = above > 0 ? Loc.N("picked_stack", above, CardInfo.Name(card), above) : Loc.T("picked", CardInfo.Name(card));
                        Speech.Say(TextCleaner.Join(msg, Loc.Hint("picked_hint")));
                        break;
                    }
                case Boosterpack bp:
                    OpenPack(bp);
                    break;
                case BuyBoosterBox bb:
                    AutoBuy(bb);
                    break;
                default:
                    Speech.Say(CardInfo.Details(current));
                    break;
            }
        }

        private static void SetDragStartLongAgo(Draggable d)
        {
            // Draggable.StopDragging() treats a very short drag as a click; make sure our moves never are.
            try { Traverse.Create(d).Field("dragStartTime").SetValue(-1000f); } catch (System.Exception ex) { Log.Once(ex); }
        }

        private static void BeginMove(GameCard card)
        {
            InvalidateCache();
            card.DragStartPosition = card.transform.position;
            // Stacklands Together treats a release close to the start as a click on chests (a coin pops out).
            if (Together.IsClient) card.DragStartPosition = card.transform.position + new Vector3(100f, 0f, 0f);
            card.StartDragging();
            SetDragStartLongAgo(card);
        }

        private void StackOnto(GameCard held, GameCard targetRoot)
        {
            string heldName = CardInfo.Name(held);
            string targetName = CardInfo.Name(targetRoot);
            if (targetRoot == held.GetRootCard() || targetRoot.IsChildOf(held))
            {
                Speech.Say(Loc.T("same_stack"));
                return;
            }
            var leaf = targetRoot.GetLeafCard();
            var withStatus = leaf.GetCardWithStatusInStack();
            if (withStatus != null && !withStatus.CardData.CanHaveCardsWhileHasStatus())
            {
                Speech.Say(Loc.T("target_busy", targetName));
                return;
            }
            var origParent = held.Parent;
            if (!leaf.CardData.CanHaveCardOnTop(held.CardData))
            {
                Speech.Say(Loc.T("cant_drop", heldName, targetName));
                return;
            }
            BeginMove(held);
            if (!leaf.CardData.CanHaveCardOnTop(held.CardData))
            {
                if (origParent != null && !origParent.Destroyed) held.SetParent(origParent);
                held.StopDragging();
                Speech.Say(Loc.T("cant_drop", heldName, targetName));
                return;
            }
            Vector3 p = leaf.transform.position;
            held.transform.position = new Vector3(p.x, held.transform.position.y, p.z - 0.05f);
            held.SetParent(leaf);
            held.StopDragging();
            Held = null;
            AutoStacker.MarkManual(held);
            LastDropRoot = held.GetRootCard();
            LastDropTime = Time.unscaledTime;
            current = held.GetRootCard();
            Speech.Say(Loc.T("dropped_on", heldName, targetName));
        }

        /// <summary>Drop a card (and the cards above it) on the sell box, a buy box or another card target.</summary>
        private bool DropOnTarget(GameCard held, CardTarget target, bool announce)
        {
            string heldName = CardInfo.Name(held);
            // Checked before picking the card up: a refused drop must not send anything to a multiplayer host.
            if (!target.CanHaveCard(held))
            {
                if (announce)
                {
                    if (target is SellBox) Speech.Say(Loc.T("cant_sell_now", heldName));
                    else if (target is BuyBoosterBox box)
                    {
                        if (box.Booster == null || !box.Booster.IsUnlocked) Speech.Say(Loc.T("buybox_locked"));
                        else Speech.Say(Loc.T("no_currency_in_stack", heldName, CurrencyName(box.BoardCurrency)));
                    }
                    else Speech.Say(Loc.T("cant_drop", heldName, CardInfo.Name(target)));
                }
                return false;
            }
            BeginMove(held);
            int boughtBefore = WorldManager.instance.BoughtBoosterIds.Count;
            var moved = held.GetAllCardsInStack();
            target.CardDropped(held);
            if (!held.Destroyed) held.StopDragging();
            // Paying can destroy the card we "dragged" (the coin at the bottom): the coins left over
            // would stay flagged as being dragged and could never be used again.
            foreach (var c in moved)
            {
                if (c != null && !c.Destroyed) c.BeingDragged = false;
            }
            Held = null;
            if (announce)
            {
                if (target is SellBox) Speech.Say(Loc.T("sold", heldName));
                else if (target is BuyBoosterBox bb && Together.IsClient) Speech.Say(Loc.T("mp_purchase_sent", CardInfo.Name(bb)));
                else if (target is BuyBoosterBox bb2) AnnouncePurchase(bb2, boughtBefore);
                else Speech.Say(Loc.T("dropped_on", heldName, CardInfo.Name(target)));
            }
            return true;
        }

        private static void AnnouncePurchase(BuyBoosterBox bb, int boughtBefore)
        {
            if (WorldManager.instance.BoughtBoosterIds.Count > boughtBefore) Speech.Say(Loc.T("bought", CardInfo.Name(bb)));
            else Speech.Say(Loc.T("paid_missing", bb.StoredCostAmount, bb.GetCurrentCost(), CurrencyName(bb.BoardCurrency, bb.GetCurrentCost())));
        }

        /// <summary>
        /// Backspace without holding anything: takes the focused card (and the cards above it) off its pile.
        /// On the bottom card, the bottom card is taken out alone and the rest of the pile stays.
        /// </summary>
        private void Unstack()
        {
            Sync();
            var stack = CurrentStack();
            if (stack == null || stack.Count < 2) { Speech.Say(Loc.T("nothing_to_unstack")); return; }
            if (!CanAct()) return;
            var card = CurrentCard();
            if (card == null) return;
            if (!card.CanBeDragged()) { Speech.Say(Loc.T("cant_pick", CardInfo.Name(card))); return; }
            if (stackIndex > 0)
            {
                Held = card;
                DropAlone();
                return;
            }
            // Bottom card: the pile above it stays where it is, the bottom card moves aside.
            var rest = card.Child;
            string name = CardInfo.Name(card);
            if (Together.IsClient && rest != null)
            {
                // The host only moves a card with the cards above it: move the rest of the pile aside instead.
                Vector3 aside = FindFreeSpot(card.transform.position, rest);
                Mover.MoveChain(rest, null, aside);
                current = rest;
                stackIndex = 0;
                Speech.Say(Loc.T("unstacked_bottom", name, CardInfo.StackLine(rest)));
                return;
            }
            card.RemoveFromStack();
            if (rest != null) rest.SetParent(null);
            BeginMove(card);
            for (var c = rest; c != null; c = c.Child) c.BeingDragged = false;
            Vector3 pos = FindFreeSpot(card.transform.position, card);
            pos.y = card.transform.position.y;
            card.transform.position = pos;
            card.TargetPosition = pos;
            card.StopDragging();
            AutoStacker.MarkManual(card);
            if (rest != null) AutoStacker.MarkManual(rest);
            current = rest != null ? rest : card;
            stackIndex = 0;
            Speech.Say(Loc.T("unstacked_bottom", name, rest != null ? CardInfo.StackLine(rest) : ""));
        }

        private void DropAlone()
        {
            if (!IsHolding) { Unstack(); return; }
            if (!CanAct()) return;
            var held = Held;
            string name = CardInfo.Name(held);
            if (held.IsEquipped || held.IsWorking)
            {
                // Out of the inventory: the game unequips an item dropped without a card under it.
                var holder = held.IsEquipped ? held.EquipmentHolder : held.WorkerHolder;
                BeginMove(held);
                Vector3 p = FindFreeSpot(holder != null ? holder.GetRootCard().transform.position : held.transform.position, held);
                p.y = held.transform.position.y;
                held.transform.position = p;
                held.TargetPosition = p;
                if (held.IsWorking && holder != null && !Together.IsClient) holder.UnequipWorker(held);
                held.StopDragging();
                Held = null;
                AutoStacker.MarkManual(held);
                current = held;
                stackIndex = 0;
                Speech.Say(Loc.T("unequipped", name));
                return;
            }
            if (held.Parent == null)
            {
                Held = null;
                Speech.Say(Loc.T("dropped_free", name));
                return;
            }
            var root = held.GetRootCard();
            BeginMove(held);
            Vector3 pos = FindFreeSpot(root.transform.position, held);
            pos.y = held.transform.position.y;
            held.transform.position = pos;
            held.TargetPosition = pos;
            held.StopDragging();
            Held = null;
            AutoStacker.MarkManual(held);
            current = held;
            stackIndex = 0;
            Speech.Say(Loc.T("dropped_free", name));
        }

        private static Vector3 FindFreeSpot(Vector3 origin, GameCard ignore)
        {
            var wm = WorldManager.instance;
            float dx = wm.GridWidth * 1.5f, dz = wm.GridHeight * 1.6f;
            var others = AllObjects().Where(d => d != ignore).Select(d => d.transform.position).ToList();
            for (int ring = 1; ring <= 4; ring++)
            {
                for (int ix = -ring; ix <= ring; ix++)
                {
                    for (int iz = -ring; iz <= ring; iz++)
                    {
                        if (Mathf.Abs(ix) != ring && Mathf.Abs(iz) != ring) continue;
                        var p = origin + new Vector3(ix * dx, 0f, iz * dz);
                        if (others.All(o => Vector2.Distance(new Vector2(o.x, o.z), new Vector2(p.x, p.z)) > dx * 0.8f)) return p;
                    }
                }
            }
            return origin + new Vector3(dx, 0f, 0f);
        }

        private void Sell()
        {
            Sync();
            var card = IsHolding ? Held : CurrentCard();
            if (card == null) { Speech.Say(Loc.T("not_now")); return; }
            if (!CanAct()) return;
            var sellBox = WorldManager.instance.CardTargets.OfType<SellBox>().FirstOrDefault(OnCurrentBoard);
            if (sellBox == null || !card.CanBeDragged()) { Speech.Say(Loc.T("cant_sell_now", CardInfo.Name(card))); return; }
            if (sellConfirmCard != card || Time.unscaledTime - sellConfirmTime > 4f)
            {
                sellConfirmCard = card;
                sellConfirmTime = Time.unscaledTime;
                var stack = card.GetAllCardsInStack();
                int idx = stack.IndexOf(card);
                var sold = idx >= 0 ? stack.Skip(idx).ToList() : new List<GameCard> { card };
                Speech.Say(Loc.T("sell_confirm", CardInfo.Summary(sold)));
                return;
            }
            sellConfirmCard = null;
            DropOnTarget(card, sellBox, announce: true);
        }

        private void ClickCard()
        {
            Sync();
            var card = CurrentCard();
            if (card == null) { if (current != null) Speech.Say(CardInfo.Details(current)); return; }
            if (!CanAct()) return;
            card.Clicked();
            Speech.Say(Loc.T("clicked", CardInfo.Name(card)));
        }

        private void OpenPack(Boosterpack bp)
        {
            bool anyLeft = bp.PackData != null && bp.PackData.CardBags.Any(b => b.CardsInPack > 0);
            if (!anyLeft) { Speech.Say(Loc.T("pack_empty")); return; }
            bp.Clicked();
            // The new card is announced by the EventNarrator.
        }

        private static bool IsMoney(CardData cd)
        {
            return cd.Id == "gold" || cd.Id == "shell" || cd is Dollar || cd is Chest || cd is Creditcard;
        }

        private static string CurrencyName(BoardCurrency c, int count = 2)
        {
            switch (c)
            {
                case BoardCurrency.Shell: return Loc.Word("icon_shell", count);
                case BoardCurrency.Dollar: return Loc.Word("icon_dollar", count);
                default: return Loc.Word("icon_gold", count);
            }
        }

        /// <summary>
        /// For each stack holding money, the lowest card from which everything up to the top is money:
        /// that part can be taken without breaking the rest of the stack (coins put on a villager, etc).
        /// </summary>
        private static List<GameCard> MoneyParts(BuyBoosterBox bb, out int blocked)
        {
            blocked = 0;
            var result = new List<(GameCard card, int rank)>();
            foreach (var root in AllObjects().OfType<GameCard>())
            {
                var stack = root.GetAllCardsInStack();
                if (!stack.Any(c => IsMoney(c.CardData))) continue;
                int k = stack.Count;
                while (k > 0 && IsMoney(stack[k - 1].CardData)) k--;
                if (k >= stack.Count) { blocked++; continue; } // money buried under other cards
                var part = stack[k];
                if (!part.CanBeDragged()) { blocked++; continue; }
                bool busy = root.GetCardWithStatusInStack() != null;
                bool chest = part.CardData is Chest || part.CardData is Creditcard;
                int rank = (k == 0 ? 0 : 1) + (busy ? 2 : 0) + (chest ? 4 : 0);
                result.Add((part, rank));
            }
            return result.OrderBy(r => r.rank).Select(r => r.card).ToList();
        }

        /// <summary>How much money a card and the cards above it hold.</summary>
        private static int MoneyValue(GameCard part)
        {
            int total = 0;
            for (var c = part; c != null; c = c.Child)
            {
                var d = c.CardData;
                if (d is Chest ch) total += ch.CoinCount;
                else if (d is Creditcard cc) total += cc.DollarCount;
                else if (IsMoney(d)) total += 1;
            }
            return total;
        }

        /// <summary>Travelling cart (3 coins per item) or merchant (takes all the coins given).</summary>
        private void PayMerchant(GameCard merchantRoot)
        {
            FixStuckDrag();
            int need = merchantRoot.CardData is TravellingCart tc ? tc.GoldToUse : 1;
            var piles = new List<List<GameCard>>();
            foreach (var root in AllObjects().OfType<GameCard>())
            {
                if (root == merchantRoot) continue;
                var stack = root.GetAllCardsInStack();
                int k = stack.Count;
                while (k > 0 && stack[k - 1].CardData.Id == "gold") k--;
                if (k < stack.Count) piles.Add(stack.Skip(k).ToList());
            }
            int total = piles.Sum(p => p.Count);
            if (total < need)
            {
                Speech.Say(Loc.T("no_money", CardInfo.Name(merchantRoot), need, Loc.T("icon_gold"), total));
                return;
            }
            var pile = merchantRoot.CardData is TravellingCart
                ? piles.Where(p => p.Count >= need).OrderBy(p => p.Count).FirstOrDefault()
                : piles.OrderByDescending(p => p.Count).FirstOrDefault();
            if (pile == null)
            {
                Speech.Say(Loc.T("need_pile", need, Loc.T("icon_gold")));
                return;
            }
            // Give exactly what is needed for the cart (the coins on top of the pile).
            var part = merchantRoot.CardData is TravellingCart ? pile[pile.Count - need] : pile[0];
            if (!part.CanBeDragged()) { Speech.Say(Loc.T("cant_pick", CardInfo.Name(part))); return; }
            Held = part;
            StackOnto(part, merchantRoot);
        }

        private void AutoBuy(BuyBoosterBox bb)
        {
            if (bb.Booster == null || !bb.Booster.IsUnlocked) { Speech.Say(Loc.T("buybox_locked")); return; }
            var wm = WorldManager.instance;
            int cost = bb.GetCurrentCost();
            string money = CurrencyName(bb.BoardCurrency);
            int available;
            switch (bb.BoardCurrency)
            {
                case BoardCurrency.Shell: available = wm.GetShellCount(true); break;
                case BoardCurrency.Dollar: available = wm.GetDollarCount(true); break;
                default: available = wm.GetGoldCount(true); break;
            }
            if (available < cost)
            {
                Speech.Say(Loc.T("no_money", CardInfo.Name(bb), cost, money, available));
                return;
            }
            int boughtBefore = wm.BoughtBoosterIds.Count;
            int paidBefore = bb.StoredCostAmount;
            var parts = MoneyParts(bb, out int blocked);
            if (Together.IsClient)
            {
                // The host takes the payment later: send just enough money, then stop.
                int still = cost, sent = 0; // GetCurrentCost() already deducts what was paid
                foreach (var part in parts)
                {
                    if (sent >= still) break;
                    if (part == null || part.Destroyed) continue;
                    int value = MoneyValue(part);
                    if (DropOnTarget(part, bb, announce: false)) sent += value; else blocked++;
                }
                Speech.Say(sent > 0 ? Loc.T("mp_purchase_sent", CardInfo.Name(bb)) : Loc.N("money_blocked", blocked, blocked, money));
                return;
            }
            foreach (var part in parts)
            {
                if (wm.BoughtBoosterIds.Count > boughtBefore) break;
                if (part == null || part.Destroyed) continue;
                if (!DropOnTarget(part, bb, announce: false)) blocked++;
            }
            if (wm.BoughtBoosterIds.Count > boughtBefore)
            {
                Speech.Say(Loc.T("bought", CardInfo.Name(bb)));
                return;
            }
            int missing = bb.GetCurrentCost();
            Speech.Say(TextCleaner.Join(
                Loc.T("paid_missing", bb.StoredCostAmount, missing, CurrencyName(bb.BoardCurrency, missing)),
                blocked > 0 ? Loc.N("money_blocked", blocked, blocked, money) : Loc.T("money_unreachable", money)));
        }

    }
}
