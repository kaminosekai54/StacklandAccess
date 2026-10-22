using System;
using System.Collections.Generic;
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
        private ConfigEntry<bool> cfgNarration;
        private ConfigEntry<bool> cfgVisual;
        private ConfigEntry<bool> cfgFirstLaunchDone;

        /// <summary>The on/off settings by key, for the options menu (F7).</summary>
        private readonly Dictionary<string, ConfigEntry<bool>> bools = new Dictionary<string, ConfigEntry<bool>>();

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
            cfgNarration = Config.GetEntry<bool>("narration", true);
            cfgVisual = Config.GetEntry<bool>("visual_menus", true);
            cfgFirstLaunchDone = Config.GetEntry<bool>("first_launch_done", false);
            cfgFirstLaunchDone.UI.Hidden = true;
            cfgNarration.UI.Name = Loc.T("cfg_narration");
            cfgVisual.UI.Name = Loc.T("cfg_visual");
            bools["narration"] = cfgNarration;
            bools["visual_menus"] = cfgVisual;
            bools["announce_new_cards"] = cfgNewCards;
            bools["announce_combat"] = cfgCombat;
            bools["announce_hits"] = cfgHits;
            bools["announce_actions"] = cfgActions;
            bools["camera_follow"] = cfgCamera;
            bools["auto_stack"] = cfgAutoStack;
            bools["sort_by_name"] = cfgSortByName;
            bools["beep_villager_free"] = cfgBeep;
            bools["announce_other_players"] = cfgOthers;
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
            AccessController.VisualMenusEnabled = cfgVisual.Value;
            Speech.Enabled = cfgNarration.Value;
        }

        /// <summary>Names of the on/off settings, in the order of the options menu.</summary>
        public static readonly string[] BoolKeys =
        {
            "narration", "visual_menus", "announce_new_cards", "announce_combat", "announce_hits", "announce_actions",
            "announce_other_players", "beep_villager_free", "camera_follow", "auto_stack", "sort_by_name",
        };

        public static bool GetBool(string key)
        {
            var i = Instance;
            return i != null && i.bools.TryGetValue(key, out var e) && e.Value;
        }

        /// <summary>Changes an on/off setting, saves it and applies it now.</summary>
        public static void SetBool(string key, bool value)
        {
            var i = Instance;
            if (i == null || !i.bools.TryGetValue(key, out var e)) return;
            try
            {
                e.Value = value;
                i.Config.Save();
                i.ApplyConfig();
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        public static string BoolName(string key)
        {
            var i = Instance;
            if (i == null || !i.bools.TryGetValue(key, out var e)) return key;
            return e.UI.Name ?? key;
        }

        public static int MoonSeconds
        {
            get { var i = Instance; return i != null && i.cfgMoon != null ? i.cfgMoon.Value : 20; }
            set
            {
                var i = Instance;
                if (i == null || i.cfgMoon == null) return;
                try { i.cfgMoon.Value = value; i.Config.Save(); i.ApplyConfig(); } catch (System.Exception ex) { Log.Once(ex); }
            }
        }

        /// <summary>Narration on or off; said in both cases so the player knows what happened.</summary>
        public static void SetNarration(bool on)
        {
            if (!on) Speech.Say(Loc.T("narration_off"));
            SetBool("narration", on);
            if (Instance == null) Speech.Enabled = on;
            if (on) Speech.Say(Loc.T("narration_on"));
        }

        public static bool FirstLaunchDone
        {
            get { var i = Instance; return i == null || i.cfgFirstLaunchDone == null || i.cfgFirstLaunchDone.Value; }
            set
            {
                var i = Instance;
                if (i == null || i.cfgFirstLaunchDone == null) return;
                try { i.cfgFirstLaunchDone.Value = value; i.Config.Save(); } catch (System.Exception ex) { Log.Once(ex); }
            }
        }

        /// <summary>Keeps the config file in sync when a setting is changed by a shortcut.</summary>
        public static void SaveSetting(string key, bool value)
        {
            var i = Instance;
            if (i == null) return;
            SetBool(key, value);
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
