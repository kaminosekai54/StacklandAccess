using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace StacklandAccessNS
{
    /// <summary>
    /// Recipes the player knows: the ideas found, plus the recipes made without the idea (by chance or by trying).
    /// The game does not remember the latter, so the mod does: every finished recipe is written in
    /// StacklandAccess_crafted.txt, next to the game's saves. For crafts made before that, a recipe also
    /// counts as known when its result has been found and nothing else in the game gives that card.
    /// </summary>
    [HarmonyPatch]
    public static class KnownRecipes
    {
        private static HashSet<string> crafted;
        private static HashSet<string> onlyFromRecipe;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "StacklandAccess_crafted.txt");

        private static HashSet<string> Crafted
        {
            get
            {
                if (crafted != null) return crafted;
                crafted = new HashSet<string>();
                try
                {
                    if (File.Exists(FilePath))
                        foreach (var l in File.ReadAllLines(FilePath, Encoding.UTF8)) if (!string.IsNullOrWhiteSpace(l)) crafted.Add(l.Trim());
                }
                catch (System.Exception ex) { Log.Once(ex); }
                return crafted;
            }
        }

        [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.BlueprintComplete))]
        [HarmonyPostfix]
        public static void BlueprintComplete_Postfix(Blueprint __instance)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(__instance.Id) || Crafted.Contains(__instance.Id)) return;
                Crafted.Add(__instance.Id);
                File.AppendAllText(FilePath, __instance.Id + "\n", Encoding.UTF8);
            }
            catch (System.Exception ex) { Log.Once(ex); }
        }

        /// <summary>True when the player knows this recipe: idea found, or made at least once.</summary>
        public static bool IsKnown(Blueprint bp)
        {
            var wm = WorldManager.instance;
            if (bp == null || wm == null) return false;
            if (wm.HasFoundCard(bp.Id) || Crafted.Contains(bp.Id)) return true;
            return MadeBefore(bp);
        }

        /// <summary>Made without the idea (from the cards found): the result was found and only this recipe gives it.</summary>
        public static bool MadeBefore(Blueprint bp)
        {
            var wm = WorldManager.instance;
            if (bp?.Subprints == null || wm == null) return false;
            var only = OnlyFromRecipe();
            foreach (var sp in bp.Subprints)
            {
                if (sp == null || string.IsNullOrEmpty(sp.ResultCard)) continue;
                if (only.Contains(sp.ResultCard) && wm.HasFoundCard(sp.ResultCard)) return true;
            }
            return false;
        }

        /// <summary>Cards that come only from one recipe: not from a pack, not dropped or harvested, not made by another idea.</summary>
        private static HashSet<string> OnlyFromRecipe()
        {
            if (onlyFromRecipe != null) return onlyFromRecipe;
            var wm = WorldManager.instance;
            var set = new HashSet<string>();
            if (wm == null) return set;
            var elsewhere = new HashSet<string>();
            var loader = wm.GameDataLoader;
            try
            {
                foreach (var pack in wm.BoosterPackDatas ?? new List<BoosterpackData>())
                {
                    if (pack == null) continue;
                    foreach (var bag in pack.CardBags ?? new List<CardBag>())
                        try { elsewhere.UnionWith(bag.GetCardsInBag(loader)); } catch { }
                    foreach (var add in pack.BoosterAdditions ?? new List<BoosterAddition>())
                        foreach (var bag in add?.CardBags ?? new List<CardBag>())
                            try { elsewhere.UnionWith(bag.GetCardsInBag(loader)); } catch { }
                }
                foreach (var cd in wm.CardDataPrefabs)
                {
                    if (cd == null) continue;
                    try { var drops = cd.GetPossibleDrops(); if (drops != null) elsewhere.UnionWith(drops); } catch { }
                }
            }
            catch (System.Exception ex) { Log.Once(ex); }
            var count = new Dictionary<string, int>();
            foreach (var bp in wm.BlueprintPrefabs)
            {
                if (bp?.Subprints == null) continue;
                foreach (var id in bp.Subprints.Where(s => s != null && !string.IsNullOrEmpty(s.ResultCard)).Select(s => s.ResultCard).Distinct())
                    count[id] = count.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            foreach (var kv in count)
                if (kv.Value == 1 && !elsewhere.Contains(kv.Key)) set.Add(kv.Key);
            onlyFromRecipe = set;
            return set;
        }
    }
}
