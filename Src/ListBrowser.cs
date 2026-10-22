using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StacklandAccessNS
{
    /// <summary>
    /// A spoken list navigated with the arrows. It can have several pages (left/right arrows switch page),
    /// and entries can have an action (Enter) and extra information (I).
    /// </summary>
    public class ListBrowser
    {
        public class Entry
        {
            public string Title;
            public string Detail;
            /// <summary>Action run by Enter (the list closes first unless KeepOpen).</summary>
            public System.Action OnActivate;
            public bool KeepOpen;
            /// <summary>Text read by I (computed when asked).</summary>
            public System.Func<string> Info;
            public Entry(string title, string detail = null) { Title = title; Detail = detail; }
        }

        public class Page
        {
            public string Name;
            public List<Entry> Entries;
            public string EmptyText;
            public Page(string name, List<Entry> entries, string emptyText = null) { Name = name; Entries = entries ?? new List<Entry>(); EmptyText = emptyText; }
        }

        private List<Page> pages = new List<Page>();
        private int pageIndex;
        private int index;
        /// <summary>Key binding (see <see cref="Bindings"/>) that closes the list: the one that opened it.</summary>
        private string closeAction;

        /// <summary>The key binding that opened the list (for the visual menu's position).</summary>
        public string Source => closeAction;

        /// <summary>Changes each time what the list shows changes (for the visual menu).</summary>
        public int Version { get; private set; }

        public IReadOnlyList<Page> Pages => pages;

        private List<Entry> entries => pages[pageIndex].Entries;

        public bool IsOpen { get; private set; }

        public void Open(string listName, List<Entry> list, string toggleAction, int startIndex = 0, bool briefIntro = false)
        {
            closeAction = toggleAction;
            OpenPages(new List<Page> { new Page(listName, list) }, startIndex, briefIntro);
        }

        public void OpenPages(List<Page> newPages, string toggleAction, int startIndex = 0, bool briefIntro = false)
        {
            closeAction = toggleAction;
            OpenPages(newPages, startIndex, briefIntro);
        }

        public int PageIndex => pageIndex;
        public int Index => index;

        /// <summary>Opens (or refreshes) the pages at a given page and line, reading only that line.</summary>
        /// <summary>Run by Escape instead of closing (a list of choices goes back to the list it came from).</summary>
        private System.Action onBack;

        public void OpenPagesAt(List<Page> newPages, int page, int line, bool speak = true, string toggleAction = null, System.Action onBack = null)
        {
            closeAction = toggleAction;
            this.onBack = onBack;
            Version++;
            pages = newPages.Where(p => p != null).ToList();
            if (pages.Count == 0) { IsOpen = false; return; }
            IsOpen = true;
            pageIndex = Mathf.Clamp(page, 0, pages.Count - 1);
            index = Mathf.Clamp(line, 0, Mathf.Max(0, entries.Count - 1));
            if (speak) Speech.Say(TextCleaner.Join(Loc.N("page_name", entries.Count, pages[pageIndex].Name, entries.Count), CurrentLine()));
        }

        private void OpenPages(List<Page> newPages, int startIndex, bool brief)
        {
            onBack = null;
            Version++;
            pages = newPages.Where(p => p != null).ToList();
            if (pages.Count == 0 || (pages.Count == 1 && pages[0].Entries.Count == 0))
            {
                IsOpen = false;
                Speech.Say(pages.Count > 0 && pages[0].EmptyText != null ? pages[0].EmptyText : Loc.T("list_empty", pages.Count > 0 ? pages[0].Name : ""));
                return;
            }
            IsOpen = true;
            pageIndex = 0;
            index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, entries.Count - 1));
            string introKey = pages.Count > 1 ? "list_open_pages" : "list_open";
            // The full instructions are given the first times only.
            string hint = Loc.Hint(introKey);
            string intro = brief ? "" : (hint != "" ? Loc.N(introKey, entries.Count, pages[0].Name, entries.Count) : Loc.N("list_open_short", entries.Count, pages[0].Name, entries.Count));
            if (brief && pages.Count > 1) intro = Loc.T("page_hint");
            Speech.Say(TextCleaner.Join(intro, CurrentLine()));
        }

        public void Close(bool announce = true)
        {
            if (!IsOpen) return;
            IsOpen = false;
            Version++;
            if (announce) Speech.Say(Loc.T("list_closed"));
        }

        private string CurrentLine()
        {
            var page = pages[pageIndex];
            if (page.Entries.Count == 0) return page.EmptyText ?? Loc.T("list_empty", page.Name);
            return TextCleaner.Join(entries[index].Title, Loc.T("of", index + 1, entries.Count));
        }

        private void ChangePage(int dir)
        {
            if (pages.Count < 2) { Speech.Say(CurrentLine()); return; }
            int np = Mathf.Clamp(pageIndex + dir, 0, pages.Count - 1);
            pageIndex = np;
            index = 0;
            Version++;
            var page = pages[pageIndex];
            Speech.Say(TextCleaner.Join(Loc.N("page_name", page.Entries.Count, page.Name, page.Entries.Count), CurrentLine()));
        }

        public void HandleInput()
        {
            if (!IsOpen) return;
            if (Keys.Down(Key.Escape) && onBack != null) { var back = onBack; onBack = null; back(); return; }
            if (Keys.Down(Key.Escape) || (closeAction != null && Bindings.Pressed(closeAction))) { Close(); return; }
            if (Keys.Down(Key.RightArrow)) { ChangePage(+1); return; }
            if (Keys.Down(Key.LeftArrow)) { ChangePage(-1); return; }
            if (VisualMenu.TakeClick(out int clicked, out int pageMove))
            {
                if (pageMove != 0) { ChangePage(pageMove); return; }
                if (clicked < 0)
                {
                    if (onBack != null) { var back = onBack; onBack = null; back(); }
                    else Close();
                    return;
                }
                if (clicked < entries.Count) { index = clicked; Version++; Activate(); }
                return;
            }
            if (entries.Count == 0) { if (Keys.EnterDown || Keys.Down(Key.UpArrow) || Keys.Down(Key.DownArrow)) Speech.Say(CurrentLine()); return; }
            if (Keys.Repeat(Key.DownArrow)) index++;
            else if (Keys.Repeat(Key.UpArrow)) index--;
            else if (Keys.Down(Key.Home)) index = 0;
            else if (Keys.Down(Key.End)) index = entries.Count - 1;
            else if (Keys.Down(Key.PageDown)) index += 10;
            else if (Keys.Down(Key.PageUp)) index -= 10;
            else if (Keys.EnterDown) { Activate(); return; }
            else if (Bindings.Pressed("list_info") && (entries[index].Info != null || !string.IsNullOrWhiteSpace(entries[index].Detail)))
            {
                var e = entries[index];
                string info = e.Info != null ? e.Info() : e.Detail;
                Speech.Say(string.IsNullOrWhiteSpace(info) ? e.Title : info);
                return;
            }
            else return;
            index = Mathf.Clamp(index, 0, entries.Count - 1);
            Version++;
            Speech.Say(CurrentLine());
        }

        private void Activate()
        {
            var e = entries[index];
            if (e.OnActivate != null)
            {
                if (!e.KeepOpen) { IsOpen = false; Version++; }
                e.OnActivate();
            }
            else Speech.Say(string.IsNullOrWhiteSpace(e.Detail) ? e.Title : TextCleaner.Join(e.Title, e.Detail));
        }

        // ------------------------------------------------------------------ list builders

        public static List<Entry> Quests(out int startIndex)
        {
            startIndex = 0;
            var list = new List<Entry>();
            var qm = QuestManager.instance;
            var gs = GameScreen.instance;
            if (qm == null || gs == null) return list;
            // GameScreen already filters the quests for the current board; reuse its elements.
            var quests = gs.questElements != null
                ? gs.questElements.Where(e => e != null && e.MyQuest != null).Select(e => e.MyQuest).ToList()
                : new List<Quest>();
            var todo = quests.Where(q => !qm.QuestIsComplete(q) && SafeVisible(qm, q)).ToList();
            var done = quests.Where(q => qm.QuestIsComplete(q)).ToList();
            foreach (var q in todo) list.Add(new Entry(TextCleaner.Join(TextCleaner.Clean(q.Description), Loc.T("quest_todo")), GroupName(q)));
            foreach (var q in done) list.Add(new Entry(TextCleaner.Join(TextCleaner.Clean(q.Description), Loc.T("quest_done")), GroupName(q)));
            if (quests.Count > 0)
            {
                list.Insert(0, new Entry(Loc.N("quests_summary", done.Count, done.Count, quests.Count), NextPackText()));
                startIndex = todo.Count > 0 ? 1 : 0;
            }
            return list;
        }

        private static bool SafeVisible(QuestManager qm, Quest q)
        {
            try { return qm.QuestIsVisible(q); } catch (System.Exception ex) { Log.Once(ex); return true; }
        }

        private static string GroupName(Quest q)
        {
            string term = "questgroup_" + q.QuestGroup.ToString().ToLowerInvariant();
            string s = SokLoc.Translate(term);
            return s == null || s.Contains("questgroup_") || s.StartsWith("---") ? "" : TextCleaner.Clean(s);
        }

        private static string NextPackText()
        {
            try
            {
                var gs = GameScreen.instance;
                if (gs != null && gs.NextPackToUnlockText != null && gs.NextPackToUnlockText.gameObject.activeInHierarchy)
                    return TextCleaner.Clean(gs.NextPackToUnlockText.text);
            }
            catch (System.Exception ex) { Log.Once(ex); }
            return "";
        }

        public static List<Entry> Ideas()
        {
            var list = new List<Entry>();
            var wm = WorldManager.instance;
            if (wm == null) return list;
            bool cities = wm.CurrentBoard != null && wm.CurrentBoard.Id == "cities";
            var known = new List<IKnowledge>();
            try
            {
                known.AddRange(wm.BlueprintPrefabs.Where(b => !b.HideFromIdeasTab).Cast<IKnowledge>());
                known.AddRange(wm.CardDataPrefabs.OfType<Rumor>().Cast<IKnowledge>());
            }
            catch (System.Exception ex) { Log.Once(ex); }
            foreach (var k in known
                .Where(k => wm.HasFoundCard(k.CardId))
                .Where(k =>
                {
                    var c = wm.GameDataLoader.GetCardFromId(k.CardId);
                    return c != null && ((c.CardUpdateType == CardUpdateType.Cities) == cities);
                })
                .OrderBy(k => k.Group.ToString()).ThenBy(k => k.KnowledgeName))
            {
                string text = "";
                try { text = k.KnowledgeText; } catch (System.Exception ex) { Log.Once(ex); }
                list.Add(new Entry(TextCleaner.Clean(k.KnowledgeName), TextCleaner.Clean(text)));
            }
            return list;
        }

        /// <summary>Ctrl+P: every villager with what it is doing. Enter goes to it on the board.</summary>
        public static List<Entry> Villagers(BoardNavigator board)
        {
            var list = new List<(string name, bool idle, Entry e)>();
            var wm = WorldManager.instance;
            if (wm == null) return new List<Entry>();
            foreach (var c in wm.AllCards)
            {
                if (c == null || c.Destroyed || c.CardData == null || c.MyBoard == null || !c.MyBoard.IsCurrent) continue;
                if (c.CardData.MyCardType != CardType.Humans) continue;
                string occupation;
                bool idle = false;
                var root = c.GetRootCard();
                if (c.IsWorking && c.WorkerHolder != null) occupation = Loc.T("vill_working_in", CardInfo.Name(c.WorkerHolder));
                else if (c.GetCardInCombatInStack() != null || c.InConflict) occupation = Loc.T("in_combat");
                else
                {
                    string status = CardInfo.StackStatus(root);
                    var stack = root.GetAllCardsInStack();
                    var others = stack.Where(x => x != c).ToList();
                    if (status != "") occupation = TextCleaner.Join(status, others.Count > 0 ? Loc.T("vill_with", CardInfo.Summary(others)) : "");
                    else if (others.Count > 0) occupation = Loc.T("vill_waiting", CardInfo.Summary(others));
                    else { occupation = Loc.T("vill_idle"); idle = true; }
                }
                string name = CardInfo.Name(c);
                var card = c;
                list.Add((name, idle, new Entry(TextCleaner.Join(name, CardInfo.HealthShort(c.CardData), occupation))
                {
                    OnActivate = () => { board.FocusStack(card.IsWorking && card.WorkerHolder != null ? card.WorkerHolder : card); board.AnnounceCurrent(); },
                    Info = () => CardInfo.Details(card),
                }));
            }
            // Idle villagers first: they are the ones waiting for a job.
            return list.OrderBy(x => x.idle ? 0 : 1).ThenBy(x => x.name, System.StringComparer.CurrentCultureIgnoreCase).Select(x => x.e).ToList();
        }

        public static List<Entry> History()
        {
            return EventNarrator.History.AsEnumerable().Reverse().Select(h => new Entry(h)).ToList();
        }

        public static List<Entry> Help()
        {
            var list = Loc.T("help_lines").Split('|').Select(l => new Entry(l)).ToList();
            list.Add(new Entry(Loc.T("help_conn")));
            list.Add(new Entry(Loc.T("help_export")));
            list.Add(new Entry(Loc.T("help_options")));
            list.Add(new Entry(Loc.T("help_pad")));
            if (Together.Installed) list.Add(new Entry(Loc.T("help_mp")));
            return list;
        }
    }
}
