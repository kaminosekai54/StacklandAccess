using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// F6: writes "Stacklands - cards.html" in the Documents folder (every card in a searchable grid, with recipes,
    /// packs and drops, in the game's language, discovered cards marked) and opens it.
    /// Shift+F6: writes a public website in "Documents/Stacklands cards site" (all languages, no save data),
    /// ready to put on GitHub Pages or any static host.
    /// Both are built from cards_viewer.html (shipped with the mod) and from the game itself.
    /// </summary>
    public static class CardExport
    {
        private const string TemplateName = "cards_viewer.html";
        private const string DataMarker = "__STACKLANDS_DATA__";
        private const string SiteFolder = "Stacklands cards site";

        private static readonly Dictionary<string, string> LangCodes = new Dictionary<string, string>
        {
            ["English"] = "en", ["French"] = "fr", ["Dutch"] = "nl", ["German"] = "de", ["Italian"] = "it", ["Spanish"] = "es",
            ["Polish"] = "pl", ["Brazilian Portuguese"] = "pt-BR", ["Japanese"] = "ja", ["Korean"] = "ko",
            ["Chinese (Simplified)"] = "zh-Hans", ["Chinese (Traditional)"] = "zh-Hant",
        };

        public static void Run(bool site = false)
        {
            try
            {
                var wm = WorldManager.instance;
                if (wm == null || wm.GameDataLoader == null) { Speech.Say(Loc.T("not_now")); return; }
                string modPath = StacklandAccess.Instance != null ? StacklandAccess.Instance.Path : null;
                string template = modPath != null ? Path.Combine(modPath, TemplateName) : null;
                if (template == null || !File.Exists(template)) { Speech.Say(Loc.T("export_no_template")); return; }
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs) || !Directory.Exists(docs)) docs = modPath;
                string html = File.ReadAllText(template);

                if (!site)
                {
                    Speech.Say(Loc.T("export_working"));
                    string imgFolder = Loc.T("export_file_name") + "_img";
                    var images = new CardImages(Path.Combine(docs, imgFolder));
                    string json;
                    try { json = Json(BuildData(wm, withSave: true, images, imgFolder + "/")); }
                    finally { images.Dispose(); }
                    string file = Path.Combine(docs, Loc.T("export_file_name") + ".html");
                    File.WriteAllText(file, html.Replace(DataMarker, json), new System.Text.UTF8Encoding(false));
                    Speech.Say(Loc.T("export_done", file));
                    Application.OpenURL(new Uri(file).AbsoluteUri);
                    return;
                }

                string dir = Path.Combine(docs, SiteFolder);
                string dataDir = Path.Combine(dir, "data");
                Directory.CreateDirectory(dataDir);
                var languages = new List<object>();
                var siteImages = new CardImages(Path.Combine(dir, "img"));
                Speech.Say(Loc.T("export_working"));
                var loc = SokLoc.instance;
                string original = loc != null ? loc.CurrentLanguage : null;
                try
                {
                    foreach (var lang in SokLoc.Languages)
                    {
                        if (loc == null || lang == null || !LangCodes.TryGetValue(lang.LanguageName, out var code)) continue;
                        // Switch the game's texts to each language in turn (restored below).
                        loc.CurrentLanguage = lang.LanguageName;
                        File.WriteAllText(Path.Combine(dataDir, code + ".json"), Json(BuildData(wm, withSave: false, siteImages, "img/")), new System.Text.UTF8Encoding(false));
                        string local = lang.LanguageName;
                        try { local = SokLoc.GetLocalLanguageName(lang.LanguageName); } catch { }
                        languages.Add(new { code, name = local, file = "data/" + code + ".json" });
                    }
                }
                finally
                {
                    if (loc != null && original != null) loc.CurrentLanguage = original;
                    siteImages.Dispose();
                }
                File.WriteAllText(Path.Combine(dataDir, "languages.json"), JsonConvert.SerializeObject(languages), new System.Text.UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, "index.html"), html.Replace(DataMarker, "null"), new System.Text.UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, ".nojekyll"), "");
                Speech.Say(Loc.T("export_site_done", dir, languages.Count));
                Application.OpenURL(new Uri(dir).AbsoluteUri);
            }
            catch (Exception ex)
            {
                Log.Once(ex);
                Speech.Say(Loc.T("export_failed"));
            }
        }

        private static string Json(object data) => JsonConvert.SerializeObject(data, Formatting.None).Replace("</", "<\\/");

        private static string Clean(string s) => TextCleaner.Clean(s ?? "");

        /// <summary>
        /// Name of a card in the current language. CardData.Name keeps the first translation it made,
        /// so it would give the same language in every file of the website: translate the term again instead.
        /// </summary>
        public static string CardName(CardData cd)
        {
            if (cd == null) return "";
            if (string.IsNullOrEmpty(cd.NameTerm)) return Clean(cd.Name);
            string text = SokLoc.Translate(cd.NameTerm);
            try
            {
                if (cd.MyCardType == CardType.Ideas)
                {
                    string label = "label_idea_fullname";
                    if (cd is Blueprint b) label = b.IsInvention ? "label_invention_fullname" : b.IsLandmark ? "label_landmark_fullname" : "label_idea_fullname";
                    text = SokLoc.Translate(label, LocParam.Create("name", text));
                }
                else if (cd.MyCardType == CardType.Rumors)
                {
                    text = SokLoc.Translate("label_rumor_full", LocParam.Create("name", text));
                }
            }
            catch { }
            return Clean(text);
        }

        private static string Term(string id, string fallback)
        {
            try
            {
                string s = SokLoc.Translate(id);
                return string.IsNullOrEmpty(s) || s.StartsWith("---") ? fallback : Clean(s);
            }
            catch { return fallback; }
        }

        /// <summary>Name of a content update, as the game's cardopedia names it.</summary>
        private static string UpdateName(CardUpdateType u)
        {
            switch (u)
            {
                case CardUpdateType.Cities: return Term("label_cities_dlc", "Cities");
                case CardUpdateType.Mod: return Term("label_cardopedia_modded", "Mods");
                default: return Term("label_cardopedia_" + u.ToString().ToLowerInvariant(), u.ToString());
            }
        }

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Card colours as in the game: background, border, icon.</summary>
        /// <summary>
        /// The game's colour for a kind of card. ColorManager.GetPaletteForCard stops on the first rule that
        /// needs a card on the board (energy producers), so the rules are tried one by one here, skipping those.
        /// </summary>
        private static CardPalette Palette(CardData cd)
        {
            var cm = ColorManager.instance;
            if (cm == null || cd == null) return null;
            try { if (cd.HasUniquePalette && cd.MyPalette != null) return cd.MyPalette; } catch { }
            var rules = HarmonyLib.Traverse.Create(cm).Field("cardColorRules").GetValue<List<CardColorRule>>();
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    try { if (rule?.Predicate != null && rule.Predicate(cd)) return rule.Palette; }
                    catch { }
                }
            }
            return cm.Default;
        }

        private static string[] Colors(CardData cd)
        {
            var p = Palette(cd);
            return p != null ? new[] { Hex(p.Color), Hex(p.Color2), Hex(p.Icon) } : null;
        }

        /// <summary>Icon shown on the card: its own, or for ideas the idea icon of its content (like the game).</summary>
        private static Sprite IconOf(CardData cd)
        {
            if (cd.Icon != null) return cd.Icon;
            var sm = SpriteManager.instance;
            if (sm == null || cd.MyCardType != CardType.Ideas) return null;
            switch (cd.CardUpdateType)
            {
                case CardUpdateType.Island: return sm.IslandIdeaIcon;
                case CardUpdateType.Spirit: return sm.SpiritIdeaIcon;
                case CardUpdateType.Cities: return sm.CitiesIdeaIcon;
                default: return sm.IdeaIcon;
            }
        }

        private static string Icon(CardImages images, Sprite icon, CardData cd, string name)
        {
            if (images == null) return null;
            icon = icon ?? IconOf(cd);
            if (icon == null) return null;
            // The icons are kept as drawn (white with black lines), like on the cards of the game.
            return images.Save(icon, name);
        }

        private static object BuildData(WorldManager wm, bool withSave, CardImages images, string imgBase)
        {
            var loader = wm.GameDataLoader;
            var cards = new List<object>();
            var seen = new HashSet<string>();
            foreach (var cd in wm.CardDataPrefabs)
            {
                if (cd == null || string.IsNullOrEmpty(cd.Id) || !seen.Add(cd.Id)) continue;
                if (cd is Blueprint bpCard)
                {
                    // Ideas are cards too: their recipe is shown on them (and they stay listed with the recipes).
                    string iname = "", idesc = "";
                    try { iname = Clean(bpCard.KnowledgeName); } catch { }
                    try { idesc = Clean(bpCard.KnowledgeText); } catch { }
                    if (iname == "" || iname.StartsWith("---")) continue;
                    var iupd = CardUpdateType.Main;
                    try { iupd = bpCard.CardUpdateType; } catch { }
                    cards.Add(new
                    {
                        id = bpCard.Id,
                        name = iname,
                        desc = idesc,
                        cat = BoardNavigator.CategoryName(BoardNavigator.Category.Ideas),
                        type = Loc.T("type_ideas"),
                        upd = UpdateName(iupd),
                        value = bpCard.Value,
                        found = withSave ? (bool?)wm.HasFoundCard(bpCard.Id) : null,
                        idea = true,
                        hidden = bpCard.HideFromIdeasTab,
                        group = Term("ideagroup_" + bpCard.BlueprintGroup.ToString().ToLowerInvariant(), bpCard.BlueprintGroup.ToString()),
                        img = Icon(images, IconOf(bpCard), bpCard, "idea_" + bpCard.CardUpdateType),
                        colors = Colors(bpCard),
                    });
                    continue;
                }
                string name = "", desc = "";
                try { name = CardName(cd); } catch { name = cd.Id; }
                try { desc = Clean(cd.Description); } catch { }
                if (name == "" || name.StartsWith("---")) continue;
                int? hp = null, atk = null, def = null;
                if (cd is Combatable cb && cb.BaseCombatStats != null)
                {
                    hp = cb.BaseCombatStats.MaxHealth;
                    atk = cb.BaseCombatStats.AttackDamage;
                    def = cb.BaseCombatStats.Defence;
                }
                List<string> drops = null;
                try { drops = cd.GetPossibleDrops()?.Distinct().ToList(); } catch { }
                var update = CardUpdateType.Main;
                try { update = cd.CardUpdateType; } catch { }
                cards.Add(new
                {
                    id = cd.Id,
                    name,
                    desc,
                    cat = BoardNavigator.CategoryName(BoardNavigator.CategoryOf(cd)),
                    type = Loc.T("type_" + cd.MyCardType.ToString().ToLowerInvariant()),
                    upd = UpdateName(update),
                    value = cd.Value,
                    food = cd is Food f ? (int?)f.FoodValue : null,
                    hp, atk, def,
                    found = withSave ? (bool?)wm.HasFoundCard(cd.Id) : null,
                    rumor = cd is Rumor,
                    drops = drops != null && drops.Count > 0 ? drops : null,
                    img = Icon(images, cd.Icon, cd, cd.Id),
                    colors = Colors(cd),
                });
            }

            var recipes = new List<object>();
            foreach (var bp in wm.BlueprintPrefabs)
            {
                if (bp?.Subprints == null) continue;
                string idea = "";
                try { idea = Clean(bp.KnowledgeName); } catch { }
                string group = Term("ideagroup_" + bp.BlueprintGroup.ToString().ToLowerInvariant(), bp.BlueprintGroup.ToString());
                var update = CardUpdateType.Main;
                try { var data = loader.GetCardFromId(bp.Id); if (data != null) update = data.CardUpdateType; } catch { }
                foreach (var sp in bp.Subprints)
                {
                    if (sp?.RequiredCards == null || sp.RequiredCards.Length == 0) continue;
                    var req = new List<object>();
                    foreach (var r in sp.RequiredCards)
                    {
                        var allowed = Recipes.AllowedIds(sp, r);
                        req.Add(new { label = Recipes.RequirementName(r, allowed), ids = allowed.Distinct().ToArray() });
                    }
                    var output = new List<string>();
                    if (!string.IsNullOrEmpty(sp.ResultCard)) output.Add(sp.ResultCard);
                    if (sp.ExtraResultCards != null) output.AddRange(sp.ExtraResultCards.Where(x => !string.IsNullOrEmpty(x)));
                    string status = "";
                    try { status = Clean(sp.StatusName); } catch { }
                    recipes.Add(new
                    {
                        idea,
                        ideaId = bp.Id,
                        group,
                        upd = UpdateName(update),
                        found = withSave ? (bool?)KnownRecipes.IsKnown(bp) : null,
                        hidden = bp.HideFromIdeasTab,
                        req,
                        output,
                        time = sp.Time,
                        status,
                    });
                }
            }

            var packs = new List<object>();
            foreach (var pack in wm.BoosterPackDatas ?? new List<BoosterpackData>())
            {
                if (pack == null) continue;
                try { packs.Add(PackData(wm, pack, images)); } catch (Exception ex) { Log.Once(ex); }
            }

            string lang = "";
            try { lang = SokLoc.instance != null ? SokLoc.instance.CurrentLanguage ?? "" : ""; } catch { }
            return new
            {
                lang,
                code = LangCodes.TryGetValue(lang, out var c) ? c : "en",
                generated = DateTime.Now.ToString("yyyy-MM-dd"),
                personal = withSave,
                imgBase,
                ui = UiTexts(),
                cards,
                recipes,
                packs,
            };
        }

        /// <summary>Currency used on the boards of a pack's location (gold, shells, dollars).</summary>
        private static BoardCurrency CurrencyOf(WorldManager wm, Location location)
        {
            try
            {
                var board = wm.Boards?.FirstOrDefault(b => b != null && b.Location == location);
                if (board != null && board.BoardOptions != null) return board.BoardOptions.Currency;
            }
            catch { }
            return location == Location.Island ? BoardCurrency.Shell : location == Location.Cities ? BoardCurrency.Dollar : BoardCurrency.Gold;
        }

        /// <summary>Cards a bag can give, with the chance of each draw (null when it is a fixed list or depends on strength).</summary>
        private static object BagData(GameDataLoader loader, CardBag bag, string condition)
        {
            var cards = new List<object>();
            List<CardChance> chances = null;
            string kind = bag.CardBagType.ToString();
            try
            {
                if (bag.CardBagType == CardBagType.Chances) chances = bag.Chances;
                else if (bag.CardBagType == CardBagType.SetCardBag)
                    chances = bag.UseFallbackBag ? CardBag.GetChancesForSetCardBag(loader, bag.SetCardBag, bag.FallbackBag) : CardBag.GetChancesForSetCardBag(loader, bag.SetCardBag, null);
            }
            catch { }
            if (chances != null && chances.Count > 0)
            {
                float total = chances.Sum(c => (float)Math.Max(0, c.Chance));
                foreach (var g in chances.GroupBy(c => c.Id))
                {
                    var first = g.First();
                    float pct = total > 0 ? g.Sum(c => (float)Math.Max(0, c.Chance)) / total * 100f : 0f;
                    if (first.IsEnemy)
                    {
                        cards.Add(new { id = (string)null, enemy = true, pct = (float?)Math.Round(pct, 1) });
                        continue;
                    }
                    cards.Add(new
                    {
                        id = first.Id,
                        pct = (float?)Math.Round(pct, 1),
                        max = first.HasMaxCount ? (int?)first.MaxCountToGive : null,
                        needs = first.HasPrerequisiteCard ? first.PrerequisiteCardId : null,
                    });
                }
            }
            else
            {
                List<string> ids = null;
                try { ids = bag.GetCardsInBag(loader); } catch { }
                foreach (var id in (ids ?? new List<string>()).Distinct()) cards.Add(new { id, pct = (float?)null });
            }
            return new { count = bag.CardsInPack, kind, cond = condition, cards };
        }

        private static object PackData(WorldManager wm, BoosterpackData pack, CardImages images)
        {
            var loader = wm.GameDataLoader;
            var ids = new List<string>();
            var bags = new List<object>();
            float expected = 0f;
            foreach (var bag in pack.CardBags ?? new List<CardBag>())
            {
                if (bag == null) continue;
                try { ids.AddRange(bag.GetCardsInBag(loader)); } catch { }
                try { bag.CalculateExpectedValueForBag(loader); expected += bag.ExpectedValue * bag.CardsInPack; } catch { }
                bags.Add(BagData(loader, bag, null));
            }
            foreach (var add in pack.BoosterAdditions ?? new List<BoosterAddition>())
            {
                if (add?.CardBags == null) continue;
                string cond = null;
                if (add.Filter != null)
                {
                    string curse = Together.Get(add.Filter, "Curse")?.ToString().ToLowerInvariant();
                    cond = Loc.T("pack_cond_" + add.Filter.GetType().Name.Replace("VariableFilter", "").ToLowerInvariant(), curse != null ? Loc.T("curse_" + curse) : "");
                }
                foreach (var bag in add.CardBags)
                {
                    if (bag == null) continue;
                    try { ids.AddRange(bag.GetCardsInBag(loader)); } catch { }
                    bags.Add(BagData(loader, bag, cond));
                }
            }
            string name = "";
            try { name = Clean(pack.Name); } catch { name = pack.BoosterId; }
            var currency = CurrencyOf(wm, pack.BoosterLocation);
            return new
            {
                id = pack.BoosterId,
                name,
                cost = pack.Cost,
                currency = Loc.Word("icon_" + currency.ToString().ToLowerInvariant(), pack.Cost),
                place = Loc.T("place_" + pack.BoosterLocation.ToString().ToLowerInvariant()),
                quests = pack.MinAchievementCount,
                intro = pack.IsIntroPack,
                ev = Math.Round(expected, 1),
                cards = ids.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList(),
                bags,
                img = images?.Save(pack.Icon, "pack_" + pack.BoosterId),
            };
        }

        /// <summary>Texts of the page, in the mod's language for the current game language.</summary>
        private static Dictionary<string, string> UiTexts()
        {
            var keys = new[]
            {
                "web_title", "web_search", "web_search_hint", "web_category", "web_all", "web_only_found", "web_count", "web_count_one",
                "web_how", "web_used", "web_packs", "web_drops", "web_from", "web_none", "web_close", "web_value", "web_food",
                "web_health", "web_attack", "web_defence", "web_category_label", "web_unknown", "web_rumor", "web_idea", "web_time",
                "web_generated", "web_generated_site", "web_type", "web_content", "web_obtain", "web_obtain_recipe", "web_obtain_pack",
                "web_obtain_drop", "web_obtain_none", "web_sort", "web_sort_name", "web_sort_value", "web_sort_food", "web_sort_attack", "web_sort_age", "web_age", "web_age_n", "web_age_unknown",
                "web_language", "web_reset", "web_loading", "web_group", "web_idea_recipes", "web_pack", "web_pack_cost", "web_pack_place", "web_pack_unlock", "web_pack_unlock_start", "web_pack_unlock_quests", "web_pack_ev", "web_pack_bags", "web_pack_bag", "web_pack_fixed", "web_pack_enemy", "web_pack_needs", "web_pack_max", "web_pack_intro", "web_pack_strength", "web_packs_cat", "web_pack_per_draw",
            };
            return keys.ToDictionary(k => k, k => Loc.T(k));
        }
    }
}
