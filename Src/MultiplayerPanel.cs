using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Spoken replacement for the Stacklands Together window (drawn with IMGUI, invisible to screen readers).
    /// Opens with F8 or the "Multiplayer" button of the main menu, like the original window.
    /// </summary>
    public static class MultiplayerPanel
    {
        private const int PageSession = 0, PageSteam = 1, PageDirect = 2, PageTools = 3;

        private static ListBrowser list;
        private static bool shown;
        private static bool searching;
        private static float searchStart;
        private static int lobbiesBefore;
        private static string armedReset;
        private static float armedTime;

        private static object Net => Together.Net;
        private static object Ctrl => Together.Controller;
        private static object Lobbies => Together.Get(Net, "SteamLobbies");

        /// <summary>Called every frame: takes over the Together window and its password prompt.</summary>
        public static void Watch(ListBrowser l)
        {
            list = l;
            var c = Ctrl;
            if (c == null) return;
            if (shown && !l.IsOpen && !TextPrompt.Active) shown = false;

            if (Together.Bool(c, "showWindow"))
            {
                Together.Set(c, "showWindow", false);
                if (shown && l.IsOpen) { l.Close(); shown = false; }
                else Open();
            }

            if (Together.Bool(c, "showJoinPasswordPrompt") && !TextPrompt.Active)
            {
                Together.Set(c, "showJoinPasswordPrompt", false);
                ulong lobby = Together.Get(c, "pendingJoinLobbyId") is ulong id ? id : 0UL;
                AskPasswordAndJoin(lobby);
            }

            if (searching)
            {
                int count = LobbyCount();
                if ((count != lobbiesBefore && Time.unscaledTime - searchStart > 0.5f) || Time.unscaledTime - searchStart > 5f)
                {
                    searching = false;
                    Speech.Say(count == 0 ? Loc.T("mp_no_lobby") : Loc.N("mp_lobbies_found", count, count));
                    // Show the new list only if the panel is still the open list.
                    if (shown && l.IsOpen) Refresh();
                }
            }
        }

        public static void Open(int page = PageSession, int line = 0, bool speak = true)
        {
            if (list == null || !Together.Installed || Net == null) return;
            shown = true;
            list.OpenPagesAt(Pages(), page, line, speak);
            if (speak && line == 0 && page == PageSession) Speech.Say(Loc.T("mp_title"), interrupt: false);
        }

        /// <summary>Rebuilds the pages without speaking (after a change), keeping the position.</summary>
        private static void Refresh()
        {
            if (list == null) return;
            int p = list.PageIndex, i = list.Index;
            Open(p, i, speak: false);
        }

        private static ListBrowser.Entry Line(string title, Action action = null, bool keepOpen = false)
        {
            return new ListBrowser.Entry(title) { OnActivate = action, KeepOpen = keepOpen };
        }

        private static void Edit(int page, int line, string label, string current, bool hidden, Action<string> apply)
        {
            TextPrompt.Open(label, current, hidden, value =>
            {
                apply(value);
                Speech.Say(Loc.T("field_set", label, hidden ? Loc.N("edit_hidden_count", value.Length, value.Length) : (value == "" ? Loc.T("edit_empty") : value)));
                Open(page, line, speak: false);
            }, () => Open(page, line, speak: false));
        }

        private static List<ListBrowser.Page> Pages()
        {
            return new List<ListBrowser.Page> { SessionPage(), SteamPage(), DirectPage(), ToolsPage() };
        }

        // ------------------------------------------------------------------ pages

        private static ListBrowser.Page SessionPage()
        {
            var n = Net;
            var c = Ctrl;
            var e = new List<ListBrowser.Entry>();
            e.Add(Line(Loc.T("mp_state", Together.Str(n, "Status"))));
            string name = Together.Str(c, "playerName");
            e.Add(Line(Loc.T("mp_name", name == "" ? Loc.T("edit_empty") : name), () =>
                Edit(PageSession, 1, Loc.T("mp_name_label"), Together.Str(Ctrl, "playerName"), false, v =>
                {
                    if (v == "") return;
                    if (v.Length > 24) v = v.Substring(0, 24);
                    Together.Set(Ctrl, "playerName", v);
                    Together.Set(Net, "LocalName", v);
                    PlayerPrefs.SetString("StacklandsTogether.PlayerName", v);
                    PlayerPrefs.Save();
                })));
            if (Together.InSession)
            {
                foreach (var p in Together.Players())
                {
                    string who = p.you ? TextCleaner.Join(p.name, Loc.T("mp_you")) : p.name;
                    e.Add(Line(TextCleaner.Join(who, p.ping > 0 ? Loc.T("mp_ping", p.ping) : "")));
                }
                e.Add(Line(Loc.T("mp_leave"), () => Together.Call(Net, "LeaveSessionAndReturnToMenu")));
            }
            else
            {
                e.Add(Line(Loc.T("mp_offline_help")));
            }
            return new ListBrowser.Page(Loc.T("mp_page_session"), e);
        }

        private static ListBrowser.Page SteamPage()
        {
            var n = Net;
            var c = Ctrl;
            var lobbies = Lobbies;
            var e = new List<ListBrowser.Entry>();
            string transport = Together.Str(n, "ActiveTransport");
            if (!Together.Bool(lobbies, "IsAvailable"))
            {
                e.Add(Line(Loc.T("mp_steam_off")));
            }
            else if (!Together.InSession)
            {
                string lobbyName = Together.Str(c, "lobbyName");
                e.Add(Line(Loc.T("mp_lobby_name", lobbyName == "" ? Loc.T("edit_empty") : lobbyName), () =>
                    Edit(PageSteam, 0, Loc.T("mp_lobby_name_label"), Together.Str(Ctrl, "lobbyName"), false, v =>
                    {
                        if (v.Length > 40) v = v.Substring(0, 40);
                        Together.Set(Ctrl, "lobbyName", v);
                        if (v != "") { PlayerPrefs.SetString("StacklandsTogether.LobbyName", v); PlayerPrefs.Save(); }
                    })));
                string pw = Together.Str(c, "lobbyPassword");
                e.Add(Line(Loc.T("mp_lobby_password", pw == "" ? Loc.T("mp_password_none") : Loc.T("mp_password_set")), () =>
                    Edit(PageSteam, 1, Loc.T("mp_lobby_password_label"), Together.Str(Ctrl, "lobbyPassword"), true, v =>
                        Together.Set(Ctrl, "lobbyPassword", v.Length > 40 ? v.Substring(0, 40) : v))));
                bool pub = Together.Bool(c, "publicLobby");
                e.Add(Line(Loc.T("mp_public", Loc.T(pub ? "mp_public_on" : "mp_public_off")), () =>
                {
                    Together.Set(Ctrl, "publicLobby", !Together.Bool(Ctrl, "publicLobby"));
                    Refresh();
                    Speech.Say(Loc.T("mp_public", Loc.T(Together.Bool(Ctrl, "publicLobby") ? "mp_public_on" : "mp_public_off")));
                }, keepOpen: true));
                e.Add(Line(Loc.T("mp_create"), () =>
                {
                    Together.Call(Net, "CreateSteamLobby", Together.Str(Ctrl, "lobbyName"), Together.Str(Ctrl, "lobbyPassword"), Together.Bool(Ctrl, "publicLobby"));
                    Speech.Say(Loc.T("mp_creating"));
                }));
                e.Add(Line(Loc.T("mp_refresh"), () =>
                {
                    lobbiesBefore = LobbyCount();
                    searching = true;
                    searchStart = Time.unscaledTime;
                    Together.SilenceStatus(6f);
                    Together.Call(Net, "RefreshSteamLobbies");
                    Speech.Say(Loc.T("mp_searching"));
                }, keepOpen: true));
                if (Together.Get(lobbies, "Lobbies") is System.Collections.IEnumerable all)
                {
                    foreach (var lobby in all) e.Add(LobbyLine(lobby));
                }
                if (LobbyCount() == 0) e.Add(Line(Loc.T("mp_no_lobby")));
            }
            else if (transport == "SteamP2P")
            {
                e.Add(Line(Loc.T("mp_in_lobby", Together.Str(Together.Get(lobbies, "CurrentLobbyId"), "m_SteamID"))));
                e.Add(Line(Loc.T("mp_invite"), InviteFriends));
                e.Add(Line(Loc.T("mp_leave"), () => Together.Call(Net, "LeaveSessionAndReturnToMenu")));
            }
            else
            {
                e.Add(Line(Loc.T("mp_leave_direct_first")));
            }
            return new ListBrowser.Page("Steam", e);
        }

        private static ListBrowser.Entry LobbyLine(object lobby)
        {
            string name = Together.Str(lobby, "Name");
            if (string.IsNullOrWhiteSpace(name)) name = "Stacklands Together";
            int members = Together.Int(lobby, "Members");
            int cap = Mathf.Max(2, Together.Int(lobby, "Capacity"));
            bool locked = Together.Bool(lobby, "HasPassword");
            bool full = members >= cap;
            string state = Together.Str(lobby, "State");
            string title = TextCleaner.Join(Loc.T("mp_lobby_line", name, members, cap), locked ? Loc.T("mp_locked") : "", full ? Loc.T("mp_full") : "", state);
            ulong id = Together.Get(lobby, "LobbyId") is ulong u ? u : 0UL;
            return Line(title, () =>
            {
                if (full) { Speech.Say(Loc.T("mp_full")); return; }
                if (locked) AskPasswordAndJoin(id);
                else { Together.Call(Net, "JoinSteamLobby", id, ""); Speech.Say(Loc.T("mp_joining")); }
            });
        }

        private static ListBrowser.Page DirectPage()
        {
            var c = Ctrl;
            var e = new List<ListBrowser.Entry>();
            bool offline = !Together.InSession;
            string transport = Together.Str(Net, "ActiveTransport");
            e.Add(Line(Loc.T("mp_address", Together.Str(c, "address")), () =>
                Edit(PageDirect, 0, Loc.T("mp_address_label"), Together.Str(Ctrl, "address"), false, v => Together.Set(Ctrl, "address", v))));
            e.Add(Line(Loc.T("mp_port", Together.Str(c, "port")), () =>
                Edit(PageDirect, 1, Loc.T("mp_port_label"), Together.Str(Ctrl, "port"), false, v => Together.Set(Ctrl, "port", v))));
            if (offline)
            {
                e.Add(Line(Loc.T("mp_host"), () => { Together.Call(Net, "StartHost", Port()); }));
                e.Add(Line(Loc.T("mp_join"), () =>
                {
                    Together.Call(Net, "Join", Together.Str(Ctrl, "address").Trim(), Port());
                    Speech.Say(Loc.T("mp_joining"));
                }));
            }
            else if (transport == "DirectUdp")
            {
                e.Add(Line(Loc.T("mp_leave"), () => Together.Call(Net, "LeaveSessionAndReturnToMenu")));
            }
            e.Add(Line(Loc.T("mp_direct_help")));
            return new ListBrowser.Page(Loc.T("mp_page_direct"), e);
        }

        private static int Port()
        {
            return int.TryParse(Together.Str(Ctrl, "port"), out int p) && p > 0 && p <= 65535 ? p : 19482;
        }

        private static ListBrowser.Page ToolsPage()
        {
            var e = new List<ListBrowser.Entry>();
            var args = new object[] { 0, 0 };
            Together.Call(Net, "GetProgressionCounts", args);
            e.Add(Line(Loc.T("mp_progress", args[0], args[1])));
            e.Add(Line(Loc.T("mp_sync"), () =>
            {
                var a = new object[] { null };
                Together.Call(Net, "RequestManualWorldSync", a);
                Speech.Say(a[0] as string ?? Loc.T("mp_sync"));
            }, keepOpen: true));
            if (Together.IsHost)
            {
                e.Add(ResetLine("mp_reset_ideas", true, false));
                e.Add(ResetLine("mp_reset_quests", false, true));
                e.Add(ResetLine("mp_reset_both", true, true));
            }
            else e.Add(Line(Loc.T("mp_tools_client")));
            return new ListBrowser.Page(Loc.T("mp_page_tools"), e);
        }

        private static ListBrowser.Entry ResetLine(string key, bool ideas, bool quests)
        {
            return Line(Loc.T(key), () =>
            {
                if (armedReset != key || Time.unscaledTime - armedTime > 5f)
                {
                    armedReset = key;
                    armedTime = Time.unscaledTime;
                    Speech.Say(Loc.T("mp_reset_confirm"));
                    return;
                }
                armedReset = null;
                var a = new object[] { ideas, quests, null };
                Together.Call(Net, "ResetSharedProgression", a);
                Speech.Say(a[2] as string ?? Loc.T(key));
                Refresh();
            }, keepOpen: true);
        }

        // ------------------------------------------------------------------ helpers

        private static int LobbyCount()
        {
            return Together.Get(Lobbies, "Lobbies") is System.Collections.ICollection col ? col.Count : 0;
        }

        private static void AskPasswordAndJoin(ulong lobbyId)
        {
            if (lobbyId == 0) return;
            if (list != null && list.IsOpen) list.Close(announce: false);
            TextPrompt.Open(Loc.T("mp_password_prompt"), "", true, pw =>
            {
                Together.Call(Net, "JoinSteamLobby", lobbyId, pw);
                Speech.Say(Loc.T("mp_joining"));
            }, () => Together.Call(Ctrl, "CloseJoinPasswordPrompt"));
        }

        private static void InviteFriends()
        {
            try
            {
                var id = Together.Get(Lobbies, "CurrentLobbyId");
                var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Steamworks.SteamFriends")).FirstOrDefault(x => x != null);
                var m = t?.GetMethod("ActivateGameOverlayInviteDialog");
                if (id == null || m == null) { Speech.Say(Loc.T("not_now")); return; }
                m.Invoke(null, new[] { id });
                Speech.Say(Loc.T("mp_invite_opened"));
            }
            catch (Exception ex) { Log.Once(ex); Speech.Say(Loc.T("not_now")); }
        }
    }
}
