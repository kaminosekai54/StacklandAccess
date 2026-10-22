using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// First start of the mod: a game pop-up on the main menu asks whether the narration should be on.
    /// "No" turns off everything the mod says or plays (it can be turned back on with Ctrl+F2 or in the options).
    /// </summary>
    public static class FirstLaunch
    {
        private static bool asked;
        private static float mainMenuSince = -1f;

        private static bool newsChecked;

        public static void Update()
        {
            if (asked) return;
            // Already set up: only the release notes of a new version, once.
            bool news = false;
            if (StacklandAccess.FirstLaunchDone)
            {
                if (newsChecked || StacklandAccess.ModVersion == "" || StacklandAccess.LastVersionSeen == StacklandAccess.ModVersion) { asked = true; return; }
                news = true;
            }
            var gc = GameCanvas.instance;
            if (gc == null || ModalScreen.instance == null) return;
            if (!(gc.CurrentScreen is MainMenu) || gc.ModalIsOpen || TransitionScreen.InTransition)
            {
                mainMenuSince = -1f;
                return;
            }
            // Wait a moment on the main menu, so the pop-up is not lost in the game's start.
            if (mainMenuSince < 0f) { mainMenuSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - mainMenuSince < 1.5f) return;
            asked = true;
            if (news) { newsChecked = true; ShowNews(); }
            else Show();
        }

        /// <summary>Text of the release notes: one line per change.</summary>
        public static string NewsTitle => Loc.T("news_title", StacklandAccess.ModVersion);
        public static string[] NewsLines => Loc.T("news_lines").Split('|');

        private static void ShowNews()
        {
            try
            {
                var m = ModalScreen.instance;
                m.Clear();
                m.SetTexts(NewsTitle, "- " + string.Join("\n- ", NewsLines));
                m.AddOption(Loc.T("news_ok"), () =>
                {
                    StacklandAccess.LastVersionSeen = StacklandAccess.ModVersion;
                    GameCanvas.instance.CloseModal();
                });
                GameCanvas.instance.OpenModal();
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private static void Show()
        {
            try
            {
                var m = ModalScreen.instance;
                m.Clear();
                m.SetTexts(Loc.T("first_title"), Loc.T("first_text", Bindings.Spoken("toggle_narration"), Bindings.Spoken("options")));
                m.AddOption(Loc.T("first_yes"), () => Answer(true));
                m.AddOption(Loc.T("first_no"), () => Answer(false));
                GameCanvas.instance.OpenModal();
            }
            catch (System.Exception ex)
            {
                Log.Once(ex);
            }
        }

        private static void Answer(bool narration)
        {
            StacklandAccess.FirstLaunchDone = true;
            // A new player does not need the list of changes.
            StacklandAccess.LastVersionSeen = StacklandAccess.ModVersion;
            GameCanvas.instance.CloseModal();
            if (narration) StacklandAccess.SetNarration(true);
            else
            {
                Speech.Say(Loc.T("first_no_said", Bindings.Spoken("toggle_narration")));
                StacklandAccess.SetBool("narration", false);
            }
        }
    }
}
