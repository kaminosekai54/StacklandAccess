using System.Collections.Generic;

namespace StacklandAccessNS
{
    /// <summary>
    /// Mod strings. French when the game is in French, English otherwise.
    /// Game content (card names, descriptions, quests...) always comes from the game's own localisation.
    /// </summary>
    public static class Loc
    {
        public static bool IsFrench
        {
            get
            {
                try
                {
                    var lang = SokLoc.instance != null ? SokLoc.instance.CurrentLanguage : null;
                    return lang != null && lang.ToLowerInvariant().StartsWith("fr");
                }
                catch { return false; }
            }
        }

        public const string TsvFileName = "StacklandAccess_strings.tsv";

        /// <summary>Strings read from the TSV file: language name (as the game names it) -> key -> text.</summary>
        private static readonly Dictionary<string, Dictionary<string, string>> tsv = new Dictionary<string, Dictionary<string, string>>();

        private static string CurrentLanguage
        {
            get
            {
                try { return SokLoc.instance != null ? SokLoc.instance.CurrentLanguage ?? "" : ""; }
                catch { return ""; }
            }
        }

        /// <summary>
        /// Loads the translations file shipped with the mod. Columns: Key, then one column per language,
        /// named like in the game (English, French, German...). Missing texts fall back to the built-in English and French.
        /// </summary>
        public static void LoadTsv(string modPath)
        {
            try
            {
                string path = System.IO.Path.Combine(modPath, TsvFileName);
                if (!System.IO.File.Exists(path)) return;
                var lines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                if (lines.Length < 2) return;
                var header = lines[0].Split('\t');
                for (int i = 1; i < lines.Length; i++)
                {
                    var cols = lines[i].Split('\t');
                    if (cols.Length < 2 || string.IsNullOrWhiteSpace(cols[0]) || cols[0].StartsWith("#")) continue;
                    for (int c = 1; c < cols.Length && c < header.Length; c++)
                    {
                        if (string.IsNullOrEmpty(cols[c])) continue;
                        string lang = header[c].Trim();
                        if (!tsv.TryGetValue(lang, out var table)) tsv[lang] = table = new Dictionary<string, string>();
                        table[cols[0].Trim()] = cols[c];
                    }
                }
                Log.Info("Loaded " + TsvFileName + ": " + string.Join(", ", tsv.Keys));
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        /// <summary>
        /// Writes the built-in English and French texts to the TSV file, keeping the other language columns
        /// already in it (used to update the translation file when texts are added).
        /// </summary>
        public static void ExportTsv(string path)
        {
            var otherLangs = new List<string>();
            var other = new Dictionary<string, Dictionary<string, string>>();
            if (System.IO.File.Exists(path))
            {
                var lines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                if (lines.Length > 0)
                {
                    var header = lines[0].Split('\t');
                    for (int c = 3; c < header.Length; c++) otherLangs.Add(header[c]);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var cols = lines[i].Split('\t');
                        var row = new Dictionary<string, string>();
                        for (int c = 3; c < cols.Length && c < header.Length; c++) row[header[c]] = cols[c];
                        other[cols[0]] = row;
                    }
                }
            }
            string Others(string key)
            {
                var sbo = new System.Text.StringBuilder();
                other.TryGetValue(key, out var row);
                foreach (var l in otherLangs) sbo.Append('\t').Append(row != null && row.TryGetValue(l, out var v) ? v : "");
                return sbo.ToString();
            }
            var sb = new System.Text.StringBuilder("Key\tEnglish\tFrench");
            foreach (var l in otherLangs) sb.Append('\t').Append(l);
            sb.Append('\n');
            var done = new HashSet<string>();
            foreach (var kv in En)
            {
                Fr.TryGetValue(kv.Key, out var fr);
                sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\t').Append(fr ?? "").Append(Others(kv.Key)).Append('\n');
                done.Add(kv.Key);
            }
            foreach (var kv in Fr)
            {
                if (done.Add(kv.Key)) sb.Append(kv.Key).Append("\t\t").Append(kv.Value).Append(Others(kv.Key)).Append('\n');
            }
            // Rows that only exist in other languages (Polish "_few" forms...).
            foreach (var key in other.Keys)
            {
                if (done.Add(key)) sb.Append(key).Append("\t\t").Append(Others(key)).Append('\n');
            }
            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
        }

        private static bool TryGet(string key, out string s)
        {
            string lang = CurrentLanguage;
            if (lang != "" && tsv.TryGetValue(lang, out var t) && t.TryGetValue(key, out s)) return true;
            var table = IsFrench ? Fr : En;
            if (table.TryGetValue(key, out s)) return true;
            if (tsv.TryGetValue("English", out var en) && en.TryGetValue(key, out s)) return true;
            return En.TryGetValue(key, out s);
        }

        private static bool Has(string key)
        {
            string lang = CurrentLanguage;
            if (lang != "" && tsv.TryGetValue(lang, out var t) && t.Count > 0 && lang != "English" && lang != "French") return t.ContainsKey(key);
            return (IsFrench ? Fr : En).ContainsKey(key);
        }

        public static string T(string key, params object[] args)
        {
            if (!TryGet(key, out var s)) s = key;
            if (args != null && args.Length > 0)
            {
                try { s = string.Format(s, args); } catch (System.Exception ex) { Log.Once(ex); }
            }
            return s;
        }

        /// <summary>Like T, with the singular form (key + "_one") when count is 1 (0 or 1 in French).</summary>
        public static string N(string key, int count, params object[] args)
        {
            bool singular = count == 1 || (IsFrench && count == 0);
            if (singular && Has(key + "_one")) key += "_one";
            else if (!singular && CurrentLanguage == "Polish")
            {
                // Polish: 2-4 (but not 12-14) use a separate form.
                int d = count % 10, h = count % 100;
                if (d >= 2 && d <= 4 && (h < 12 || h > 14) && Has(key + "_few")) key += "_few";
            }
            return T(key, args);
        }

        /// <summary>Word for an icon / currency, singular or plural.</summary>
        public static string Word(string iconKey, int count) => N(iconKey, count);

        private static readonly Dictionary<string, int> hintCounts = new Dictionary<string, int>();

        /// <summary>Instructions are given the first 3 times only in a session, then left out.</summary>
        public static string Hint(string key)
        {
            hintCounts.TryGetValue(key, out int n);
            hintCounts[key] = n + 1;
            return n < 3 ? T(key) : "";
        }

        public static string Icon(string spriteName)
        {
            switch (spriteName)
            {
                case "goldicon": return T("icon_gold");
                case "foodicon": return T("icon_food");
                case "cardicon": return T("icon_card");
                case "happinessicon": return T("icon_happiness");
                case "energyicon": return T("icon_energy");
                case "shellicon": return T("icon_shell");
                case "dollaricon": return T("icon_dollar");
                case "checkmarkicon": return T("icon_check");
                case "workericon": return T("icon_worker");
                case "housingicon": return T("icon_housing");
                case "pollutionicon": return T("icon_pollution");
                case "sewericon": return T("icon_sewer");
                case "transporticon": return T("icon_transport");
                case "healthicon": return T("icon_health");
                case "hvicon": return T("icon_hv");
                case "lvicon": return T("icon_lv");
                case "calamityicon": return T("icon_calamity");
                case "circleicon": return "";
                default: return spriteName.Replace("icon", "");
            }
        }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["mod_ready"] = "Stackland Access ready. Press F1 for help.",
            ["icon_gold_one"] = "coin", ["icon_shell_one"] = "shell", ["icon_dollar_one"] = "dollar", ["icon_card_one"] = "card",
            ["pack_one"] = "pack {0}, {1} card left", ["picked_stack_one"] = "Picked up {0} and 1 card above.",
            ["list_open_one"] = "{0}, 1 item. Up and down arrows, Enter for details, Escape to close.", ["page_name_one"] = "{0}, 1 item",
            ["list_open_short"] = "{0}, {1} items.", ["list_open_short_one"] = "{0}, 1 item.",
            ["money_blocked_one"] = "1 stack with {1} could not be used: busy, under another card, or in a fight. Move the {1} free and try again.",
            ["undiscovered_one"] = "1 card not discovered yet in this pack", ["quests_summary_one"] = "{0} done of {1}",
            ["seconds"] = "{0} seconds", ["seconds_one"] = "{0} second",
            ["list_production"] = "Production buildings", ["no_production"] = "No production building on the board (sawmill, brickyard, composter, farmland).",
            ["n_possible"] = "{0} possible", ["n_possible_one"] = "{0} possible",
            ["prod_rule"] = "{0} cards make {1}", ["prod_available"] = "{0} available: {1}", ["prod_none"] = "nothing available to put on it",
            ["prod_not_enough"] = "{0} needs at least {1} cards.", ["prod_fed"] = "{0} fed with {1}.",
            ["move_start"] = "Moving {0}.", ["move_hint"] = "Arrows to move it, I for what is around, Enter or Escape to finish.",
            ["move_end"] = "{0} placed.", ["move_edge"] = "Edge of the board.", ["move_free"] = "free space", ["move_near"] = "near {0}",
            ["list_villagers"] = "Villagers", ["vill_working_in"] = "working in {0}", ["vill_with"] = "with {0}", ["vill_waiting"] = "in a pile with {0}, nothing in progress", ["vill_idle"] = "idle",
            ["restacked"] = "{0} cards regrouped into full piles.", ["restacked_one"] = "1 card regrouped.", ["restacked_none"] = "Nothing to regroup.",
            ["missing_busy"] = "{0} (busy)", ["any_fish"] = "fish", ["portal_info"] = "spawns enemies, {0} left. Put villagers on it to travel to the dark forest.",
            ["new_portal"] = "A strange portal appeared: {0}. Shortcut U.", ["villager_free"] = "Free: {0}", ["cfg_beep"] = "Accessibility: sound when a villager becomes free",
            ["mod_error"] = "Stackland Access: an error occurred, details are in the game log.",
            ["compat_warning"] = "Stackland Access: this version of the game changed, these features may not work: {0}",
            ["feat_move"] = "moving cards", ["feat_buttons"] = "menu buttons", ["feat_focus"] = "menu focus", ["feat_recipes"] = "recipes", ["feat_camera"] = "camera follow",
            ["icon_gold"] = "coins", ["icon_food"] = "food", ["icon_card"] = "cards", ["icon_happiness"] = "happiness",
            ["icon_energy"] = "energy", ["icon_shell"] = "shells", ["icon_dollar"] = "dollars", ["icon_check"] = "done",
            ["icon_worker"] = "workers", ["icon_housing"] = "housing", ["icon_pollution"] = "pollution", ["icon_sewer"] = "sewage",
            ["icon_transport"] = "transport", ["icon_health"] = "health", ["icon_hv"] = "high voltage", ["icon_lv"] = "low voltage",
            ["icon_calamity"] = "calamity",

            // menus
            ["unavailable"] = "unavailable", ["button"] = "button", ["checked"] = "checked", ["unchecked"] = "not checked",
            ["slider"] = "slider", ["edit"] = "edit field", ["editing"] = "editing, type then press Enter", ["edit_done"] = "Text: {0}",
            ["empty"] = "empty", ["deleted"] = "deleted {0}", ["dropdown"] = "list", ["of"] = "{0} of {1}",
            ["no_items"] = "No items on this screen.", ["modal"] = "Dialog", ["screen_text"] = "Screen text: {0}",
            ["no_info"] = "No more information.", ["choices"] = "Choices: {0}", ["vol_music"] = "Music volume", ["vol_sfx"] = "Sound effects volume", ["selected"] = "selected", ["not_selected"] = "not selected",
            ["scr_MainMenu"] = "Main menu", ["scr_PauseScreen"] = "Pause", ["scr_OptionsScreen"] = "Options",
            ["scr_CardopediaScreen"] = "Cardopedia", ["scr_GameOverScreen"] = "Game over", ["scr_SelectSaveScreen"] = "Select save",
            ["scr_RunOptionsScreen"] = "New run options", ["scr_ModsScreen"] = "Mods", ["scr_ModOptionsScreen"] = "Mod options",
            ["scr_ControlsScreen"] = "Controls", ["scr_AccessibilityScreen"] = "Accessibility", ["scr_AdvancedSettingsScreen"] = "Advanced settings",
            ["scr_CreditsScreen"] = "Credits", ["scr_SelectLanguageScreen"] = "Language", ["scr_SelectResolutionScreen"] = "Resolution",
            ["scr_GameScreen"] = "Board", ["scr_CutsceneScreen"] = "",

            // board
            ["cat_all"] = "All", ["cat_people"] = "People", ["cat_food"] = "Food", ["cat_resources"] = "Resources",
            ["cat_buildings"] = "Buildings", ["cat_enemies"] = "Enemies", ["cat_animals"] = "Animals", ["cat_ideas"] = "Ideas and rumors",
            ["cat_locations"] = "Locations", ["cat_equipment"] = "Equipment", ["cat_shop"] = "Shop, packs and merchants", ["cat_other"] = "Other",
            ["cat_empty"] = "{0}: empty", ["cat_count"] = "{0}, {1}",
            ["stack_of"] = "stack of {0}", ["card_pos"] = "card {0} of {1}", ["bottom"] = "bottom", ["top"] = "top",
            ["in_combat"] = "in combat", ["busy"] = "{0}, {1} left", ["face_down"] = "face down card",
            ["pack"] = "pack {0}, {1} cards left", ["pack_closed"] = "pack {0}, not opened",
            ["buybox"] = "Buy {0}, costs {1}", ["buybox_locked"] = "Locked pack", ["sellbox"] = "{0}",
            ["hp"] = "{0} of {1} health", ["value"] = "worth {0}", ["cant_sell"] = "cannot be sold", ["food_value"] = "food value {0}",
            ["equipped"] = "equipped: {0}", ["sell_price"] = "Sells for {0} {1}", ["stack_price"] = "Whole stack sells for {0} {1}",
            ["in_stack"] = "Card {0} of {1} in the stack: {2}", ["has_inventory"] = "Has an inventory", ["undiscovered"] = "{0} cards not discovered yet in this pack",
            ["details"] = "Details", ["held_none"] = "You are not holding any card.", ["held_is"] = "In hand: {0}. Its stack: {1}", ["type"] = "type {0}",
            ["picked"] = "Picked up {0}.", ["picked_hint"] = "Choose a target and press Enter. Backspace: put it down alone. Escape: cancel.",
            ["new_cards_on"] = "New card announcements on.", ["new_cards_off"] = "New card announcements off.",
            ["picked_stack"] = "Picked up {0} and {1} cards above.",
            ["cant_pick"] = "{0} cannot be moved now.", ["pick_cancel"] = "Cancelled.", ["holding"] = "Holding {0}.",
            ["dropped_on"] = "{0} placed on {1}.", ["cant_drop"] = "Cannot put {0} on {1}.", ["dropped_free"] = "{0} put down alone.",
            ["target_busy"] = "{0} is busy.", ["same_stack"] = "That is the same stack.",
            ["sold"] = "Sold {0}.", ["cant_sell_now"] = "{0} cannot be sold.", ["sell_confirm"] = "Sell {0}? Press Delete again to confirm.",
            ["bought"] = "Pack bought: {0}.", ["paid"] = "Paid {0}, {1} still to pay.", ["no_money"] = "Not enough {2} for {0}: costs {1}, you have {3}.", ["paid_missing"] = "Paid {0}, {1} {2} still missing.",
            ["money_blocked"] = "{0} stacks with {1} could not be used: busy, under another card, or in a fight. Move the {1} free and try again.",
            ["money_unreachable"] = "The remaining {0} could not be reached.", ["no_currency_in_stack"] = "{0} contains no {1}.",
            ["pack_empty"] = "This pack is empty.", ["clicked"] = "Clicked {0}.", ["no_board"] = "Board not available now.",
            ["not_now"] = "Not possible right now.", ["why_modal"] = "A window is open.", ["why_paused_menu"] = "The game is not running.", ["why_animation"] = "An animation or event is in progress, wait a moment.", ["action_started"] = "{0}: {1}, {2}.", ["nothing_happens"] = "No recipe started.", ["nothing_to_unstack"] = "This card is not in a pile.", ["unstacked_bottom"] = "{0} taken out of the pile. Remaining pile: {1}",
            ["board_name"] = "Board: {0}",
            ["b_main"] = "Mainland", ["b_island"] = "Island", ["b_forest"] = "Dark forest", ["b_cities"] = "Cities",
            ["b_happiness"] = "Happiness world", ["b_greed"] = "Greed world", ["b_death"] = "Death world",

            // status
            ["moon"] = "Moon {0}, {1} left.", ["moon_peaceful"] = "Moon {0}, peaceful mode.", ["moon_forest"] = "Forest, wave {0}.",
            ["speed_pause"] = "Paused", ["speed"] = "Speed {0}",
            ["moon_soon"] = "End of moon in {0} seconds.", ["too_many_cards"] = "Too many cards: {0} of {1}.",

            // events
            ["new_card"] = "New: {0}", ["new_enemy"] = "Enemy! {0}", ["new_idea"] = "New idea: {0}",
            ["lost_card"] = "Lost: {0}", ["defeated"] = "Defeated: {0}", ["continue_hint"] = "Enter: {0}",
            ["removing_hint"] = "Select cards on the board and press Delete to sell them.",
            ["cfg_new_cards"] = "Accessibility: announce new cards", ["cfg_combat"] = "Accessibility: announce combat",
            ["cfg_actions"] = "Accessibility: announce recipes that start", ["cfg_moon"] = "Accessibility: end of moon warning, seconds (0 = off)",
            ["list_possible"] = "Recipes possible now", ["no_possible"] = "No recipe possible with the cards on the board. Right arrow: all ideas.",
            ["recipe_possible"] = "possible", ["recipe_blocked"] = "not available right now", ["recipe_max"] = "already {0} on the board, the maximum", ["recipe_no_match"] = "Warning: the game does not seem to accept this pile, the recipe may not start.", ["recipe_missing"] = "missing: {0}", ["recipe_in_place"] = "already in place",
            ["recipe_built"] = "{0} set up: {1}", ["recipe_partial"] = "{0}: could not stack {1}", ["recipe_changed"] = "The cards changed, open the list again.",
            ["list_open_pages"] = "{0}, {1} items. Up and down to browse, left and right to change list, Enter to use, I for information, Escape to close.",
            ["page_name"] = "{0}, {1} items", ["page_hint"] = "Right arrow for more.",
            ["inventory"] = "Inventory", ["inventory_empty"] = "Inventory empty.", ["unequipped"] = "{0} removed from the inventory.",
            ["picked_equip_hint"] = "Enter on a villager to give it, Backspace to take it off, Escape to cancel.",
            ["effects"] = "Effects: {0}", ["effect_new"] = "{0}: {1}", ["action_done"] = "{0} finished: {1}",
            ["view"] = "View: {0}", ["view_Default"] = "normal", ["view_Energy"] = "energy", ["view_Sewer"] = "sewage", ["view_Transport"] = "transport", ["view_Calamity"] = "calamities",
            ["workers"] = "Workers: {0}", ["autostack_on"] = "Automatic stacking on.", ["cfg_sort"] = "Accessibility: sort the stacks of a category by name (otherwise by place on the board)", ["autostack_off"] = "Automatic stacking off.", ["cfg_autostack"] = "Accessibility: stack idle resources and food of the same kind automatically", ["contains"] = "contains {0} {1}", ["contains_food"] = "contains {0} food", ["nothing"] = "nothing",
            ["cfg_hits"] = "Accessibility: announce every hit in combat", ["attack_hit"] = "{0} hits {1}, {2} of {3} health", ["attack_miss"] = "{0} misses {1}",
            ["attack_kill"] = "{0} defeats {1}", ["combat_end"] = "Combat over.", ["cart_cost"] = "give {0} coins to buy an item, Enter to pay",
            ["merchant_given"] = "{0} of {1} coins given, Enter to give coins", ["need_pile"] = "You need a pile of at least {0} {1}: stack your coins together.",
            ["cfg_camera"] = "Accessibility: camera follows the selection", ["combat_start"] = "Combat: {0}", ["notif"] = "{0}: {1}",

            // lists
            ["list_quests"] = "Quests", ["list_ideas"] = "Ideas", ["list_history"] = "Message history", ["list_help"] = "Help",
            ["list_empty"] = "{0}: empty", ["list_open"] = "{0}, {1} items. Up and down arrows, Enter for details, Escape to close.",
            ["list_closed"] = "Closed.", ["quest_done"] = "done", ["btn_on"] = "selected", ["btn_off"] = "not selected", ["quest_todo"] = "to do",
            ["quests_summary"] = "{0} done of {1}",
            ["mp_speed_host"] = "Only the host can change the game speed.",
            ["mp_purchase_sent"] = "Payment sent to the host for {0}.",
            ["mp_status"] = "Multiplayer: {0}",
            ["mp_joined"] = "{0} joined the game.",
            ["mp_left"] = "{0} left the game.",
            ["mp_title"] = "Multiplayer (Stacklands Together). Left and right arrows: Session, Steam, Direct IP, Tools.",
            ["mp_page_session"] = "Session",
            ["mp_page_direct"] = "Direct IP",
            ["mp_page_tools"] = "Tools",
            ["mp_state"] = "Status: {0}",
            ["mp_name"] = "Your player name: {0}. Enter to change.",
            ["mp_name_label"] = "Player name",
            ["mp_you"] = "you",
            ["mp_ping"] = "{0} ms",
            ["mp_leave"] = "Leave the session",
            ["mp_offline_help"] = "Not connected. Connect from the main menu: create or join a lobby on the Steam page, or use Direct IP. The host then starts or loads the world.",
            ["mp_steam_off"] = "Steam is not ready. Start the game through Steam and make sure Steam is online.",
            ["mp_lobby_name"] = "Lobby name: {0}. Enter to change.",
            ["mp_lobby_name_label"] = "Lobby name",
            ["mp_lobby_password"] = "Lobby password: {0}. Enter to change.",
            ["mp_lobby_password_label"] = "Lobby password",
            ["mp_password_none"] = "none",
            ["mp_password_set"] = "set",
            ["mp_public"] = "Visibility: {0}. Enter to switch.",
            ["mp_public_on"] = "public lobby",
            ["mp_public_off"] = "friends only",
            ["mp_create"] = "Create the lobby (you will be the host)",
            ["mp_creating"] = "Creating the lobby…",
            ["mp_refresh"] = "Search for lobbies",
            ["mp_searching"] = "Searching for lobbies…",
            ["mp_lobbies_found"] = "{0} lobbies found.",
            ["mp_lobbies_found_one"] = "1 lobby found.",
            ["mp_lobby_line"] = "{0}, {1} of {2} players",
            ["mp_locked"] = "password required",
            ["mp_full"] = "full",
            ["mp_no_lobby"] = "No lobby found. Use Search for lobbies, or accept a Steam invitation.",
            ["mp_in_lobby"] = "In Steam lobby {0}. Friends can join through Steam or the lobby search.",
            ["mp_invite"] = "Invite friends (opens the Steam overlay)",
            ["mp_invite_opened"] = "Steam invitation window opened.",
            ["mp_leave_direct_first"] = "You are in a Direct IP session: leave it on the Direct IP page before using Steam.",
            ["mp_address"] = "Host address: {0}. Enter to change.",
            ["mp_address_label"] = "Host address",
            ["mp_port"] = "UDP port: {0}. Enter to change.",
            ["mp_port_label"] = "UDP port",
            ["mp_host"] = "Host a game on this port",
            ["mp_join"] = "Join the host at this address",
            ["mp_joining"] = "Connecting…",
            ["mp_direct_help"] = "Direct IP works on a local network or through a UDP tunnel such as Playit.gg. Default port: 19482.",
            ["mp_progress"] = "{0} ideas found, {1} quests complete",
            ["mp_sync"] = "Sync the world now (send a fresh copy of the board)",
            ["mp_reset_ideas"] = "Reset ideas",
            ["mp_reset_quests"] = "Reset quests",
            ["mp_reset_both"] = "Reset ideas and quests",
            ["mp_reset_confirm"] = "This changes the host's save for every player (cards stay in place). Press Enter again to confirm.",
            ["mp_tools_client"] = "Ideas and quests come from the host while you are connected. Only the host can reset them.",
            ["mp_password_prompt"] = "Password for this lobby",
            ["edit_start"] = "{0}, edit field: {1}.",
            ["edit_hint"] = "Type the text, Backspace erases, up arrow reads it, Enter confirms, Escape cancels.",
            ["edit_empty"] = "empty",
            ["edit_hidden"] = "star",
            ["edit_hidden_count"] = "{0} hidden characters",
            ["edit_hidden_count_one"] = "1 hidden character",
            ["edit_space"] = "space",
            ["edit_deleted"] = "{0} erased",
            ["edit_cancel"] = "Cancelled.",
            ["help_mp"] = "F8: multiplayer panel (Stacklands Together): connect, players, world sync. As a client, every action is sent to the host; only the host changes the speed, and automatic stacking is paused during a session.",
            ["field_set"] = "{0}: {1}",
            ["mp_card_taken"] = "{0} is being moved by another player.",
            ["mp_sold"] = "{0} sold {1}.",
            ["mp_bought"] = "{0} bought {1}.",
            ["mp_paid"] = "{0} paid towards {1}.",
            ["mp_opened"] = "{0} opened {1}.",
            ["mp_stacked"] = "{0} put {1} on {2}.",
            ["mp_by"] = "{0}: {1}",
            ["mp_other_player"] = "another player",
            ["cfg_others"] = "Accessibility: announce other players' actions (multiplayer host)",
            ["prod_plant"] = "{0}: plant {1}, gives {2} in {3}",
            ["prod_put"] = "{0}: put {1} on it",
            ["prod_count"] = "{0} available",
            ["list_all_ideas"] = "All ideas and rumors",
            ["no_ideas"] = "No idea found yet.",
            ["rumor"] = "rumor",
            ["new_city_gate"] = "The Industrial Revolution card appeared: {0}. Put a villager on it to go to the city. Shortcut U.",
            ["city_gate_info"] = "put a villager on it: after 10 seconds you can go to the city",
            ["conn_lv"] = "low voltage cable",
            ["conn_hv"] = "high voltage cable",
            ["conn_sewer"] = "sewer pipe",
            ["conn_transport"] = "transport arrow",
            ["conn_in"] = "input",
            ["conn_out"] = "output",
            ["conn_free"] = "not connected, Enter to connect",
            ["conn_to"] = "connected to {0}, {1}. Enter to disconnect",
            ["pos_right"] = "{0} to the right",
            ["pos_left"] = "{0} to the left",
            ["pos_up"] = "{0} up",
            ["pos_down"] = "{0} down",
            ["pos_here"] = "same place",
            ["eff_powered"] = "has energy",
            ["eff_unpowered"] = "no energy",
            ["eff_producing"] = "produces energy",
            ["eff_not_producing"] = "produces no energy",
            ["eff_sewer"] = "connected to the sewer",
            ["eff_no_sewer"] = "not connected to the sewer",
            ["conn_help_lv"] = "Low voltage: the output of a generator gives energy to the input of a building; low voltage buildings stop without it.",
            ["conn_help_hv"] = "High voltage: carries energy from big generators; high voltage buildings need it.",
            ["conn_help_sewer"] = "Sewer: links buildings to the sewer system; a building without it gets the \"no sewer\" problem.",
            ["conn_help_transport"] = "Transport: cards made by the card at the output are sent to the card at the input.",
            ["conn_none"] = "No connection on this card.",
            ["conn_none_mainland"] = "On this island, cards connect with transport arrows once you have a road builder.",
            ["conn_page_card"] = "Connections of {0}",
            ["conn_page_network"] = "All connections",
            ["conn_page_problems"] = "Problems",
            ["conn_link"] = "{0} to {1}, {2}, {3}",
            ["conn_no_links"] = "No connection on the board.",
            ["conn_no_problems"] = "No energy or sewer problem.",
            ["conn_page_targets"] = "Connect {0} to",
            ["conn_no_target"] = "No free card to connect it to.",
            ["conn_done"] = "{0} connected to {1} ({2}).",
            ["conn_failed"] = "The game refused this connection.",
            ["conn_removed"] = "{0} disconnected from {1}.",
            ["help_conn"] = "E: connections of the stack (energy, sewer, transport): what they give it, where the linked cards are; Enter connects or disconnects. Right arrow: all connections, then the cards with an energy or sewer problem. In move mode (Ctrl+Enter), E tells where the linked cards are.",
            ["web_title"] = "Stacklands cards",
            ["web_search"] = "Search a card, recipe or ingredient",
            ["web_search_hint"] = "The grid updates as you type. Each card says how to get it; Enter or a click opens all its details.",
            ["web_category"] = "Category",
            ["web_all"] = "All",
            ["web_only_found"] = "Only cards I have found (no spoilers)",
            ["web_count"] = "{0} cards",
            ["web_count_one"] = "1 card",
            ["web_how"] = "How to make it",
            ["web_used"] = "Used in",
            ["web_packs"] = "Found in packs",
            ["web_drops"] = "Gives",
            ["web_from"] = "Obtained from",
            ["web_none"] = "Nothing known.",
            ["web_close"] = "Close",
            ["web_value"] = "Sell value",
            ["web_food"] = "Food",
            ["web_health"] = "Health",
            ["web_attack"] = "Attack",
            ["web_defence"] = "Defence",
            ["web_category_label"] = "Category",
            ["web_unknown"] = "idea not found yet",
            ["web_rumor"] = "rumor",
            ["web_idea"] = "Idea",
            ["web_time"] = "time",
            ["web_generated"] = "Made from your game on {0}. Press F6 in the game to update it.",
            ["web_cities"] = "Cities",
            ["export_file_name"] = "Stacklands - cards",
            ["export_done"] = "Card list created and opened in your browser: {0}",
            ["export_failed"] = "Could not create the card list (see Player.log).",
            ["export_no_template"] = "The file cards_viewer.html is missing from the mod folder.",
            ["help_export"] = "F6: create a web page with every card of the game, searchable, with recipes, packs and drops, and open it in your browser (saved in Documents). Shift+F6: the same as a public website in every language, ready for GitHub Pages.",
            ["web_generated_site"] = "Data extracted from the game on {0}, made with the Stackland Access mod.",
            ["web_type"] = "Card type",
            ["web_content"] = "Content",
            ["web_obtain"] = "How to get it",
            ["web_obtain_recipe"] = "Has a recipe",
            ["web_obtain_pack"] = "In a pack",
            ["web_obtain_drop"] = "Dropped or harvested",
            ["web_obtain_none"] = "No known source",
            ["web_sort"] = "Sort by",
            ["web_sort_name"] = "Name",
            ["web_sort_value"] = "Sell value",
            ["web_sort_food"] = "Food",
            ["web_sort_attack"] = "Attack",
            ["web_language"] = "Language",
            ["web_reset"] = "Reset filters",
            ["web_loading"] = "Loading…",
            ["web_group"] = "group",
            ["type_structures"] = "Building",
            ["type_humans"] = "Person",
            ["type_resources"] = "Resource",
            ["type_ideas"] = "Idea",
            ["type_food"] = "Food",
            ["type_mobs"] = "Creature",
            ["type_locations"] = "Location",
            ["type_fish"] = "Fish",
            ["type_rumors"] = "Rumor",
            ["type_equipable"] = "Equipment",
            ["type_spirits"] = "Spirit",
            ["type_weather"] = "Weather",
            ["type_disaster"] = "Disaster",
            ["type_event"] = "Event",
            ["type_landmarks"] = "Landmark",
            ["export_site_done"] = "Public website created in {0} ({1} languages). Put the content of this folder on GitHub Pages or any web host.",
            ["web_idea_recipes"] = "Recipe of this idea",
            ["help_board"] = "Board keys",
            ["help_lines"] =
                "Left and right arrows: previous or next stack in the category.|" +
                "Page up and page down: change category.|" +
                "Home and End: first or last stack.|" +
                "Up and down arrows: cards inside the stack, from bottom to top.|" +
                "Direct shortcuts, press again for the next one: P people and villagers, B shop, packs, sell box and travelling merchants, H buildings, F food, G resources, X enemies, A animals, O equipment, K ideas, U locations, J other, Y everything. M: the card in hand. Ctrl+P: list of villagers and what they are doing.|" +
                "I: details of the card, as a list: price, stack value, health, combat, effects, description. Right arrow: inventory of a villager, Enter on an item takes it, then Enter on another villager or Backspace to take it off. Escape or I to close.|" +
                "Enter: pick up the card and the cards above it, then Enter again on a target stack, the sell box or a pack to buy.|" +
                "Ctrl+Enter: move the stack on the board with the arrows, Enter to finish.|" +
                "Enter on a pack: open it, one card at a time. Enter on a buy box with nothing held: pay automatically with your coins.|" +
                "Backspace: take the card and the cards above it off the pile (on the bottom card: take it out alone). While holding a card: put it down alone. Escape: cancel.|" +
                "Delete: sell the card and the cards above it, press twice to confirm.|" +
                "C: click the card, for example to take coins out of a chest.|" +
                "R: resources. T: moon and time. Q: quests. L: recipes, with pages: possible now, production buildings, then one page per idea group; Enter sets the recipe up or feeds the building, left and right change page, I for the result. N: message history.|" +
                "Minus and plus (also on the numeric keypad): slower or faster, from pause to speed 5. Numeric keypad 0 to 4: pause, 0.5, 1, 2, 5. Space: pause. Tab and 1, 2, 3: game speed, handled by the game.|" +
                "Menus: up and down arrows, Enter to activate, left and right for sliders and lists, I for the description, R to read the whole screen, Escape to go back.|" +
                "F2: repeat the last message. F3: toggle announcement of new cards. F4: toggle automatic stacking of identical resources and food (piles you build yourself are never touched). F5: regroup all identical idle cards into full piles (cards used by a craft, and cards that make something together, are left alone). Ctrl+F5: same, only in the current category. Each category remembers the last stack visited.",
        };

