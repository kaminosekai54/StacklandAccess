using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Bridge to the "Stacklands Together" multiplayer mod (optional, found by reflection).
    /// In a Together session the host runs the game; a client only shows it and sends requests
    /// (grab, release, sell, buy, click). Every card move of this mod must go through those requests on a client.
    /// </summary>
    public static class Together
    {
        private const string AssemblyName = "stacklands_together";

        private static Type ctrlType;
        private static float nextSearch;
        private static PropertyInfo networkProp;
        private static FieldInfo instanceField;

        public static bool Installed => ctrlType != null;
        public static Assembly Assembly => ctrlType?.Assembly;

        /// <summary>The Together NetworkManager, or null.</summary>
        public static object Net
        {
            get
            {
                if (networkProp == null) return null;
                try { return networkProp.GetValue(null); } catch { return null; }
            }
        }

        /// <summary>The MultiplayerController component (its UI state lives there).</summary>
        public static object Controller
        {
            get
            {
                if (instanceField == null) return null;
                try { return instanceField.GetValue(null); } catch { return null; }
            }
        }

        /// <summary>"Offline", "Host" or "Client".</summary>
        public static string Mode
        {
            get
            {
                var n = Net;
                if (n == null) return "Offline";
                return Get(n, "Mode")?.ToString() ?? "Offline";
            }
        }

        public static bool IsClient => Installed && Mode == "Client";
        public static bool IsHost => Installed && Mode == "Host";
        public static bool InSession => Installed && Mode != "Offline";

        /// <summary>Looks for the Together assembly (it can be loaded after this mod).</summary>
        public static void Detect()
        {
            if (ctrlType != null || Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 3f;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == AssemblyName);
                if (asm == null) return;
                var t = asm.GetType("StacklandsTogetherNS.UI.MultiplayerController");
                if (t == null) return;
                networkProp = t.GetProperty("Network", BindingFlags.Public | BindingFlags.Static);
                instanceField = t.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
                ctrlType = t;
                Log.Info("Stacklands Together found: multiplayer support enabled.");
                RemoteActions.TryPatch();
            }
            catch (Exception ex) { Log.Once(ex); }
        }

        // ------------------------------------------------------------------ reflection helpers

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Dictionary<string, MemberInfo> members = new Dictionary<string, MemberInfo>();

        private static MemberInfo Member(Type t, string name)
        {
            string key = t.FullName + "." + name;
            if (members.TryGetValue(key, out var m)) return m;
            m = (MemberInfo)t.GetProperty(name, All) ?? t.GetField(name, All);
            members[key] = m;
            return m;
        }

        public static object Get(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                switch (Member(obj.GetType(), name))
                {
                    case PropertyInfo p: return p.GetValue(obj);
                    case FieldInfo f: return f.GetValue(obj);
                }
            }
            catch (Exception ex) { Log.Once(ex); }
            return null;
        }

        public static void Set(object obj, string name, object value)
        {
            if (obj == null) return;
            try
            {
                switch (Member(obj.GetType(), name))
                {
                    case PropertyInfo p: p.SetValue(obj, value); break;
                    case FieldInfo f: f.SetValue(obj, value); break;
                }
            }
            catch (Exception ex) { Log.Once(ex); }
        }

        /// <summary>Calls a method by name and argument count. Out parameters are returned in args.</summary>
        public static object Call(object obj, string name, params object[] args)
        {
            if (obj == null) return null;
            try
            {
                var m = obj.GetType().GetMethods(All).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length);
                if (m == null) { Log.Info("Together: method not found " + name); return null; }
                return m.Invoke(obj, args);
            }
            catch (Exception ex) { Log.Once(ex); return null; }
        }

        public static string Str(object obj, string name) => Get(obj, name)?.ToString() ?? "";
        public static bool Bool(object obj, string name) => Get(obj, name) is bool b && b;
        public static int Int(object obj, string name) => Get(obj, name) is int i ? i : 0;

        // ------------------------------------------------------------------ card requests (client)

        public static void Grab(GameCard card)
        {
            if (card != null) Call(Net, "RequestGrab", card);
        }

        public static void Release(GameCard card, string parentId)
        {
            if (card != null) Call(Net, "RequestRelease", card, parentId ?? "");
        }

        public static void GrabBooster(Boosterpack bp) => Call(Net, "RequestBoosterGrab", bp);
        public static void ReleaseBooster(Boosterpack bp) => Call(Net, "RequestBoosterRelease", bp);

        // ------------------------------------------------------------------ session state

        public static List<(int id, string name, int ping, bool you)> Players()
        {
            var list = new List<(int, string, int, bool)>();
            var n = Net;
            if (n == null) return list;
            int me = Int(n, "LocalPlayerId");
            if (Get(n, "Players") is System.Collections.IEnumerable dict)
            {
                foreach (var kv in dict)
                {
                    var p = Get(kv, "Value");
                    if (p == null) continue;
                    int id = Int(p, "Id");
                    list.Add((id, Str(p, "Name"), Int(p, "Ping"), id == me));
                }
            }
            return list;
        }

        private static string lastStatus, lastLoading;
        private static float quietUntil;

        /// <summary>True while the host's world is being copied to this game (cards appear by the dozen).</summary>
        public static bool Quiet => Installed && (Time.unscaledTime < quietUntil || Bool(Net, "IsWorldLoading"));
        private static Dictionary<int, string> lastPlayers = new Dictionary<int, string>();
        private static float statusSilentUntil;

        /// <summary>The panel announces some results itself (lobby search): keep the status quiet meanwhile.</summary>
        public static void SilenceStatus(float seconds) => statusSilentUntil = Time.unscaledTime + seconds;
        private static bool wasInSession;

        /// <summary>Speaks session changes: status, players joining or leaving, world loading.</summary>
        public static void Update(ListBrowser list)
        {
            Detect();
            if (!Installed) return;
            var n = Net;
            if (n == null) return;

            // Together copies the player name to the network only while its own window is drawn,
            // and this mod keeps that window closed: copy it here.
            var ctrl = Controller;
            string wanted = Str(ctrl, "playerName").Trim();
            if (wanted != "" && Str(n, "LocalName") != wanted) Set(n, "LocalName", wanted);

            string status = Str(n, "Status");
            if (status != lastStatus)
            {
                // World download progress changes many times a second: the loading headline covers it.
                bool noisy = status.StartsWith("Downloading") || Time.unscaledTime < statusSilentUntil;
                if (lastStatus != null && !noisy && !string.IsNullOrWhiteSpace(status)) EventNarrator.Announce(Loc.T("mp_status", status), important: true);
                lastStatus = status;
            }

            bool inSession = InSession;
            var now = new Dictionary<int, string>();
            if (inSession) foreach (var p in Players()) if (!p.you) now[p.id] = p.name;
            if (inSession && wasInSession)
            {
                foreach (var kv in now) if (!lastPlayers.ContainsKey(kv.Key)) EventNarrator.Announce(Loc.T("mp_joined", kv.Value), important: true);
                foreach (var kv in lastPlayers) if (!now.ContainsKey(kv.Key)) EventNarrator.Announce(Loc.T("mp_left", kv.Value), important: true);
            }
            lastPlayers = now;
            wasInSession = inSession;

            bool isLoading = Bool(n, "IsWorldLoading");
            if (isLoading || (lastLoading != null && !isLoading)) quietUntil = Time.unscaledTime + 5f;
            string loading = isLoading ? Str(n, "WorldLoadingHeadline") : null;
            if (loading != lastLoading)
            {
                if (!string.IsNullOrWhiteSpace(loading)) Speech.Say(loading, interrupt: false);
                lastLoading = loading;
            }

            MultiplayerPanel.Watch(list);
        }
    }
}
