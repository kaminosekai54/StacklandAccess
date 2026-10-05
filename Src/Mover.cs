using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Card moves done by the mod itself (recipes, production buildings, stacking).
    /// Alone or as the host, the board is changed directly. As a Stacklands Together client, each move is
    /// also sent to the host as a grab followed by a release, exactly like a mouse drag.
    /// </summary>
    public static class Mover
    {
        /// <summary>Moves a card and the cards above it onto the top of another stack, or alone to a position.</summary>
        /// <returns>False if the game does not accept the card there (nothing is moved then).</returns>
        public static bool MoveChain(GameCard card, GameCard onto, Vector3? position = null)
        {
            if (card == null || card.Destroyed) return false;
            bool net = Together.IsClient;
            GameCard leaf = onto != null ? onto.GetLeafCard() : null;
            if (leaf != null && (leaf == card || leaf.IsPartOfSameStack(card))) return false;
            // Same check as a real drop: the top card of the target pile decides (with its pile around it).
            if (leaf != null && !leaf.CardData.CanHaveCardOnTop(card.CardData)) return false;
            if (net) Together.Grab(card);
            if (leaf != null)
            {
                Vector3 p = leaf.transform.position;
                card.transform.position = new Vector3(p.x, card.transform.position.y, p.z - 0.05f);
                // SetParent(x) does not clear the old parent's link to this card: detach first.
                card.SetParent(null);
                card.SetParent(leaf);
            }
            else
            {
                card.SetParent(null);
                if (position.HasValue)
                {
                    Vector3 q = position.Value;
                    q.y = card.transform.position.y;
                    card.transform.position = q;
                    card.TargetPosition = q;
                }
            }
            card.StackUpdate = true;
            if (net) Together.Release(card, leaf != null ? leaf.CardData.UniqueId : "");
            return true;
        }

        /// <summary>Takes one card out of its stack; the cards above it go down on the card below.</summary>
        public static void Extract(GameCard c)
        {
            if (c == null) return;
            var parent = c.Parent;
            var child = c.Child;
            if (parent == null && child == null) return;
            if (!Together.IsClient)
            {
                c.RemoveFromStack();
                if (parent != null && child != null) child.SetParent(parent);
                if (child != null && parent == null) child.TargetPosition = child.transform.position;
                return;
            }
            // Client: the host only knows whole-chain moves. Take the card with the cards above it,
            // then put the cards above it back where they were.
            Vector3 here = c.transform.position;
            MoveChain(c, null, here);
            if (child != null)
            {
                child.SetParent(null);
                if (c.Child == child) c.Child = null;
                if (parent == null || parent.Destroyed || !MoveChain(child, parent, here)) MoveChain(child, null, here);
            }
        }
    }
}