        private static readonly Dictionary<string, string> Fr = new Dictionary<string, string>
        {
            ["mod_ready"] = "Stackland Access prêt. F1 pour l'aide.",
            ["icon_gold_one"] = "pièce", ["icon_shell_one"] = "coquillage", ["icon_dollar_one"] = "dollar", ["icon_card_one"] = "carte",
            ["pack_one"] = "paquet {0}, reste {1} carte", ["picked_stack_one"] = "{0} pris, avec 1 carte au-dessus.",
            ["list_open_one"] = "{0}, 1 élément. Flèches haut et bas, Entrée pour le détail, Échap pour fermer.", ["page_name_one"] = "{0}, {1} élément",
            ["list_open_short"] = "{0}, {1} éléments.", ["list_open_short_one"] = "{0}, {1} élément.",
            ["money_blocked_one"] = "1 pile avec des {1} n'a pas pu servir : occupée, sous une autre carte ou en combat. Libérez-la et réessayez.",
            ["undiscovered_one"] = "1 carte pas encore découverte dans ce paquet", ["quests_summary_one"] = "{0} terminée sur {1}",
            ["seconds"] = "{0} secondes", ["seconds_one"] = "{0} seconde",
            ["list_production"] = "Bâtiments de production", ["no_production"] = "Aucun bâtiment de production sur le plateau (scierie, briqueterie, composteur, champ).",
            ["n_possible"] = "{0} possibles", ["n_possible_one"] = "{0} possible",
            ["prod_rule"] = "{0} cartes donnent {1}", ["prod_available"] = "{0} disponibles : {1}", ["prod_none"] = "rien de disponible à poser dessus",
            ["prod_not_enough"] = "{0} a besoin d'au moins {1} cartes.", ["prod_fed"] = "{0} alimenté avec {1}.",
            ["move_start"] = "Déplacement de {0}.", ["move_hint"] = "Flèches pour déplacer, I pour ce qui est autour, Entrée ou Échap pour terminer.",
            ["move_end"] = "{0} posé.", ["move_edge"] = "Bord du plateau.", ["move_free"] = "espace libre", ["move_near"] = "près de {0}",
            ["list_villagers"] = "Villageois", ["vill_working_in"] = "travaille dans {0}", ["vill_with"] = "avec {0}", ["vill_waiting"] = "dans une pile avec {0}, rien en cours", ["vill_idle"] = "inactif",
            ["restacked"] = "{0} cartes regroupées en piles pleines.", ["restacked_one"] = "1 carte regroupée.", ["restacked_none"] = "Rien à regrouper.",
            ["missing_busy"] = "{0} (occupé)", ["any_fish"] = "poisson", ["portal_info"] = "fait apparaître des ennemis, encore {0}. Posez des villageois dessus pour aller dans la forêt sombre.",
            ["new_portal"] = "Un portail étrange est apparu : {0}. Raccourci U.", ["villager_free"] = "Libre : {0}", ["cfg_beep"] = "Accessibilité : son quand un villageois devient libre",
            ["mod_error"] = "Stackland Access : une erreur s'est produite, le détail est dans le journal du jeu.",
            ["compat_warning"] = "Stackland Access : cette version du jeu a changé, ces fonctions risquent de ne pas marcher : {0}",
            ["feat_move"] = "déplacement des cartes", ["feat_buttons"] = "boutons des menus", ["feat_focus"] = "focus des menus", ["feat_recipes"] = "recettes", ["feat_camera"] = "suivi de la caméra",
            ["icon_gold"] = "pièces", ["icon_food"] = "nourriture", ["icon_card"] = "cartes", ["icon_happiness"] = "bonheur",
            ["icon_energy"] = "énergie", ["icon_shell"] = "coquillages", ["icon_dollar"] = "dollars", ["icon_check"] = "fait",
            ["icon_worker"] = "travailleurs", ["icon_housing"] = "logement", ["icon_pollution"] = "pollution", ["icon_sewer"] = "égouts",
            ["icon_transport"] = "transport", ["icon_health"] = "santé", ["icon_hv"] = "haute tension", ["icon_lv"] = "basse tension",
            ["icon_calamity"] = "calamité",

            ["unavailable"] = "indisponible", ["button"] = "bouton", ["checked"] = "coché", ["unchecked"] = "non coché",
            ["slider"] = "curseur", ["edit"] = "zone de texte", ["editing"] = "saisie, tapez puis Entrée", ["edit_done"] = "Texte : {0}",
            ["empty"] = "vide", ["deleted"] = "effacé {0}", ["dropdown"] = "liste", ["of"] = "{0} sur {1}",
            ["no_items"] = "Aucun élément sur cet écran.", ["modal"] = "Fenêtre", ["screen_text"] = "Texte de l'écran : {0}",
            ["no_info"] = "Pas d'autre information.", ["choices"] = "Choix : {0}", ["vol_music"] = "Volume de la musique", ["vol_sfx"] = "Volume des effets sonores", ["selected"] = "sélectionné", ["not_selected"] = "non sélectionné",
            ["scr_MainMenu"] = "Menu principal", ["scr_PauseScreen"] = "Pause", ["scr_OptionsScreen"] = "Options",
            ["scr_CardopediaScreen"] = "Encyclopédie des cartes", ["scr_GameOverScreen"] = "Partie terminée", ["scr_SelectSaveScreen"] = "Choix de la sauvegarde",
            ["scr_RunOptionsScreen"] = "Options de la partie", ["scr_ModsScreen"] = "Mods", ["scr_ModOptionsScreen"] = "Options des mods",
            ["scr_ControlsScreen"] = "Commandes", ["scr_AccessibilityScreen"] = "Accessibilité", ["scr_AdvancedSettingsScreen"] = "Paramètres avancés",
            ["scr_CreditsScreen"] = "Crédits", ["scr_SelectLanguageScreen"] = "Langue", ["scr_SelectResolutionScreen"] = "Résolution",
            ["scr_GameScreen"] = "Plateau", ["scr_CutsceneScreen"] = "",

            ["cat_all"] = "Tout", ["cat_people"] = "Personnes", ["cat_food"] = "Nourriture", ["cat_resources"] = "Ressources",
            ["cat_buildings"] = "Bâtiments", ["cat_enemies"] = "Ennemis", ["cat_animals"] = "Animaux", ["cat_ideas"] = "Idées et rumeurs",
            ["cat_locations"] = "Lieux", ["cat_equipment"] = "Équipement", ["cat_shop"] = "Boutique, paquets et marchands", ["cat_other"] = "Autres",
            ["cat_empty"] = "{0} : vide", ["cat_count"] = "{0}, {1}",
            ["stack_of"] = "pile de {0}", ["card_pos"] = "carte {0} sur {1}", ["bottom"] = "bas", ["top"] = "haut",
            ["in_combat"] = "en combat", ["busy"] = "{0}, reste {1}", ["face_down"] = "carte face cachée",
            ["pack"] = "paquet {0}, reste {1} cartes", ["pack_closed"] = "paquet {0}, pas encore ouvert",
            ["buybox"] = "Acheter {0}, coûte {1}", ["buybox_locked"] = "Paquet verrouillé", ["sellbox"] = "{0}",
            ["hp"] = "{0} sur {1} points de vie", ["value"] = "vaut {0}", ["cant_sell"] = "ne peut pas être vendue", ["food_value"] = "valeur nutritive {0}",
            ["equipped"] = "équipé : {0}", ["sell_price"] = "Se vend {0} {1}", ["stack_price"] = "Toute la pile se vend {0} {1}",
            ["in_stack"] = "Carte {0} sur {1} dans la pile : {2}", ["has_inventory"] = "Possède un inventaire", ["undiscovered"] = "{0} cartes pas encore découvertes dans ce paquet",
            ["details"] = "Détails", ["held_none"] = "Vous ne tenez aucune carte.", ["held_is"] = "En main : {0}. Sa pile : {1}", ["type"] = "type {0}",
            ["picked"] = "{0} pris.", ["picked_hint"] = "Choisissez une cible et appuyez sur Entrée. Retour arrière : la poser seule. Échap : annuler.",
            ["new_cards_on"] = "Annonce des nouvelles cartes activée.", ["new_cards_off"] = "Annonce des nouvelles cartes désactivée.",
            ["picked_stack"] = "{0} pris, avec {1} cartes au-dessus.",
            ["cant_pick"] = "{0} ne peut pas être déplacée maintenant.", ["pick_cancel"] = "Annulé.", ["holding"] = "Vous tenez {0}.",
            ["dropped_on"] = "{0} posé sur {1}.", ["cant_drop"] = "Impossible de poser {0} sur {1}.", ["dropped_free"] = "{0} posé seul.",
            ["target_busy"] = "{0} est occupé.", ["same_stack"] = "C'est la même pile.",
            ["sold"] = "Vendu : {0}.", ["cant_sell_now"] = "{0} ne peut pas être vendu.", ["sell_confirm"] = "Vendre {0} ? Appuyez encore sur Suppr pour confirmer.",
            ["bought"] = "Paquet acheté : {0}.", ["paid"] = "Payé {0}, reste {1} à payer.", ["no_money"] = "Pas assez de {2} pour {0} : coûte {1}, vous en avez {3}.", ["paid_missing"] = "Payé {0}, il manque encore {1} {2}.",
            ["money_blocked"] = "{0} piles avec des {1} n'ont pas pu servir : occupées, sous une autre carte ou en combat. Libérez les {1} et réessayez.",
            ["money_unreachable"] = "Les {0} restants sont inaccessibles.", ["no_currency_in_stack"] = "{0} ne contient pas de {1}.",
            ["pack_empty"] = "Ce paquet est vide.", ["clicked"] = "Clic sur {0}.", ["no_board"] = "Plateau indisponible pour l'instant.",
            ["not_now"] = "Impossible pour l'instant.", ["why_modal"] = "Une fenêtre est ouverte.", ["why_paused_menu"] = "La partie n'est pas en cours.", ["why_animation"] = "Une animation ou un événement est en cours, patientez un instant.", ["action_started"] = "{0} : {1}, {2}.", ["nothing_happens"] = "Aucune recette lancée.", ["nothing_to_unstack"] = "Cette carte n'est pas dans une pile.", ["unstacked_bottom"] = "{0} sorti de la pile. Pile restante : {1}",
            ["board_name"] = "Plateau : {0}",
            ["b_main"] = "Continent", ["b_island"] = "Île", ["b_forest"] = "Forêt sombre", ["b_cities"] = "Cités",
            ["b_happiness"] = "Monde du bonheur", ["b_greed"] = "Monde de l'avarice", ["b_death"] = "Monde de la mort",

            ["moon"] = "Lune {0}, reste {1}.", ["moon_peaceful"] = "Lune {0}, mode paisible.", ["moon_forest"] = "Forêt, vague {0}.",
            ["speed_pause"] = "Pause", ["speed"] = "Vitesse {0}",
            ["moon_soon"] = "Fin de la lune dans {0} secondes.", ["too_many_cards"] = "Trop de cartes : {0} sur {1}.",

            ["new_card"] = "Nouveau : {0}", ["new_enemy"] = "Ennemi ! {0}", ["new_idea"] = "Nouvelle idée : {0}",
            ["lost_card"] = "Perdu : {0}", ["defeated"] = "Vaincu : {0}", ["continue_hint"] = "Entrée : {0}",
            ["removing_hint"] = "Choisissez des cartes sur le plateau et appuyez sur Suppr pour les vendre.",
            ["cfg_new_cards"] = "Accessibilité : annoncer les nouvelles cartes", ["cfg_combat"] = "Accessibilité : annoncer les combats",
            ["cfg_actions"] = "Accessibilité : annoncer les recettes qui démarrent", ["cfg_moon"] = "Accessibilité : alerte de fin de lune, en secondes (0 = désactivée)",
            ["list_possible"] = "Recettes possibles maintenant", ["no_possible"] = "Aucune recette possible avec les cartes du plateau. Flèche droite : toutes les idées.",
            ["recipe_possible"] = "possible", ["recipe_blocked"] = "indisponible pour le moment", ["recipe_max"] = "déjà {0} sur le plateau, le maximum", ["recipe_no_match"] = "Attention : le jeu ne semble pas accepter cette pile, la recette risque de ne pas démarrer.", ["recipe_missing"] = "manque : {0}", ["recipe_in_place"] = "déjà en place",
            ["recipe_built"] = "{0} mis en place : {1}", ["recipe_partial"] = "{0} : impossible d'empiler {1}", ["recipe_changed"] = "Les cartes ont changé, rouvrez la liste.",
            ["list_open_pages"] = "{0}, {1} éléments. Haut et bas pour parcourir, gauche et droite pour changer de liste, Entrée pour utiliser, I pour les informations, Échap pour fermer.",
            ["page_name"] = "{0}, {1} éléments", ["page_hint"] = "Flèche droite pour la suite.",
            ["inventory"] = "Inventaire", ["inventory_empty"] = "Inventaire vide.", ["unequipped"] = "{0} retiré de l'inventaire.",
            ["picked_equip_hint"] = "Entrée sur un villageois pour le lui donner, Retour arrière pour le retirer, Échap pour annuler.",
            ["effects"] = "Effets : {0}", ["effect_new"] = "{0} : {1}", ["action_done"] = "{0} terminé : {1}",
            ["view"] = "Vue : {0}", ["view_Default"] = "normale", ["view_Energy"] = "énergie", ["view_Sewer"] = "égouts", ["view_Transport"] = "transport", ["view_Calamity"] = "calamités",
            ["workers"] = "Travailleurs : {0}", ["autostack_on"] = "Empilement automatique activé.", ["cfg_sort"] = "Accessibilité : trier les piles d'une catégorie par nom (sinon par place sur le plateau)", ["autostack_off"] = "Empilement automatique désactivé.", ["cfg_autostack"] = "Accessibilité : empiler automatiquement les ressources et la nourriture identiques inutilisées", ["contains"] = "contient {0} {1}", ["contains_food"] = "contient {0} de nourriture", ["nothing"] = "rien",
            ["cfg_hits"] = "Accessibilité : annoncer chaque coup en combat", ["attack_hit"] = "{0} frappe {1}, {2} sur {3} points de vie", ["attack_miss"] = "{0} rate {1}",
            ["attack_kill"] = "{0} achève {1}", ["combat_end"] = "Combat terminé.", ["cart_cost"] = "donnez {0} pièces pour acheter un objet, Entrée pour payer",
            ["merchant_given"] = "{0} pièces données sur {1}, Entrée pour donner des pièces", ["need_pile"] = "Il faut une pile d'au moins {0} {1} : empilez vos pièces ensemble.",
            ["cfg_camera"] = "Accessibilité : la caméra suit la sélection", ["combat_start"] = "Combat : {0}", ["notif"] = "{0} : {1}",

            ["list_quests"] = "Quêtes", ["list_ideas"] = "Idées", ["list_history"] = "Historique des messages", ["list_help"] = "Aide",
            ["list_empty"] = "{0} : vide", ["list_open"] = "{0}, {1} éléments. Flèches haut et bas, Entrée pour le détail, Échap pour fermer.",
            ["list_closed"] = "Fermé.", ["quest_done"] = "terminée", ["btn_on"] = "sélectionné", ["btn_off"] = "non sélectionné", ["quest_todo"] = "à faire",
            ["quests_summary"] = "{0} terminées sur {1}",
            ["mp_speed_host"] = "Seul l'hôte peut changer la vitesse du jeu.",
            ["mp_purchase_sent"] = "Paiement envoyé à l'hôte pour {0}.",
            ["mp_status"] = "Multijoueur : {0}",
            ["mp_joined"] = "{0} a rejoint la partie.",
            ["mp_left"] = "{0} a quitté la partie.",
            ["mp_title"] = "Multijoueur (Stacklands Together). Flèches gauche et droite : Session, Steam, IP directe, Outils.",
            ["mp_page_session"] = "Session",
            ["mp_page_direct"] = "IP directe",
            ["mp_page_tools"] = "Outils",
            ["mp_state"] = "État : {0}",
            ["mp_name"] = "Votre nom de joueur : {0}. Entrée pour le changer.",
            ["mp_name_label"] = "Nom du joueur",
            ["mp_you"] = "vous",
            ["mp_ping"] = "{0} ms",
            ["mp_leave"] = "Quitter la session",
            ["mp_offline_help"] = "Non connecté. Connectez-vous depuis le menu principal : créez ou rejoignez un salon sur la page Steam, ou utilisez l'IP directe. L'hôte lance ensuite ou charge le monde.",
            ["mp_steam_off"] = "Steam n'est pas prêt. Lancez le jeu depuis Steam et vérifiez que Steam est en ligne.",
            ["mp_lobby_name"] = "Nom du salon : {0}. Entrée pour le changer.",
            ["mp_lobby_name_label"] = "Nom du salon",
            ["mp_lobby_password"] = "Mot de passe du salon : {0}. Entrée pour le changer.",
            ["mp_lobby_password_label"] = "Mot de passe du salon",
            ["mp_password_none"] = "aucun",
            ["mp_password_set"] = "défini",
            ["mp_public"] = "Visibilité : {0}. Entrée pour changer.",
            ["mp_public_on"] = "salon public",
            ["mp_public_off"] = "amis seulement",
            ["mp_create"] = "Créer le salon (vous serez l'hôte)",
            ["mp_creating"] = "Création du salon…",
            ["mp_refresh"] = "Chercher des salons",
            ["mp_searching"] = "Recherche des salons…",
            ["mp_lobbies_found"] = "{0} salons trouvés.",
            ["mp_lobbies_found_one"] = "1 salon trouvé.",
            ["mp_lobby_line"] = "{0}, {1} joueurs sur {2}",
            ["mp_locked"] = "mot de passe requis",
            ["mp_full"] = "complet",
            ["mp_no_lobby"] = "Aucun salon trouvé. Utilisez Chercher des salons, ou acceptez une invitation Steam.",
            ["mp_in_lobby"] = "Dans le salon Steam {0}. Vos amis peuvent rejoindre par Steam ou par la recherche de salons.",
            ["mp_invite"] = "Inviter des amis (ouvre la fenêtre Steam)",
            ["mp_invite_opened"] = "Fenêtre d'invitation Steam ouverte.",
            ["mp_leave_direct_first"] = "Vous êtes dans une session IP directe : quittez-la sur la page IP directe avant d'utiliser Steam.",
            ["mp_address"] = "Adresse de l'hôte : {0}. Entrée pour la changer.",
            ["mp_address_label"] = "Adresse de l'hôte",
            ["mp_port"] = "Port UDP : {0}. Entrée pour le changer.",
            ["mp_port_label"] = "Port UDP",
            ["mp_host"] = "Héberger une partie sur ce port",
            ["mp_join"] = "Rejoindre l'hôte à cette adresse",
            ["mp_joining"] = "Connexion…",
            ["mp_direct_help"] = "L'IP directe fonctionne en réseau local ou via un tunnel UDP comme Playit.gg. Port par défaut : 19482.",
            ["mp_progress"] = "{0} idées trouvées, {1} quêtes terminées",
            ["mp_sync"] = "Synchroniser le monde maintenant (renvoie une copie du plateau)",
            ["mp_reset_ideas"] = "Réinitialiser les idées",
            ["mp_reset_quests"] = "Réinitialiser les quêtes",
            ["mp_reset_both"] = "Réinitialiser idées et quêtes",
            ["mp_reset_confirm"] = "Cela modifie la sauvegarde de l'hôte pour tous les joueurs (les cartes restent en place). Entrée de nouveau pour confirmer.",
            ["mp_tools_client"] = "Les idées et quêtes viennent de l'hôte pendant la connexion. Seul l'hôte peut les réinitialiser.",
            ["mp_password_prompt"] = "Mot de passe de ce salon",
            ["edit_start"] = "{0}, champ de saisie : {1}.",
            ["edit_hint"] = "Tapez le texte, Retour arrière efface, flèche haut relit, Entrée valide, Échap annule.",
            ["edit_empty"] = "vide",
            ["edit_hidden"] = "étoile",
            ["edit_hidden_count"] = "{0} caractères masqués",
            ["edit_hidden_count_one"] = "1 caractère masqué",
            ["edit_space"] = "espace",
            ["edit_deleted"] = "{0} effacé",
            ["edit_cancel"] = "Annulé.",
            ["help_mp"] = "F8 : panneau multijoueur (Stacklands Together) : connexion, joueurs, synchronisation. En tant qu'invité, chaque action est envoyée à l'hôte ; seul l'hôte change la vitesse, et l'empilement automatique est suspendu pendant une session.",
            ["field_set"] = "{0} : {1}",
            ["mp_card_taken"] = "{0} est déplacé par un autre joueur.",
            ["mp_sold"] = "{0} a vendu {1}.",
            ["mp_bought"] = "{0} a acheté {1}.",
            ["mp_paid"] = "{0} a payé une partie de {1}.",
            ["mp_opened"] = "{0} a ouvert {1}.",
            ["mp_stacked"] = "{0} a posé {1} sur {2}.",
            ["mp_by"] = "{0} : {1}",
            ["mp_other_player"] = "un autre joueur",
            ["cfg_others"] = "Accessibilité : annoncer les actions des autres joueurs (hôte multijoueur)",
            ["prod_plant"] = "{0} : planter {1}, donne {2} en {3}",
            ["prod_put"] = "{0} : poser {1} dessus",
            ["prod_count"] = "{0} disponibles",
            ["list_all_ideas"] = "Toutes les idées et rumeurs",
            ["no_ideas"] = "Aucune idée trouvée pour l'instant.",
            ["rumor"] = "rumeur",
            ["new_city_gate"] = "La carte Révolution industrielle est apparue : {0}. Posez un villageois dessus pour aller en ville. Raccourci U.",
            ["city_gate_info"] = "posez un villageois dessus : après 10 secondes vous pourrez aller en ville",
            ["conn_lv"] = "câble basse tension",
            ["conn_hv"] = "câble haute tension",
            ["conn_sewer"] = "tuyau d'égout",
            ["conn_transport"] = "flèche de transport",
            ["conn_in"] = "entrée",
            ["conn_out"] = "sortie",
            ["conn_free"] = "libre, Entrée pour relier",
            ["conn_to"] = "relié à {0}, {1}. Entrée pour débrancher",
            ["pos_right"] = "{0} à droite",
            ["pos_left"] = "{0} à gauche",
            ["pos_up"] = "{0} en haut",
            ["pos_down"] = "{0} en bas",
            ["pos_here"] = "au même endroit",
            ["eff_powered"] = "alimenté en énergie",
            ["eff_unpowered"] = "sans énergie",
            ["eff_producing"] = "produit de l'énergie",
            ["eff_not_producing"] = "ne produit pas d'énergie",
            ["eff_sewer"] = "raccordé à l'égout",
            ["eff_no_sewer"] = "pas raccordé à l'égout",
            ["conn_help_lv"] = "Basse tension : la sortie d'un générateur alimente l'entrée d'un bâtiment ; sans énergie, le bâtiment s'arrête.",
            ["conn_help_hv"] = "Haute tension : transporte l'énergie des grands générateurs ; les bâtiments haute tension en ont besoin.",
            ["conn_help_sewer"] = "Égout : relie les bâtiments au réseau d'égout ; sans lui, le bâtiment a le problème « pas d'égout ».",
            ["conn_help_transport"] = "Transport : les cartes produites par la carte en sortie sont envoyées vers la carte en entrée.",
            ["conn_none"] = "Aucune connexion sur cette carte.",
            ["conn_none_mainland"] = "Sur cette île, les cartes se relient par des flèches de transport une fois que vous avez un constructeur de routes.",
            ["conn_page_card"] = "Connexions de {0}",
            ["conn_page_network"] = "Toutes les connexions",
            ["conn_page_problems"] = "Problèmes",
            ["conn_link"] = "{0} vers {1}, {2}, {3}",
            ["conn_no_links"] = "Aucune connexion sur le plateau.",
            ["conn_no_problems"] = "Aucun problème d'énergie ou d'égout.",
            ["conn_page_targets"] = "Relier {0} à",
            ["conn_no_target"] = "Aucune carte libre à laquelle le relier.",
            ["conn_done"] = "{0} relié à {1} ({2}).",
            ["conn_failed"] = "Le jeu a refusé cette connexion.",
            ["conn_removed"] = "{0} débranché de {1}.",
            ["help_conn"] = "E : connexions de la pile (énergie, égout, transport) : ce qu'elles lui apportent, où sont les cartes reliées ; Entrée relie ou débranche. Flèche droite : toutes les connexions, puis les cartes qui ont un problème d'énergie ou d'égout. En mode déplacement (Ctrl+Entrée), E dit où sont les cartes reliées.",
            ["web_title"] = "Cartes de Stacklands",
            ["web_search"] = "Rechercher une carte, une recette ou un ingrédient",
            ["web_search_hint"] = "La grille se met à jour pendant la frappe. Chaque carte indique comment l'obtenir ; Entrée ou un clic ouvre toutes ses informations.",
            ["web_category"] = "Catégorie",
            ["web_all"] = "Toutes",
            ["web_only_found"] = "Seulement les cartes découvertes (sans spoiler)",
            ["web_count"] = "{0} cartes",
            ["web_count_one"] = "1 carte",
            ["web_how"] = "Comment la fabriquer",
            ["web_used"] = "Utilisée dans",
            ["web_packs"] = "Dans les paquets",
            ["web_drops"] = "Donne",
            ["web_from"] = "Obtenue avec",
            ["web_none"] = "Rien de connu.",
            ["web_close"] = "Fermer",
            ["web_value"] = "Prix de vente",
            ["web_food"] = "Nourriture",
            ["web_health"] = "Santé",
            ["web_attack"] = "Attaque",
            ["web_defence"] = "Défense",
            ["web_category_label"] = "Catégorie",
            ["web_unknown"] = "idée pas encore découverte",
            ["web_rumor"] = "rumeur",
            ["web_idea"] = "Idée",
            ["web_time"] = "durée",
            ["web_generated"] = "Créé depuis votre partie le {0}. Appuyez sur F6 dans le jeu pour le mettre à jour.",
            ["web_cities"] = "Cités",
            ["export_file_name"] = "Stacklands - cartes",
            ["export_done"] = "Liste des cartes créée et ouverte dans votre navigateur : {0}",
            ["export_failed"] = "Impossible de créer la liste des cartes (voir Player.log).",
            ["export_no_template"] = "Le fichier cards_viewer.html manque dans le dossier du mod.",
            ["help_export"] = "F6 : créer une page web avec toutes les cartes du jeu, avec recherche, recettes, paquets et butins, et l'ouvrir dans votre navigateur (enregistrée dans Documents). Maj+F6 : la même chose en site public dans toutes les langues, prêt pour GitHub Pages.",
            ["web_generated_site"] = "Données extraites du jeu le {0}, avec le mod Stackland Access.",
            ["web_type"] = "Type de carte",
            ["web_content"] = "Contenu",
            ["web_obtain"] = "Obtention",
            ["web_obtain_recipe"] = "A une recette",
            ["web_obtain_pack"] = "Dans un paquet",
            ["web_obtain_drop"] = "Butin ou récolte",
            ["web_obtain_none"] = "Sans source connue",
            ["web_sort"] = "Trier par",
            ["web_sort_name"] = "Nom",
            ["web_sort_value"] = "Prix de vente",
            ["web_sort_food"] = "Nourriture",
            ["web_sort_attack"] = "Attaque",
            ["web_language"] = "Langue",
            ["web_reset"] = "Réinitialiser les filtres",
            ["web_loading"] = "Chargement…",
            ["web_group"] = "groupe",
            ["type_structures"] = "Bâtiment",
            ["type_humans"] = "Personne",
            ["type_resources"] = "Ressource",
            ["type_ideas"] = "Idée",
            ["type_food"] = "Nourriture",
            ["type_mobs"] = "Créature",
            ["type_locations"] = "Lieu",
            ["type_fish"] = "Poisson",
            ["type_rumors"] = "Rumeur",
            ["type_equipable"] = "Équipement",
            ["type_spirits"] = "Esprit",
            ["type_weather"] = "Météo",
            ["type_disaster"] = "Catastrophe",
            ["type_event"] = "Événement",
            ["type_landmarks"] = "Monument",
            ["export_site_done"] = "Site public créé dans {0} ({1} langues). Mettez le contenu de ce dossier sur GitHub Pages ou un autre hébergeur.",
            ["web_idea_recipes"] = "Recette de cette idée",
            ["help_board"] = "Touches du plateau",
            ["help_lines"] =
                "Flèches gauche et droite : pile précédente ou suivante dans la catégorie.|" +
                "Page précédente et page suivante : changer de catégorie.|" +
                "Début et Fin : première ou dernière pile.|" +
                "Flèches haut et bas : cartes de la pile, du bas vers le haut.|" +
                "Raccourcis directs, appuyez encore pour passer au suivant : P personnes et villageois, B boutique, paquets, vente et marchands ambulants, H bâtiments, F nourriture, G ressources, X ennemis, A animaux, O équipement, K idées, U lieux, J autres, Y tout. M : la carte en main. Ctrl+P : liste des villageois et de leur occupation.|" +
                "I : détails de la carte, sous forme de liste : prix de vente, valeur de la pile, santé, combat, effets, description. Flèche droite : inventaire d'un villageois, Entrée sur un objet le prend, puis Entrée sur un autre villageois ou Retour arrière pour le retirer. Échap ou I pour fermer.|" +
                "Entrée : prendre la carte et celles au-dessus, puis Entrée sur une pile cible, la boîte de vente ou un paquet à acheter.|" +
                "Ctrl+Entrée : déplacer la pile sur le plateau avec les flèches, Entrée pour terminer.|" +
                "Entrée sur un paquet : l'ouvrir, une carte à la fois. Entrée sur un paquet à acheter sans rien tenir : payer automatiquement avec vos pièces.|" +
                "Retour arrière : sortir la carte et celles au-dessus de la pile (sur la carte du bas : la sortir seule). En tenant une carte : la poser seule. Échap : annuler.|" +
                "Suppr : vendre la carte et celles au-dessus, deux fois pour confirmer.|" +
                "C : cliquer sur la carte, par exemple pour sortir des pièces d'un coffre.|" +
                "R : ressources. T : lune et temps. Q : quêtes. L : recettes, par pages : possibles maintenant, bâtiments de production, puis une page par groupe d'idées ; Entrée met la recette en place ou alimente le bâtiment, gauche et droite changent de page, I pour le résultat. N : historique des messages.|" +
                "Moins et plus (aussi sur le pavé numérique) : ralentir ou accélérer, de la pause à la vitesse 5. Pavé numérique 0 à 4 : pause, 0,5, 1, 2, 5. Espace : pause. Tab et 1, 2, 3 : vitesse du jeu, gérés par le jeu.|" +
                "Menus : flèches haut et bas, Entrée pour activer, gauche et droite pour les curseurs et listes, I pour la description, R pour lire tout l'écran, Échap pour revenir.|" +
                "F2 : répéter le dernier message. F3 : activer ou couper l'annonce des nouvelles cartes. F4 : activer ou couper l'empilement automatique des ressources et de la nourriture identiques (les piles faites à la main ne sont jamais touchées). F5 : regrouper toutes les cartes identiques inutilisées en piles pleines (les cartes d'une fabrication en cours, et celles qui fabriquent quelque chose ensemble, ne bougent pas). Ctrl+F5 : pareil, seulement dans la catégorie courante. Chaque catégorie retient la dernière pile visitée.",
        };
    }
}
