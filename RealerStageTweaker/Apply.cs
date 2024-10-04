using IL.RoR2.UI.MainMenu;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using static UnityEngine.UI.StencilMaterial;

namespace RealerStageTweaker
{
    public class Apply
    {
        public static void Monster(ClassicStageInfo csi, Dictionary<string, float> monsters, Dictionary<string, float> monstersLoop)
        {
            // Main.Log.LogInfo("Enemies: " + Setup.JoinAndOrderBy(Main.Enemies.Keys));
            // Main.Log.LogInfo("Interactables: " + Setup.JoinAndOrderBy(Main.Interactables.Keys));
            List<string> removed = [], changed = [], added = [];
            Main.Log.LogInfo("Patching Monsters");
            var mainCategory = GetCategory(csi.monsterDccsPool);
            var mainEntry = GetEntry(csi.monsterDccsPool); mainEntry ??= new() { dccs = ScriptableObject.CreateInstance<DirectorCardCategorySelection>(), weight = 1 };
            if (mainEntry is DccsPool.ConditionalPoolEntry e) e.requiredExpansions = [];
            mainCategory.alwaysIncluded = [..mainCategory.alwaysIncluded.Where(x => x.dccs is FamilyDirectorCardCategorySelection), mainEntry];
            mainCategory.includedIfConditionsMet = mainCategory.includedIfConditionsMet.Where(x => x.dccs is FamilyDirectorCardCategorySelection).ToArray();
            mainCategory.includedIfNoConditionsMet = mainCategory.includedIfNoConditionsMet.Where(x => x.dccs is FamilyDirectorCardCategorySelection).ToArray();
            for (var i = 0; i < mainEntry.dccs.categories.Length; i++)
            {
                var cat = mainEntry.dccs.categories[i];
                List<DirectorCard> cards = [];
                foreach (var d in cat.cards)
                {
                    var name = d.spawnCard?.name?.Replace(",", "");
                    if (string.IsNullOrWhiteSpace(name)) { Main.Log.LogWarning("invalid spawncard at " + d + "!"); continue; }
                    bool isLoop = false; float weight = 0;
                    // remove
                    if (monstersLoop.TryGetValue(name, out weight)) isLoop = true;
                    else if (!monsters.TryGetValue(name, out weight) || weight <= 0) { removed.Add(name); continue; }
                    // change
                    bool _changed = false;
                    bool _isLoop = d.minimumStageCompletions == 0;
                    if (_isLoop != isLoop) _changed = true;
                    d.minimumStageCompletions = isLoop ? 5 : 0;
                    if (d.selectionWeight != weight) { _changed = true; d.selectionWeight = (int)weight; }
                    if (_changed) changed.Add(name);
                    cards.Add(d); monsters.Remove(name); monstersLoop.Remove(name);
                }
                // add
                foreach (var name in monstersLoop.Keys.ToArray())
                {
                    if (!SavedConfig.Category.ContainsKey(name)) { monsters.Remove(name); monstersLoop.Remove(name); continue; }
                    if (SavedConfig.Category[name] == cat.name)
                    {
                        if (Main.Enemies.ContainsKey(name)) { Main.Log.LogWarning("Enemy failed to load: " + name + ", skipping"); monsters.Remove(name); monstersLoop.Remove(name); continue; }
                        cards.Add(MakeDirectorCard(name, Main.Enemies[name], monstersLoop[name], true));
                        added.Add(name); monsters.Remove(name); monstersLoop.Remove(name);
                    }
                }
                foreach (var name in monsters.Keys.ToArray())
                {
                    if (!SavedConfig.Category.ContainsKey(name)) { monsters.Remove(name); monstersLoop.Remove(name); continue; }
                    if (SavedConfig.Category[name] == cat.name)
                    {
                        if (Main.Enemies.ContainsKey(name)) { Main.Log.LogWarning("Enemy failed to load: " + name + ", skipping"); monsters.Remove(name); monstersLoop.Remove(name); continue; }
                        cards.Add(MakeDirectorCard(name, Main.Enemies[name], monsters[name], false));
                        added.Add(name); monsters.Remove(name); monstersLoop.Remove(name);
                    }
                }
                mainEntry.dccs.categories[i].cards = cards.ToArray();
            }
            // add invalid categories
            while (monstersLoop.Count > 0 || monsters.Count > 0)
            {
                var key = (monstersLoop.Count == 0 ? monsters.First() : monstersLoop.First()).Key;
                if (!SavedConfig.Category.ContainsKey(key)) { monsters.Remove(key); monstersLoop.Remove(key); continue; };
                var _category = SavedConfig.Category[key];
                DirectorCardCategorySelection.Category category = new()
                {
                    name = _category,
                    selectionWeight = SavedConfig.CategoryWeight[_category],
                    cards = []
                };
                List<DirectorCard> cards = [];
                foreach (var name in monstersLoop.Keys.ToArray())
                {
                    if (!SavedConfig.Category.ContainsKey(name)) { monsters.Remove(name); monstersLoop.Remove(name); continue; }
                    if (SavedConfig.Category[name] == category.name)
                    {
                        if (Main.Enemies.ContainsKey(name)) { Main.Log.LogWarning("Enemy failed to load: " + name + ", skipping"); monsters.Remove(name); monstersLoop.Remove(name); continue; }
                        cards.Add(MakeDirectorCard(name, Main.Enemies[name], monstersLoop[name], true));
                        added.Add(name); monsters.Remove(name); monstersLoop.Remove(name);
                    }
                }
                foreach (var name in monsters.Keys.ToArray())
                {
                    if (!SavedConfig.Category.ContainsKey(name)) { monsters.Remove(name); monstersLoop.Remove(name); continue; }
                    if (SavedConfig.Category[name] == category.name)
                    {
                        if (Main.Enemies.ContainsKey(name)) { Main.Log.LogWarning("Enemy failed to load: " + name + ", skipping"); monsters.Remove(name); monstersLoop.Remove(name); continue; }
                        cards.Add(MakeDirectorCard(name, Main.Enemies[name], monsters[name], false));
                        added.Add(name); monsters.Remove(name); monstersLoop.Remove(name);
                    }
                }
                category.cards = cards.ToArray();
                mainEntry.dccs.categories = [.. mainEntry.dccs.categories, category];
            }
            Main.Log.LogInfo($"Added {added.Count} ({Setup.JoinAndOrderBy(added)}), Changed {changed.Count} ({Setup.JoinAndOrderBy(changed)}), Removed {removed.Count} ({Setup.JoinAndOrderBy(removed)}) monsters");
        }
        public static Dictionary<string, DccsPool.ConditionalPoolEntry> Families = [];
        public static void Family(ClassicStageInfo csi, List<string> families)
        {
            List<string> removed = [], added = [];
            Main.Log.LogInfo("Patching Families");
            DccsPool.Category mainCategory = null; int maxFamilyCount = 0;
            foreach (var cat in csi.monsterDccsPool.poolCategories)
            {
                cat.alwaysIncluded ??= [];
                cat.includedIfConditionsMet ??= [];
                cat.includedIfNoConditionsMet ??= [];
                if (cat.name == "Families" || cat.name == "Family") { mainCategory = cat; maxFamilyCount = int.MaxValue; } // lock in
                bool found = cat.includedIfConditionsMet.Any(Setup.HasDLC);
                var _families = GetFamilies(cat);
                if (_families.Length > maxFamilyCount) { mainCategory = cat; maxFamilyCount = _families.Length; }
                // remove
                var contains = _families.Where(x => families.Contains(((FamilyDirectorCardCategorySelection)x.dccs).selectionChatString.Replace(",", "")));
                removed.AddRange(_families.Select(x => ((FamilyDirectorCardCategorySelection)x.dccs).selectionChatString.Replace(",", "")).Except(contains.Select(x => ((FamilyDirectorCardCategorySelection)x.dccs).selectionChatString.Replace(",", ""))));
                cat.alwaysIncluded = [.. cat.alwaysIncluded.Where(x => x.dccs is not FamilyDirectorCardCategorySelection || contains.Contains(x))];
                cat.includedIfConditionsMet = [.. cat.includedIfConditionsMet.Where(x => x.dccs is not FamilyDirectorCardCategorySelection || contains.Contains(x))];
                cat.includedIfNoConditionsMet = [.. cat.includedIfNoConditionsMet.Where(x => x.dccs is not FamilyDirectorCardCategorySelection || contains.Contains(x))];
                families.RemoveAll(contains.Select(x => ((FamilyDirectorCardCategorySelection)x.dccs).selectionChatString.Replace(",", "")).Contains);
            }
            // remove categories
            // csi.monsterDccsPool.poolCategories = [.. csi.monsterDccsPool.poolCategories.Where(x => x == mainCategory || x.alwaysIncluded.Length + x.includedIfConditionsMet.Length + x.includedIfNoConditionsMet.Length > 0)];
            // make category
            if (mainCategory == null)
            {
                mainCategory = new() { categoryWeight = ClassicStageInfo.monsterFamilyChance, name = "Family", alwaysIncluded = [], includedIfConditionsMet = [], includedIfNoConditionsMet = [] };
                csi.monsterDccsPool.poolCategories = [..csi.monsterDccsPool.poolCategories, mainCategory];
            }
            // add
            List<DccsPool.ConditionalPoolEntry> add = [];
            foreach (var family in families)
            {
                if (Families.TryGetValue(family, out var f)) add.Add(f);
                else
                {
                    if (!SavedConfig.Families.ContainsKey(family)) { Main.Log.LogWarning("family not found: " + family + ", skipping"); continue; }
                    FamilyDirectorCardCategorySelection dccs = ScriptableObject.CreateInstance<FamilyDirectorCardCategorySelection>();
                    dccs.name = "dccs" + Setup.ToTitleCase(family);
                    dccs.selectionChatString = family;
                    dccs.minimumStageCompletion = 0;
                    dccs.maximumStageCompletion = int.MaxValue;
                    dccs.categories = SavedConfig.Families[family].Where(x => SavedConfig.Category.ContainsKey(x) && SavedConfig.CategoryWeight.ContainsKey(SavedConfig.Category[x]) && Main.Enemies.ContainsKey(x)).Select(x => new DirectorCardCategorySelection.Category()
                    {
                        name = SavedConfig.Category[x],
                        selectionWeight = SavedConfig.CategoryWeight[SavedConfig.Category[x]],
                        cards = [MakeDirectorCard(x, Main.Enemies[x], 1, false)]
                    }).ToArray();
                    DccsPool.ConditionalPoolEntry entry = new()
                    {
                        dccs = dccs,
                        requiredExpansions = [],
                        weight = 1
                    };
                    Families.Add(family, entry);
                    add.Add(entry);
                }
            }
            mainCategory.includedIfConditionsMet = [.. mainCategory.includedIfConditionsMet, ..add];
            Main.Log.LogInfo($"Added {families.Count} ({Setup.JoinAndOrderBy(families)}), Removed {removed.Count} ({Setup.JoinAndOrderBy(removed)}) family events");
        }
        public static DccsPool.PoolEntry[] GetFamilies(DccsPool.Category category)
        {
            bool found = category.includedIfConditionsMet.Any(Setup.HasDLC);
            return [..category.alwaysIncluded.Where(x => x.dccs is FamilyDirectorCardCategorySelection), 
                ..(found ? category.includedIfConditionsMet : category.includedIfNoConditionsMet).Where(x => x.dccs is FamilyDirectorCardCategorySelection)];
        }
        public static void Interactable(ClassicStageInfo csi, Dictionary<string, float> interactables)
        {
            List<string> removed = [], changed = [], added = [];
            Main.Log.LogInfo("Patching Interactables");
            var mainCategory = GetCategory(csi.interactableDccsPool);
            var mainEntry = GetEntry(csi.interactableDccsPool); mainEntry ??= new() { dccs = ScriptableObject.CreateInstance<DirectorCardCategorySelection>(), weight = 1 };
            if (mainEntry is DccsPool.ConditionalPoolEntry e) e.requiredExpansions = [];
            mainCategory.alwaysIncluded = [mainEntry];
            mainCategory.includedIfConditionsMet = [];
            mainCategory.includedIfNoConditionsMet = [];
            for (var i = 0; i < mainEntry.dccs.categories.Length; i++)
            {
                var cat = mainEntry.dccs.categories[i];
                List<DirectorCard> cards = [];
                foreach (var d in cat.cards)
                {
                    var name = d.spawnCard?.name?.Replace(",", "");
                    if (string.IsNullOrWhiteSpace(name)) { Main.Log.LogWarning("invalid spawncard at " + d + "!"); continue; }
                    // remove & also ss2 compat
                    if (name == "iscShockDrone") continue;
                    float weight = 0;
                    if (!interactables.TryGetValue(name, out weight) || weight <= 0) { removed.Add(name); continue; }
                    // change
                    if (d.selectionWeight != weight) { changed.Add(name); d.selectionWeight = (int)weight; }
                    cards.Add(d); interactables.Remove(name);
                }
                // add
                foreach (var name in interactables.Keys.ToArray())
                {
                    if (!SavedConfig.Category.ContainsKey(name)) { interactables.Remove(name); continue; }
                    if (SavedConfig.Category[name] == cat.name)
                    {
                        if (Main.Interactables.ContainsKey(name)) { Main.Log.LogWarning("Interactable failed to load: " + name + ", skipping"); interactables.Remove(name); continue; }
                        cards.Add(MakeDirectorCard(name, Main.Interactables[name], interactables[name], false));
                        added.Add(name); interactables.Remove(name);
                    }
                }
                mainEntry.dccs.categories[i].cards = cards.ToArray();
            }
            // add invalid categories
            while (interactables.Count > 0)
            {
                var key = interactables.First().Key;
                if (!SavedConfig.Category.ContainsKey(key)) { interactables.Remove(key); continue; };
                var _category = SavedConfig.Category[key];
                DirectorCardCategorySelection.Category category = new()
                {
                    name = _category,
                    selectionWeight = SavedConfig.CategoryWeight[_category],
                    cards = []
                };
                List<DirectorCard> cards = [];
                foreach (var name in interactables.Keys.ToArray())
                {
                    if (!SavedConfig.Category.ContainsKey(name)) { interactables.Remove(name); continue; }
                    if (SavedConfig.Category[name] == category.name)
                    {
                        if (Main.Interactables.ContainsKey(name)) { Main.Log.LogWarning("Interactable failed to load: " + name + ", skipping"); interactables.Remove(name); continue; }
                        cards.Add(MakeDirectorCard(name, Main.Interactables[name], interactables[name], false));
                        added.Add(name); interactables.Remove(name);
                    }
                }
                category.cards = cards.ToArray();
                mainEntry.dccs.categories = [.. mainEntry.dccs.categories, category];
            }
            Main.Log.LogInfo($"Added {added.Count} ({Setup.JoinAndOrderBy(added)}), Changed {changed.Count} ({Setup.JoinAndOrderBy(changed)}), Removed {removed.Count} ({Setup.JoinAndOrderBy(removed)}) interactables");
        }
        public static void _Finalize(ClassicStageInfo self)
        {
            foreach (var cat in self.monsterDccsPool.poolCategories)
            {
                foreach (var entry in cat.alwaysIncluded)
                    entry.dccs.categories = entry.dccs.categories.Where(x => x.selectionWeight > 0 && x.cards.Count() > 0).ToArray();
                cat.alwaysIncluded = cat.alwaysIncluded.Where(x => x.weight > 0 && x.dccs.categories.Length > 0).ToArray();
                foreach (var entry in cat.includedIfConditionsMet)
                    entry.dccs.categories = entry.dccs.categories.Where(x => x.selectionWeight > 0 && x.cards.Count() > 0).ToArray();
                cat.includedIfConditionsMet = cat.includedIfConditionsMet.Where(x => x.weight > 0 && x.dccs.categories.Length > 0).ToArray();
                foreach (var entry in cat.includedIfNoConditionsMet)
                    entry.dccs.categories = entry.dccs.categories.Where(x => x.selectionWeight > 0 && x.cards.Count() > 0).ToArray();
                cat.includedIfNoConditionsMet = cat.includedIfNoConditionsMet.Where(x => x.weight > 0 && x.dccs.categories.Length > 0).ToArray();
            }
            self.monsterDccsPool.poolCategories = self.monsterDccsPool.poolCategories.Where(x => x.categoryWeight > 0 && x.alwaysIncluded.Length + x.includedIfConditionsMet.Length + x.includedIfNoConditionsMet.Length > 0).ToArray();
            foreach (var cat in self.interactableDccsPool.poolCategories)
            {
                foreach (var entry in cat.alwaysIncluded)
                    entry.dccs.categories = entry.dccs.categories.Where(x => x.selectionWeight > 0 && x.cards.Count() > 0).ToArray();
                cat.alwaysIncluded = cat.alwaysIncluded.Where(x => x.weight > 0 && x.dccs.categories.Length > 0).ToArray();
                foreach (var entry in cat.includedIfConditionsMet)
                    entry.dccs.categories = entry.dccs.categories.Where(x => x.selectionWeight > 0 && x.cards.Count() > 0).ToArray();
                cat.includedIfConditionsMet = cat.includedIfConditionsMet.Where(x => x.weight > 0 && x.dccs.categories.Length > 0).ToArray();
                foreach (var entry in cat.includedIfNoConditionsMet)
                    entry.dccs.categories = entry.dccs.categories.Where(x => x.selectionWeight > 0 && x.cards.Count() > 0).ToArray();
                cat.includedIfNoConditionsMet = cat.includedIfNoConditionsMet.Where(x => x.weight > 0 && x.dccs.categories.Length > 0).ToArray();
            }
            self.interactableDccsPool.poolCategories = self.interactableDccsPool.poolCategories.Where(x => x.categoryWeight > 0 && x.alwaysIncluded.Length + x.includedIfConditionsMet.Length + x.includedIfNoConditionsMet.Length > 0).ToArray();
            if (self.monsterDccsPool.poolCategories.Length == 0) Main.Log.LogWarning("Monster pool is empty! did you empty the entire list??");
            if (self.interactableDccsPool.poolCategories.Length == 0) Main.Log.LogWarning("Interactable pool is empty! did you empty the entire list??");
            Main.Log.LogInfo("Patch Completed!");
        }
        public static DirectorCard MakeDirectorCard(string name, SpawnCard sc, float weight, bool isLoop) => new()
        {
            spawnCard = sc,
            selectionWeight = (int)weight,
            minimumStageCompletions = isLoop ? 5 : 0,
            spawnDistance = (DirectorCore.MonsterSpawnDistance)SavedConfig.SpawnDistance[name],
            preventOverhead = SavedConfig.PreventOverhead[name],
        };
        public static DccsPool.PoolEntry GetEntry(DccsPool dp) => GetEntry(dp, Setup.GetMainDCCS(dp));
        public static DccsPool.PoolEntry GetEntry(DccsPool dp, DirectorCardCategorySelection dccs)
        {
            foreach (var cat in dp.poolCategories)
            {
                foreach (var d1 in cat.alwaysIncluded) if (d1.dccs == dccs) return d1;
                foreach (var d2 in cat.includedIfConditionsMet) if (d2.dccs == dccs) return d2;
                foreach (var d3 in cat.includedIfNoConditionsMet) if (d3.dccs == dccs) return d3;
            }
            return null;
        }
        public static DccsPool.Category GetCategory(DccsPool dp) => GetCategory(dp, Setup.GetMainDCCS(dp));
        public static DccsPool.Category GetCategory(DccsPool dp, DccsPool.PoolEntry entry)
        {
            foreach (var cat in dp.poolCategories)
            {
                if (cat.alwaysIncluded.Contains(entry)
                    || cat.includedIfConditionsMet.Contains(entry)
                    || cat.includedIfNoConditionsMet.Contains(entry)) return cat;
            }
            return null;
        }
        public static DccsPool.Category GetCategory(DccsPool dp, DirectorCardCategorySelection dccs)
        {
            foreach (var cat in dp.poolCategories)
            {
                if (cat.alwaysIncluded.Any(x => x.dccs == dccs)
                    || cat.includedIfConditionsMet.Any(x => x.dccs == dccs)
                    || cat.includedIfNoConditionsMet.Any(x => x.dccs == dccs)) return cat;
            }
            return null;
        }
        public static bool TryFirst<T>(IEnumerable<T> arr, Func<T, bool> fn, out T ret)
        {
            ret = default;
            foreach (var item in arr) if (fn(item)) { ret = item; return true; }
            return false;
        }
    }
}
