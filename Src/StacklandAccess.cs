using System;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>Entry point loaded by the Stacklands mod loader.</summary>
    public class StacklandAccess : Mod
    {
        public static StacklandAccess Instance;
        public HarmonyLib.Harmony ModHarmony => Harmony;
        private static bool patched;

        private ConfigEntry<bool> cfgNewCards;
        private ConfigEntry<bool> cfgCombat;
        private ConfigEntry<bool> cfgHits;
        private ConfigEntry<bool> cfgActions;
        private ConfigEntry<int> cfgMoon;
        private ConfigEntry<bool> cfgCamera;
        private ConfigEntry<bool> cfgAutoStack;
        private ConfigEntry<bool> cfgSortByName;
        private ConfigEntry<bool> cfgBeep;
        private ConfigEntry<bool> cfgOthers;

        public override void Ready()
        {
            Instance = this;
            try
            {
                Speech.Init(Path);
                Beeper.ModPath = Path;
                Loc.LoadTsv(Path);
                try { SetupConfig(); } catch (Exception e) { Logger.LogError("Config: " + e); }
                if (AccessController.Instance == null)
                {
                    var go = new GameObject("StacklandAccess");
                    DontDestroyOnLoad(go);
                    go.AddComponent<AccessController>();
                }
                if (!patched)
                {
                    patched = true;
                    try { Harmony.PatchAll(typeof(StacklandAccess).Assembly); }
                    catch (Exception e) { Logger.LogError("Harmony patches failed: " + e); }
                }
                Application.quitting += Speech.Shutdown;
                Logger.Log("Stackland Access ready");
            }
            catch (Exception e)
            {
                Logger.LogError("Stackland Access failed to start: " + e);
            }
        }

        private void SetupConfig()
        {
            cfgNewCards = Config.GetEntry<bool>("announce_new_cards", true);
            cfgCombat = Config.GetEntry<bool>("announce_combat", true);
            cfgHits = Config.GetEntry<bool>("announce_hits", true);
            cfgActions = Config.GetEntry<bool>("announce_actions", true);
            cfgMoon = Config.GetEntry<int>("moon_warning_seconds", 20);
            cfgCamera = Config.GetEntry<bool>("camera_follow", true);
            cfgAutoStack = Config.GetEntry<bool>("auto_stack", true);
            cfgSortByName = Config.GetEntry<bool>("sort_by_name", true);
            cfgBeep = Config.GetEntry<bool>("beep_villager_free", true);
            cfgOthers = Config.GetEntry<bool>("announce_other_players", true);
            cfgNewCards.UI.Name = Loc.T("cfg_new_cards");
            cfgCombat.UI.Name = Loc.T("cfg_combat");
            cfgHits.UI.Name = Loc.T("cfg_hits");
            cfgActions.UI.Name = Loc.T("cfg_actions");
            cfgMoon.UI.Name = Loc.T("cfg_moon");
            cfgCamera.UI.Name = Loc.T("cfg_camera");
            cfgAutoStack.UI.Name = Loc.T("cfg_autostack");
            cfgSortByName.UI.Name = Loc.T("cfg_sort");
            cfgBeep.UI.Name = Loc.T("cfg_beep");
            cfgOthers.UI.Name = Loc.T("cfg_others");
            ApplyConfig();
        }

        public void ApplyConfig()
        {
            AccessController.AnnounceNewCards = cfgNewCards.Value;
            AccessController.AnnounceCombat = cfgCombat.Value;
            AccessController.AnnounceHits = cfgHits.Value;
            AccessController.AnnounceActions = cfgActions.Value;
            AccessController.MoonWarningSeconds = Mathf.Max(0, cfgMoon.Value);
            AccessController.CameraFollowEnabled = cfgCamera.Value;
            AccessController.AutoStackEnabled = cfgAutoStack.Value;
            AccessController.SortByName = cfgSortByName.Value;
            AccessController.BeepIdleVillager = cfgBeep.Value;
            AccessController.AnnounceOtherPlayers = cfgOthers.Value;
        }

        /// <summary>Keeps the config file in sync when a setting is changed by a shortcut.</summary>
        public static void SaveSetting(string key, bool value)
        {
            var i = Instance;
            if (i == null) return;
            try
            {
                if (key == "announce_new_cards" && i.cfgNewCards != null) { i.cfgNewCards.Value = value; i.Config.Save(); }
                if (key == "auto_stack" && i.cfgAutoStack != null) { i.cfgAutoStack.Value = value; i.Config.Save(); }
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        private float configTimer;

        private void Update()
        {
            // Settings can be changed in Mod Options at any time.
            configTimer -= Time.unscaledDeltaTime;
            if (configTimer <= 0f && cfgNewCards != null)
            {
                configTimer = 1f;
                try { ApplyConfig(); } catch (System.Exception ex) { Log.Once(ex); }
            }
        }
    }
}
