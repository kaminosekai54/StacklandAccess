using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Watches the game state and speaks what happens: new cards, enemies, combat, recipes starting,
    /// cutscene texts, game speed, end of moon warning, card limit. Everything goes to the history (N key).
    /// </summary>
    public class EventNarrator
    {
        public static readonly List<string> History = new List<string>();

        public static void Announce(string text, bool important = false)
        {
            text = TextCleaner.Clean(text);
            if (string.IsNullOrWhiteSpace(text)) return;
            History.Add(text);
            if (History.Count > 150) History.RemoveAt(0);
            if (important) Speech.SayImportant(text);
            else Speech.Say(text, interrupt: false);
        }

        public static void OnAttack(Combatable attacker, Combatable target)
        {
            if (!AccessController.AnnounceCombat || !AccessController.AnnounceHits || attacker == null || target == null) return;
            if (attacker.MyGameCard == null || attacker.MyGameCard.MyBoard == null || !attacker.MyGameCard.MyBoard.IsCurrent) return;
            string a = CardInfo.Name(attacker.MyGameCard);
            string t = target.MyGameCard != null ? CardInfo.Name(target.MyGameCard) : "";
            string text;
            if (!attacker.AttackIsHit) text = Loc.T("attack_miss", a, t);
            else if (target.HealthPoints <= 0) text = Loc.T("attack_kill", a, t);
            else text = Loc.T("attack_hit", a, t, target.HealthPoints, target.ProcessedCombatStats.MaxHealth);
            Speech.Say(text, interrupt: false);
        }

        /// <summary>Multiplayer client: a hit replayed from the host.</summary>
        public static void OnRemoteAttack(Combatable attacker, Combatable target, bool isHit, int damage)
        {
            if (!AccessController.AnnounceCombat || !AccessController.AnnounceHits || attacker == null || target == null) return;
            if (attacker.MyGameCard == null || attacker.MyGameCard.MyBoard == null || !attacker.MyGameCard.MyBoard.IsCurrent) return;
            string a = CardInfo.Name(attacker.MyGameCard);
            string t = target.MyGameCard != null ? CardInfo.Name(target.MyGameCard) : "";
            string text;
            // Health comes from the host's frequent snapshots, which usually arrive after this hit: deduct it.
            int hp = Mathf.Max(0, target.HealthPoints - Mathf.Max(0, damage));
            if (!isHit) text = Loc.T("attack_miss", a, t);
            else if (hp <= 0) text = Loc.T("attack_kill", a, t);
            else text = Loc.T("attack_hit", a, t, hp, target.ProcessedCombatStats.MaxHealth);
            Speech.Say(text, interrupt: false);
        }

        private string boardId;
        private readonly Dictionary<int, GameCard> known = new Dictionary<int, GameCard>();
        private readonly Dictionary<int, string> knownNames = new Dictionary<int, string>();
        private readonly Dictionary<int, CardData> knownData = new Dictionary<int, CardData>();
        private readonly List<GameCard> pendingNew = new List<GameCard>();
        private readonly List<string> pendingLost = new List<string>();
        private readonly List<string> pendingDefeated = new List<string>();
        private float lastPendingTime;
        private float scanTimer;
        private readonly HashSet<int> inConflict = new HashSet<int>();
        private float lastConflictAnnounce = -100f;

        private string lastCutTitle, lastCutText, lastContinue;
        private float lastSpeed = -1f;
        private int warnedMonth = -1;
        private bool overCap;

        public void ResetBaseline()
        {
            known.Clear(); knownNames.Clear(); knownData.Clear();
            pendingNew.Clear(); pendingLost.Clear(); pendingDefeated.Clear();
            inConflict.Clear();
            effectsSeen.Clear();
            watched.Clear();
            villagerBusy.Clear();
            var wm = WorldManager.instance;
            if (wm == null) return;
            foreach (var c in wm.AllCards) Remember(c);
        }

        private void Remember(GameCard c)
        {
            if (c == null || c.Destroyed || c.CardData == null || c.MyBoard == null || !c.MyBoard.IsCurrent) return;
            int id = c.GetInstanceID();
            known[id] = c;
            knownNames[id] = CardInfo.Name(c);
            knownData[id] = c.CardData;
        }

        public void Update()
        {
            var wm = WorldManager.instance;
            var gc = GameCanvas.instance;
            if (wm == null || gc == null) return;

            bool inGame = gc.CurrentScreen is GameScreen || gc.CurrentScreen is CutsceneScreen;
            if (!inGame || wm.CurrentBoard == null)
            {
                boardId = null;
                return;
            }

            if (boardId != wm.CurrentBoard.Id)
            {
                bool first = boardId == null;
                boardId = wm.CurrentBoard.Id;
                ResetBaseline();
                lastSpeed = wm.SpeedUp;
                if (!first) Announce(Loc.T("board_name", BoardName(boardId)));
                return;
            }

            CheckCutscene(wm, gc);
            if (!(gc.CurrentScreen is GameScreen) && !wm.RemovingCards)
            {
                // During cutscene animations cards appear/disappear in bulk; keep the baseline fresh.
                scanTimer -= Time.unscaledDeltaTime;
                if (scanTimer <= 0f) { scanTimer = 0.3f; ScanCards(wm, announce: wm.CutsceneBoardView); }
                FlushPending(force: false);
                return;
            }

            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.25f;
                ScanCards(wm, announce: true);
                CheckConflicts(wm);
                CheckEffects(wm);
                CheckIdleVillagers(wm);
                CheckCardCap(wm);
            }
            FlushPending(force: false);
            CheckDropResult();
            CheckRemoteDrops();
            CheckWatchedActions();
            CheckView(wm);
            CheckSpeed(wm);
            CheckMoon(wm);
        }

        public static string BoardName(string id)
        {
            string key = "b_" + id;
            string s = Loc.T(key);
            return s == key ? id : s;
        }

        // ------------------------------------------------------------------ cards

        private void ScanCards(WorldManager wm, bool announce)
        {
            var seen = new HashSet<int>();
            var fresh = new List<GameCard>();
            foreach (var c in wm.AllCards)
            {
                if (c == null || c.Destroyed || c.CardData == null || c.MyBoard == null || !c.MyBoard.IsCurrent) continue;
                int id = c.GetInstanceID();
                seen.Add(id);
                if (!known.ContainsKey(id)) fresh.Add(c);
                else knownNames[id] = CardInfo.Name(c);
            }
            var gone = known.Keys.Where(id => !seen.Contains(id)).ToList();

            // Loading a save or a big reshuffle: do not read 40 cards.
            bool bulk = fresh.Count > 20 || gone.Count > 20 || Together.Quiet;

            foreach (var id in gone)
            {
                if (announce && !bulk && knownData.TryGetValue(id, out var data) && data != null)
                {
                    // CardData may already be destroyed by Unity: use the stored name.
                    string n = knownNames.TryGetValue(id, out var nm) ? nm : "";
                    bool isEnemy = false, isHuman = false;
                    try { isEnemy = data is Enemy; isHuman = data.MyCardType == CardType.Humans; } catch (System.Exception ex) { Log.Once(ex); }
                    if (isEnemy) pendingDefeated.Add(n);
                    else if (isHuman && AccessController.AnnounceNewCards) pendingLost.Add(n);
                    lastPendingTime = Time.unscaledTime;
                }
                known.Remove(id); knownNames.Remove(id); knownData.Remove(id);
            }
            foreach (var c in fresh)
            {
                Remember(c);
                if (announce && !bulk)
                {
                    pendingNew.Add(c);
                    lastPendingTime = Time.unscaledTime;
                }
            }
        }

        private void FlushPending(bool force)
        {
            if (pendingNew.Count == 0 && pendingLost.Count == 0 && pendingDefeated.Count == 0) return;
            if (!force && Time.unscaledTime - lastPendingTime < 0.45f) return;

            var enemies = new List<string>();
            var ideas = new List<string>();
            var others = new List<string>();
            foreach (var c in pendingNew)
            {
                if (c == null || c.Destroyed || c.CardData == null) continue;
                string n = CardInfo.Name(c);
                if (c.CardData is StrangePortal) Announce(Loc.T("new_portal", n), true);
                else if (c.CardData is IndustrialRevolution || c.CardData.Id == "event_industrial_revolution") Announce(Loc.T("new_city_gate", n), true);
                else if (c.CardData is Enemy) enemies.Add(n);
                else if (c.CardData.MyCardType == CardType.Ideas || c.CardData.MyCardType == CardType.Rumors) ideas.Add(n);
                else others.Add(n);
            }
            if (enemies.Count > 0) Announce(Loc.T("new_enemy", Group(enemies)), true);
            if (ideas.Count > 0) Announce(Loc.T("new_idea", Group(ideas)), true);
            if (others.Count > 0 && AccessController.AnnounceNewCards) Announce(Loc.T("new_card", Group(others)));
            if (pendingDefeated.Count > 0 && AccessController.AnnounceCombat) Announce(Loc.T("defeated", Group(pendingDefeated)));
            if (pendingLost.Count > 0) Announce(Loc.T("lost_card", Group(pendingLost)), true);
            pendingNew.Clear(); pendingLost.Clear(); pendingDefeated.Clear();
        }

        /// <summary>"Wood, Wood, Stone" -> "2 Wood, Stone".</summary>
        private static string Group(List<string> names)
        {
            return string.Join(", ", names.GroupBy(n => n).Select(g => g.Count() > 1 ? g.Count() + " " + g.Key : g.Key));
        }

        private void CheckConflicts(WorldManager wm)
        {
            if (!AccessController.AnnounceCombat) return;
            var now = new HashSet<int>();
            var newOnes = new List<GameCard>();
            foreach (var c in wm.AllCards)
            {
                if (c == null || c.Destroyed || c.MyBoard == null || !c.MyBoard.IsCurrent) continue;
                bool fighting;
                try { fighting = c.InConflict; } catch (System.Exception ex) { Log.Once(ex); fighting = false; }
                if (!fighting) continue;
                int id = c.GetInstanceID();
                now.Add(id);
                if (!inConflict.Contains(id)) newOnes.Add(c);
            }
            bool ended = inConflict.Count > 0 && now.Count == 0;
            inConflict.Clear();
            foreach (var id in now) inConflict.Add(id);
            if (ended) Announce(Loc.T("combat_end"), true);
            if (newOnes.Count > 0 && Time.unscaledTime - lastConflictAnnounce > 1.5f)
            {
                lastConflictAnnounce = Time.unscaledTime;
                Announce(Loc.T("combat_start", Group(newOnes.Select(CardInfo.Name).ToList())), true);
            }
        }

        private void CheckCardCap(WorldManager wm)
        {
            int count, max;
            try { count = wm.GetCardCount(); max = wm.GetMaxCardCount(); } catch (System.Exception ex) { Log.Once(ex); return; }
            bool over = count > max;
            if (over && !overCap) Announce(Loc.T("too_many_cards", count, max), true);
            overCap = over;
        }

        // ------------------------------------------------------------------ player feedback

        private void CheckDropResult()
        {
            var root = BoardNavigator.LastDropRoot;
            if (root == null) return;
            float since = Time.unscaledTime - BoardNavigator.LastDropTime;
            if (root.Destroyed) { BoardNavigator.LastDropRoot = null; return; }
            var card = root.GetRootCard().GetCardWithStatusInStack();
            if (card != null && card.TimerRunning && !string.IsNullOrEmpty(card.Status))
            {
                BoardNavigator.LastDropRoot = null;
                watched.Add(new Watched { Card = card, Root = root.GetRootCard(), Status = TextCleaner.Clean(card.Status) });
                if (AccessController.AnnounceActions)
                {
                    float total = card.TargetTimerTime - card.CurrentTimerTime;
                    Announce(Loc.T("action_started", CardInfo.Name(root.GetRootCard()), TextCleaner.Clean(card.Status), CardInfo.Seconds(total)));
                }
                return;
            }
            if (since > 1.2f) BoardNavigator.LastDropRoot = null;
        }

        private class Watched { public GameCard Card; public GameCard Root; public string Status; public float LastTime; public string Who; }

        private class RemoteDrop { public GameCard Root; public float Time; public string Who; }
        private static readonly List<RemoteDrop> remoteDrops = new List<RemoteDrop>();

        /// <summary>Multiplayer host: another player dropped cards on this pile; announce the craft it starts.</summary>
        public static void WatchRemoteDrop(GameCard root, string who)
        {
            if (root == null) return;
            remoteDrops.RemoveAll(d => d.Root == root);
            remoteDrops.Add(new RemoteDrop { Root = root, Time = UnityEngine.Time.unscaledTime, Who = who });
        }

        private void CheckRemoteDrops()
        {
            for (int i = remoteDrops.Count - 1; i >= 0; i--)
            {
                var d = remoteDrops[i];
                if (d.Root == null || d.Root.Destroyed || Time.unscaledTime - d.Time > 1.5f) { remoteDrops.RemoveAt(i); continue; }
                var root = d.Root.GetRootCard();
                var card = root.GetCardWithStatusInStack();
                if (card == null || !card.TimerRunning || string.IsNullOrEmpty(card.Status)) continue;
                remoteDrops.RemoveAt(i);
                if (watched.Any(w => w.Card == card)) continue;
                string status = TextCleaner.Clean(card.Status);
                watched.Add(new Watched { Card = card, Root = root, Status = status, Who = d.Who });
                if (AccessController.AnnounceActions)
                {
                    float total = card.TargetTimerTime - card.CurrentTimerTime;
                    Announce(Loc.T("mp_by", d.Who, Loc.T("action_started", CardInfo.Name(root), status, CardInfo.Seconds(total))));
                }
            }
        }
        private readonly List<Watched> watched = new List<Watched>();

        /// <summary>Recipes started by the player: say when they are finished.</summary>
        private void CheckWatchedActions()
        {
            for (int i = watched.Count - 1; i >= 0; i--)
            {
                var w = watched[i];
                bool done = w.Card == null || w.Card.Destroyed || !w.Card.TimerRunning || TextCleaner.Clean(w.Card.Status) != w.Status
                            || w.Card.CurrentTimerTime + 0.01f < w.LastTime; // timer restarted: one cycle done
                if (!done) { w.LastTime = w.Card.CurrentTimerTime; continue; }
                watched.RemoveAt(i);
                if (AccessController.AnnounceActions)
                {
                    string who = w.Root != null && !w.Root.Destroyed ? CardInfo.Name(w.Root) : (w.Card != null && !w.Card.Destroyed ? CardInfo.Name(w.Card) : "");
                    FlushPending(force: true);
                    string doneText = Loc.T("action_done", w.Status, who);
                    Announce(w.Who != null ? Loc.T("mp_by", w.Who, doneText) : doneText);
                }
            }
        }

        private static readonly HashSet<string> Empty = new HashSet<string>();
        private readonly Dictionary<int, HashSet<string>> effectsSeen = new Dictionary<int, HashSet<string>>();

        /// <summary>New status effects on cards (poisoned, sick, on fire, well fed...).</summary>
        private void CheckEffects(WorldManager wm)
        {
            var alive = new HashSet<int>();
            foreach (var c in wm.AllCards)
            {
                if (c == null || c.Destroyed || c.CardData == null || c.MyBoard == null || !c.MyBoard.IsCurrent) continue;
                int id = c.GetInstanceID();
                alive.Add(id);
                bool hasEffects = c.CardData.StatusEffects != null && c.CardData.StatusEffects.Count > 0;
                if (!hasEffects && !effectsSeen.ContainsKey(id)) { effectsSeen[id] = Empty; continue; }
                if (!hasEffects) { effectsSeen[id] = Empty; continue; }
                var now = new HashSet<string>();
                if (c.CardData.StatusEffects != null)
                    foreach (var e in c.CardData.StatusEffects)
                        if (!CardInfo.IsQuietEffect(e)) now.Add(e.GetType().Name);
                if (effectsSeen.TryGetValue(id, out var before))
                {
                    foreach (var e in c.CardData.StatusEffects)
                    {
                        if (CardInfo.IsQuietEffect(e) || before.Contains(e.GetType().Name)) continue;
                        string n = "";
                        try { n = TextCleaner.Clean(e.Name); } catch (System.Exception ex) { Log.Once(ex); }
                        if (n != "") Announce(Loc.T("effect_new", CardInfo.Name(c), n), true);
                    }
                }
                effectsSeen[id] = now;
            }
            foreach (var id in effectsSeen.Keys.Where(k => !alive.Contains(k)).ToList()) effectsSeen.Remove(id);
        }

        private readonly Dictionary<int, bool> villagerBusy = new Dictionary<int, bool>();

        /// <summary>A villager is busy when its pile has a recipe running, it fights or works in a building.</summary>
        public static bool VillagerBusy(GameCard c)
        {
            if (c.IsWorking) return true;
            if (c.InConflict || c.GetCardInCombatInStack() != null) return true;
            return Recipes.IsBusy(c.GetRootCard());
        }

        /// <summary>Beep when a villager finishes its job and becomes free.</summary>
        private void CheckIdleVillagers(WorldManager wm)
        {
            var seen = new HashSet<int>();
            var freed = new List<string>();
            foreach (var c in wm.AllCards)
            {
                if (c == null || c.Destroyed || c.CardData == null || c.MyBoard == null || !c.MyBoard.IsCurrent) continue;
                if (!(c.CardData is BaseVillager)) continue;
                int id = c.GetInstanceID();
                seen.Add(id);
                bool busy = VillagerBusy(c);
                if (villagerBusy.TryGetValue(id, out bool was) && was && !busy) freed.Add(CardInfo.Name(c));
                villagerBusy[id] = busy;
            }
            foreach (var id in villagerBusy.Keys.Where(k => !seen.Contains(k)).ToList()) villagerBusy.Remove(id);
            if (freed.Count > 0 && AccessController.BeepIdleVillager)
            {
                Beeper.Play();
                History.Add(Loc.T("villager_free", Group(freed)));
            }
        }

        private ViewType lastView = ViewType.Default;

        private void CheckView(WorldManager wm)
        {
            if (wm.CurrentView == lastView) return;
            lastView = wm.CurrentView;
            Speech.Say(Loc.T("view", Loc.T("view_" + lastView)));
        }

        private void CheckSpeed(WorldManager wm)
        {
            float s = wm.SpeedUp;
            if (Mathf.Approximately(s, lastSpeed)) return;
            float prev = lastSpeed;
            lastSpeed = s;
            if (prev < 0f) return;
            // The game pauses on its own while a menu is open; only speak real changes on the board.
            if (!(GameCanvas.instance.CurrentScreen is GameScreen) || GameCanvas.instance.ModalIsOpen) return;
            Speech.Say(s <= 0f ? Loc.T("speed_pause") : Loc.T("speed", "x" + s.ToString("0.#")));
        }

        private void CheckMoon(WorldManager wm)
        {
            int warn = AccessController.MoonWarningSeconds;
            if (warn <= 0 || wm.ForestMoonEnabled || !wm.IsPlaying) return;
            try { if (wm.CurrentRunOptions != null && wm.CurrentRunOptions.IsPeacefulMode) return; } catch (System.Exception ex) { Log.Once(ex); }
            float left = wm.MonthTime - wm.MonthTimer;
            if (left <= warn && left > 0f && warnedMonth != wm.CurrentMonth)
            {
                warnedMonth = wm.CurrentMonth;
                Announce(Loc.T("moon_soon", Mathf.CeilToInt(left)), true);
            }
        }

        // ------------------------------------------------------------------ cutscenes

        private void CheckCutscene(WorldManager wm, GameCanvas gc)
        {
            if (!(gc.CurrentScreen is CutsceneScreen cs))
            {
                lastCutTitle = lastCutText = lastContinue = null;
                return;
            }
            string title = wm.CutsceneTitle ?? "";
            string text = wm.CutsceneText ?? "";
            string cont = cs.ContinueButton != null && cs.ContinueButton.gameObject.activeInHierarchy ? (wm.ContinueButtonText ?? "") : "";
            if (title == lastCutTitle && text == lastCutText && cont == lastContinue) return;
            var parts = new List<string>();
            if (title != lastCutTitle) parts.Add(title);
            if (text != lastCutText) parts.Add(text);
            if (cont != lastContinue && cont != "") parts.Add(Loc.T("continue_hint", cont));
            lastCutTitle = title; lastCutText = text; lastContinue = cont;
            FlushPending(force: true);
            if (wm.RemovingCards) parts.Add(Loc.T("removing_hint"));
            Announce(TextCleaner.Join(parts.ToArray()), true);
        }
    }
}
