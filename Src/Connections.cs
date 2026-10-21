using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// E: connections of the selected card (energy cables, sewer pipes, transport arrows), what they do,
    /// and connecting or disconnecting them without the mouse. In Stacklands the position of a card on the
    /// board only matters through these connections, so the list also says where the other cards are.
    /// </summary>
    public static class Connections
    {
        private const int PageCard = 0, PageNetwork = 1, PageProblems = 2;

        private static ListBrowser list;
        private static BoardNavigator board;

        /// <summary>Connectors usable on this board (on the main island: transport only, once a road builder exists).</summary>
        private static bool Usable(CardConnector c)
        {
            if (c == null || c.Parent == null || c.Parent.Destroyed) return false;
            var wm = WorldManager.instance;
            if (wm == null || c.Parent.MyBoard != wm.CurrentBoard) return false;
            if (wm.CurrentBoard.Id == "cities") return true;
            return c.ConnectionType == ConnectionType.Transport && wm.CanUseTransport;
        }

        private static IEnumerable<CardConnector> ConnectorsOf(GameCard card)
        {
            if (card == null || card.CardConnectorChildren == null) return Enumerable.Empty<CardConnector>();
            return card.CardConnectorChildren.Where(Usable);
        }

        private static IEnumerable<CardConnector> AllConnectors()
        {
            var wm = WorldManager.instance;
            if (wm == null) return Enumerable.Empty<CardConnector>();
            return wm.AllCards.Where(c => c != null && !c.Destroyed).SelectMany(ConnectorsOf);
        }

        public static string TypeName(ConnectionType t)
        {
            switch (t)
            {
                case ConnectionType.LV: return Loc.T("conn_lv");
                case ConnectionType.HV: return Loc.T("conn_hv");
                case ConnectionType.Sewer: return Loc.T("conn_sewer");
                default: return Loc.T("conn_transport");
            }
        }

        private static string DirName(CardConnector c) => Loc.T(c.CardDirection == CardDirection.output ? "conn_out" : "conn_in");

        /// <summary>"2 to the right, 1 up" from one card to another, in card spaces.</summary>
        public static string RelativePosition(Component from, Component to)
        {
            var wm = WorldManager.instance;
            if (from == null || to == null || wm == null) return "";
            Vector3 d = to.transform.position - from.transform.position;
            int dx = Mathf.RoundToInt(d.x / (wm.GridWidth * 1.4f));
            int dz = Mathf.RoundToInt(d.z / (wm.GridHeight * 1.4f));
            var parts = new List<string>();
            if (dx != 0) parts.Add(Loc.T(dx > 0 ? "pos_right" : "pos_left", Mathf.Abs(dx)));
            if (dz != 0) parts.Add(Loc.T(dz > 0 ? "pos_up" : "pos_down", Mathf.Abs(dz)));
            return parts.Count == 0 ? Loc.T("pos_here") : string.Join(", ", parts);
        }

        /// <summary>What the connections give this card: energy, sewer.</summary>
        public static string Effects(GameCard card)
        {
            if (card == null || card.CardData == null) return "";
            var d = card.CardData;
            var parts = new List<string>();
            try
            {
                var cons = d.EnergyConnectors ?? new List<CardConnectorData>();
                bool energyIn = cons.Any(x => x.EnergyConnectionType == CardDirection.input && (x.EnergyConnectionStrength == ConnectionType.LV || x.EnergyConnectionStrength == ConnectionType.HV));
                bool energyOut = cons.Any(x => x.EnergyConnectionType == CardDirection.output && (x.EnergyConnectionStrength == ConnectionType.LV || x.EnergyConnectionStrength == ConnectionType.HV));
                bool sewerIn = cons.Any(x => x.EnergyConnectionType == CardDirection.input && x.EnergyConnectionStrength == ConnectionType.Sewer);
                if (energyIn) parts.Add(Loc.T(d.HasEnergyInput(null) ? "eff_powered" : "eff_unpowered"));
                if (energyOut) parts.Add(Loc.T(d.HasEnergyOutput(null, new List<CardConnector>()) ? "eff_producing" : "eff_not_producing"));
                if (d is IEnergyConsumer ec) { string s = TextCleaner.Clean(ec.GetEnergyConsumptionString()); if (s != "") parts.Add(s); }
                if (sewerIn) parts.Add(Loc.T(d.StatusEffects.OfType<StatusEffect_NoSewer>().Any() ? "eff_no_sewer" : "eff_sewer"));
                if (d.StatusEffects.OfType<StatusEffect_NoEnergy>().Any() && !energyIn) parts.Add(Loc.T("eff_unpowered"));
            }
            catch (System.Exception ex) { Log.Once(ex); }
            return TextCleaner.Join(parts.ToArray());
        }

        private static string ConnectorLine(GameCard owner, CardConnector c)
        {
            string head = TypeName(c.ConnectionType) + ", " + DirName(c);
            var other = c.GetConnectedGameCard();
            if (other == null) return TextCleaner.Join(head, Loc.T("conn_free"));
            return TextCleaner.Join(head, Loc.T("conn_to", CardInfo.Name(other), RelativePosition(owner, other)));
        }

        public static void Open(ListBrowser l, BoardNavigator b, GameCard card, int page = PageCard, int line = 0, bool speak = true)
        {
            list = l;
            board = b;
            list.OpenPagesAt(Pages(card), page, line, speak);
        }

        private static List<ListBrowser.Page> Pages(GameCard card)
        {
            return new List<ListBrowser.Page> { CardPage(card), NetworkPage(), ProblemsPage() };
        }

        private static ListBrowser.Page CardPage(GameCard root)
        {
            var e = new List<ListBrowser.Entry>();
            string title = root != null ? CardInfo.Name(root) : "";
            if (root != null)
            {
                foreach (var card in root.GetAllCardsInStack())
                {
                    string eff = Effects(card);
                    if (eff != "") e.Add(new ListBrowser.Entry(TextCleaner.Join(CardInfo.Name(card), eff)));
                    int i = 0;
                    foreach (var c in ConnectorsOf(card))
                    {
                        var connector = c;
                        var owner = card;
                        int line = e.Count;
                        e.Add(new ListBrowser.Entry(ConnectorLine(owner, connector))
                        {
                            OnActivate = () =>
                            {
                                if (connector.ConnectedNode != null) { Disconnect(connector); Open(list, board, root, PageCard, line, speak: false); }
                                else ChooseTarget(root, owner, connector);
                            },
                            KeepOpen = true,
                            Info = () => Loc.T("conn_help_" + connector.ConnectionType.ToString().ToLowerInvariant()),
                        });
                        i++;
                    }
                }
            }
            string empty = WorldManager.instance != null && WorldManager.instance.CurrentBoard != null && WorldManager.instance.CurrentBoard.Id != "cities" && !WorldManager.instance.CanUseTransport
                ? Loc.T("conn_none_mainland") : Loc.T("conn_none");
            if (e.Count == 0) e.Add(new ListBrowser.Entry(empty));
            return new ListBrowser.Page(Loc.T("conn_page_card", title), e);
        }

        private static ListBrowser.Page NetworkPage()
        {
            var e = new List<ListBrowser.Entry>();
            foreach (var c in AllConnectors().Where(x => x.CardDirection == CardDirection.output && x.ConnectedNode != null))
            {
                var from = c.Parent;
                var to = c.GetConnectedGameCard();
                if (to == null) continue;
                e.Add(new ListBrowser.Entry(Loc.T("conn_link", CardInfo.Name(from), CardInfo.Name(to), TypeName(c.ConnectionType), RelativePosition(from, to)))
                {
                    OnActivate = () => board?.FocusStack(to),
                    Info = () => TextCleaner.Join(CardInfo.Name(to), Effects(to)),
                });
            }
            return new ListBrowser.Page(Loc.T("conn_page_network"), e, Loc.T("conn_no_links"));
        }

        private static ListBrowser.Page ProblemsPage()
        {
            var e = new List<ListBrowser.Entry>();
            var wm = WorldManager.instance;
            if (wm != null)
            {
                foreach (var card in wm.AllCards)
                {
                    if (card == null || card.Destroyed || card.CardData == null || card.MyBoard != wm.CurrentBoard) continue;
                    var fx = card.CardData.StatusEffects;
                    if (fx == null || !fx.Any(x => x is StatusEffect_NoEnergy || x is StatusEffect_NoSewer)) continue;
                    var target = card;
                    e.Add(new ListBrowser.Entry(TextCleaner.Join(CardInfo.Name(card), Effects(card)))
                    {
                        OnActivate = () => board?.FocusStack(target),
                    });
                }
            }
            return new ListBrowser.Page(Loc.T("conn_page_problems"), e, Loc.T("conn_no_problems"));
        }

        /// <summary>Second list: the free connectors this one can be linked to, nearest first.</summary>
        private static void ChooseTarget(GameCard root, GameCard owner, CardConnector from)
        {
            var targets = AllConnectors()
                .Where(t => t.Parent != from.Parent && t.ConnectionType == from.ConnectionType && t.CardDirection != from.CardDirection && t.ConnectedNode == null)
                .OrderBy(t => Vector3.Distance(t.Parent.transform.position, owner.transform.position))
                .ToList();
            var e = new List<ListBrowser.Entry>();
            foreach (var t in targets)
            {
                var target = t;
                e.Add(new ListBrowser.Entry(TextCleaner.Join(CardInfo.Name(target.Parent), RelativePosition(owner, target.Parent), Effects(target.Parent)))
                {
                    OnActivate = () =>
                    {
                        bool ok = Connect(from, target);
                        Speech.Say(ok ? Loc.T("conn_done", CardInfo.Name(owner), CardInfo.Name(target.Parent), TypeName(from.ConnectionType)) : Loc.T("conn_failed"));
                        Open(list, board, root, PageCard, 0, speak: false);
                    },
                });
            }
            string name = TextCleaner.Join(TypeName(from.ConnectionType), DirName(from));
            list.OpenPagesAt(new List<ListBrowser.Page> { new ListBrowser.Page(Loc.T("conn_page_targets", name), e, Loc.T("conn_no_target")) }, 0, 0);
        }

        /// <summary>Same path as the mouse: start a cable on one connector, end it on the other.</summary>
        private static bool Connect(CardConnector a, CardConnector b)
        {
            var cm = CitiesManager.instance;
            if (cm == null || a == null || b == null) return false;
            try
            {
                cm.StartDrawCable(a);
                cm.StopDrawCable(b);
            }
            catch (System.Exception ex) { Log.Once(ex); return false; }
            return a.ConnectedNode == b || Together.IsClient;
        }

        private static void Disconnect(CardConnector a)
        {
            var cm = CitiesManager.instance;
            if (cm == null || a == null) return;
            var other = a.GetConnectedGameCard();
            try
            {
                cm.StartDrawCable(a);
                a.SetConnectedNode(null);
                cm.StopDrawCable(null);
                a.Parent?.CardData?.NotifyEnergyConsumers();
                other?.CardData?.NotifyEnergyConsumers();
            }
            catch (System.Exception ex) { Log.Once(ex); }
            Speech.Say(Loc.T("conn_removed", TypeName(a.ConnectionType), other != null ? CardInfo.Name(other) : ""));
        }
    }
}
