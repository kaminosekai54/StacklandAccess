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

        public static void Update()
        {
            if (asked) return;
            if (StacklandAccess.FirstLaunchDone) { asked = true; return; }
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
            Show();
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
