using System.Collections.Generic;
using System.Linq;

namespace StacklandAccessNS
{
    /// <summary>
    /// Options of the mod (F7): turn features on or off, and change the keys.
    /// Also in the game's Mod options screen for the on/off settings.
    /// </summary>
    public static class OptionsMenu
    {
        private static ListBrowser list;
        private static bool resetArmed;

        private static readonly int[] MoonChoices = { 0, 10, 20, 30, 60 };

        public static void Open(ListBrowser browser)
        {
            list = browser;
            resetArmed = false;
            list.OpenPagesAt(Pages(), 0, 0, speak: false, toggleAction: "options");
            Speech.Say(TextCleaner.Join(Loc.T("opt_open"), Loc.N("page_name", list.Pages[0].Entries.Count, list.Pages[0].Name, list.Pages[0].Entries.Count),
                list.Pages[0].Entries.Count > 0 ? list.Pages[0].Entries[0].Title : ""));
        }

        /// <summary>Rebuilds the pages in place, keeping the page and line, and says something.</summary>
        private static void Refresh(string say)
        {
            if (list == null) return;
            list.OpenPagesAt(Pages(), list.PageIndex, list.Index, speak: false, toggleAction: "options");
            if (!string.IsNullOrEmpty(say)) Speech.Say(say);
        }

        private static string OnOff(bool v) => Loc.T(v ? "opt_on" : "opt_off");

        /// <summary>Setting names start with "Accessibility:" in the game's Mod options; not needed here.</summary>
        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int best = -1, len = 0;
            foreach (var sep in new[] { " : ", ": ", "：" })
            {
                int i = name.IndexOf(sep, System.StringComparison.Ordinal);
                if (i > 0 && i <= 25 && (best < 0 || i < best)) { best = i; len = sep.Length; }
            }
            return best < 0 ? name : name.Substring(best + len).Trim();
        }

        private static List<ListBrowser.Page> Pages()
        {
            var pages = new List<ListBrowser.Page>();

            // Features
            var features = new List<ListBrowser.Entry>();
            foreach (var key in StacklandAccess.BoolKeys)
            {
                string k = key;
                string name = Short(StacklandAccess.BoolName(k));
                features.Add(new ListBrowser.Entry(TextCleaner.Join(name, OnOff(StacklandAccess.GetBool(k))))
                {
                    KeepOpen = true,
                    OnActivate = () =>
                    {
                        bool v = !StacklandAccess.GetBool(k);
                        if (k == "narration") { StacklandAccess.SetNarration(v); Refresh(null); return; }
                        StacklandAccess.SetBool(k, v);
                        Refresh(TextCleaner.Join(name, OnOff(v)));
                    },
                });
            }
            string moonName = Short(Loc.T("cfg_moon"));
            int moon = StacklandAccess.MoonSeconds;
            features.Add(new ListBrowser.Entry(TextCleaner.Join(moonName, moon <= 0 ? Loc.T("opt_off") : CardInfo.Seconds(moon)))
            {
                KeepOpen = true,
                OnActivate = () =>
                {
                    int cur = StacklandAccess.MoonSeconds;
                    int i = System.Array.FindIndex(MoonChoices, c => c > cur);
                    int next = i < 0 ? MoonChoices[0] : MoonChoices[i];
                    StacklandAccess.MoonSeconds = next;
                    Refresh(TextCleaner.Join(moonName, next <= 0 ? Loc.T("opt_off") : CardInfo.Seconds(next)));
                },
            });
            pages.Add(new ListBrowser.Page(Loc.T("opt_page_features"), features));

            // Keys, by place where they work
            AddKeyPage(pages, Loc.T("opt_page_keys_global"), Bindings.Context.Global);
            AddKeyPage(pages, Loc.T("opt_page_keys_board"), Bindings.Context.Board);
            AddKeyPage(pages, Loc.T("opt_page_keys_lists"), Bindings.Context.List, Bindings.Context.Menu);
            return pages;
        }

        private static void AddKeyPage(List<ListBrowser.Page> pages, string name, params Bindings.Context[] contexts)
        {
            var entries = new List<ListBrowser.Entry>();
            foreach (var a in Bindings.All.Where(x => contexts.Contains(x.Context)))
            {
                var action = a;
                entries.Add(new ListBrowser.Entry(TextCleaner.Join(action.Name, action.KeysSpoken))
                {
                    KeepOpen = true,
                    OnActivate = () => Bindings.BeginCapture(action.Id, msg => Refresh(msg)),
                    Info = () => Loc.T("key_default", string.Join(Loc.T("key_or"), action.Defaults.Select(d => d.Spoken()))),
                });
            }
            entries.Add(new ListBrowser.Entry(Loc.T("key_reset_all"))
            {
                KeepOpen = true,
                OnActivate = () =>
                {
                    if (!resetArmed) { resetArmed = true; Speech.Say(Loc.T("key_reset_confirm")); return; }
                    resetArmed = false;
                    Bindings.ResetAll();
                    Refresh(Loc.T("key_reset_done"));
                },
            });
            entries.Add(new ListBrowser.Entry(Loc.T("key_fixed")));
            pages.Add(new ListBrowser.Page(name, entries));
        }
    }
}
