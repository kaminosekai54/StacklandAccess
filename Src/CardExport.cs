using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// F6: writes "Stacklands - cards.html" in the Documents folder: every card of the game in a searchable grid,
    /// with recipes, packs and drops, in the game's language. The page is built from cards_viewer.html
    /// (shipped with the mod) and the data read from the game itself, so it follows updates and DLCs.
    /// </summary>
    public static class CardExport
    {
        private const string TemplateName = "cards_viewer.html";
        private const string DataMarker = "__STACKLANDS_DATA__";

        public static void Run()
        {
            try
            {
                var wm = WorldManager.instance;
                if (wm == null || wm.GameDataLoader == null) { Speech.Say(Loc.T("not_now")); return; }
                string modPath = StacklandAccess.Instance != null ? StacklandAccess.Instance.Path : null;
                string template = modPath != null ? Path.Combine(modPath, TemplateName) : null;
                if (template == null || !File.Exists(template)) { Speech.Say(Loc.T("export_no_template")); return; }

                string json = JsonConvert.SerializeObject(BuildData(wm), Formatting.None).Replace("</", "<\\/");
                string html = File.ReadAllText(template).Replace(DataMarker, json);
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs) || !Directory.Exists(docs)) docs = modPath;
                string file = Path.Combine(docs, Loc.T("export_file_name") + ".html");
                File.WriteAllText(file, html, new System.Text.UTF8Encoding(false));
                Speech.Say(Loc.T("export_done", file));
                Application.OpenURL(new Uri(file).AbsoluteUri);
            }
            catch (Exception ex)
            {
                Log.Once(ex);
                Speech.Say(Loc.T("export_failed"));
            }
        }

        private static string Clean(string s) => TextCleaner.Clean(s ?? "");

        private static object BuildData(WorldManager wm)
        {
            var loader = wm.GameDataLoader;
            var cards = new List<object>();
            var seen = new HashSet<string>();
            foreach (var cd in wm.CardDataPrefabs)
            {
                if (cd == null || string.IsNullOrEmpty(cd.Id) || !seen.Add(cd.Id)) continue;
                if (cd is Blueprint) continue; // ideas are listed with the recipes
                string name = "", desc = "";
                try { name = Clean(cd.FullName); } catch { name = cd.Id; }
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
                bool cities = false;
                try { cities = cd.CardUpdateType == CardUpdateType.Cities; } catch { }
                cards.Add(new
                {
                    id = cd.Id,
                    name,
                    desc,
                    cat = BoardNavigator.CategoryName(BoardNavigator.CategoryOf(cd)),
                    value = cd.Value,
                    food = cd is Food f ? (int?)f.FoodValue : null,
                    hp, atk, def,
                    found = wm.HasFoundCard(cd.Id),
                    cities,
                    rumor = cd is Rumor,
                    drops = drops != null && drops.Count > 0 ? drops : null,
                });
            }

            var recipes = new List<object>();
            foreach (var bp in wm.BlueprintPrefabs)
            {
                if (bp?.Subprints == null) continue;
                string idea = "";
                try { idea = Clean(bp.KnowledgeName); } catch { }
                string group = "";
                try
                {
                    string g = SokLoc.Translate("ideagroup_" + bp.BlueprintGroup.ToString().ToLowerInvariant());
                    group = string.IsNullOrEmpty(g) || g.StartsWith("---") ? bp.BlueprintGroup.ToString() : Clean(g);
                }
                catch { }
                bool cities = false;
                try { var data = loader.GetCardFromId(bp.Id); cities = data != null && data.CardUpdateType == CardUpdateType.Cities; } catch { }
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
                        found = wm.HasFoundCard(bp.Id),
                        hidden = bp.HideFromIdeasTab,
                        cities,
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
                var ids = new List<string>();
                try { foreach (var bag in pack.CardBags) ids.AddRange(bag.GetCardsInBag(loader)); } catch { }
                string name = "";
                try { name = Clean(pack.Name); } catch { name = pack.BoosterId; }
                packs.Add(new { id = pack.BoosterId, name, cost = pack.Cost, cards = ids.Distinct().ToList() });
            }

            string lang = "";
            try { lang = SokLoc.instance != null ? SokLoc.instance.CurrentLanguage ?? "" : ""; } catch { }
            return new
            {
                lang,
                generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ui = UiTexts(),
                cards,
                recipes,
                packs,
            };
        }

        /// <summary>Texts of the page, in the mod's language for the game's language.</summary>
        private static Dictionary<string, string> UiTexts()
        {
            var keys = new[]
            {
                "web_title", "web_search", "web_search_hint", "web_category", "web_all", "web_only_found", "web_count", "web_count_one",
                "web_how", "web_used", "web_packs", "web_drops", "web_from", "web_none", "web_close", "web_value", "web_food",
                "web_health", "web_attack", "web_defence", "web_category_label", "web_unknown", "web_rumor", "web_idea", "web_time",
                "web_generated", "web_cities",
            };
            return keys.ToDictionary(k => k, k => Loc.T(k));
        }
    }
}
