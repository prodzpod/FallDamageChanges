using RoR2;
using RoR2.UI;
using RoR2.UI.MainMenu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace RealerStageTweaker
{
    public class Setup
    {
        public static Dictionary<string, string> totalMonsters = [];
        public static Dictionary<string, string> totalFamilies = [];
        public static Dictionary<string, string> totalInteractables = [];
        public static int processedScenes = 0;
        public static void Init(SceneDef def, ClassicStageInfo csi)
        {
            if (csi == null) { Finalize_(); return; }
            SavedConfig.SceneCredit[def.cachedName + ".monster"] = csi.sceneDirectorMonsterCredits;
            var config = Main.Config.Bind(GetStageName(def), "Monster Credits", (float)csi.sceneDirectorMonsterCredits, "Amount of monster credits this stage has at the beginning.");
            if (Main.ResetConfig2.Value) config.Value = (float)config.DefaultValue;
            SavedConfig.SceneCredit[def.cachedName + ".interactable"] = csi.sceneDirectorMonsterCredits;
            var config2 = Main.Config.Bind(GetStageName(def), "Interactable Credits", (float)csi.sceneDirectorInteractibleCredits, "Amount of interactable credits this stage has at the beginning.");
            if (Main.ResetConfig2.Value) config2.Value = (float)config2.DefaultValue;
            if (def.cachedName == "voidraid") { Finalize_(); return; }

            Dictionary<CharacterSpawnCard, float> monsters = [];
            Dictionary<CharacterSpawnCard, float> monstersLoop = [];
            List<FamilyDirectorCardCategorySelection> families = [];
            Dictionary<InteractableSpawnCard, float> interactables = [];

            var monsterDCCS = GetMainDCCS(csi.monsterDccsPool);
            if (monsterDCCS != null) foreach (var category in monsterDCCS.categories)
            {
                foreach (var enemy in category.cards)
                {
                    var csc = enemy.spawnCard as CharacterSpawnCard;
                    if (csc == null) continue;
                    var name = csc.name.Replace(",", "");
                    if (!SavedConfig.Category.ContainsKey(name)) SavedConfig.Category.Add(name, category.name);
                    if (!SavedConfig.Credit.ContainsKey(name)) SavedConfig.Credit.Add(name, enemy.cost);
                    if (!SavedConfig.SpawnDistance.ContainsKey(name)) SavedConfig.SpawnDistance.Add(name, (int)enemy.spawnDistance);
                    if (!SavedConfig.PreventOverhead.ContainsKey(name)) SavedConfig.PreventOverhead.Add(name, enemy.preventOverhead);
                    if (!SavedConfig.CategoryWeight.ContainsKey(category.name)) SavedConfig.CategoryWeight.Add(category.name, category.selectionWeight);
                    var dict = enemy.minimumStageCompletions < def.stageOrder ? monsters : monstersLoop;
                    if (dict.ContainsKey(csc)) dict[csc] += enemy.selectionWeight;
                    else dict.Add(csc, enemy.selectionWeight);
                }
            }
            var familyDCCS = GetFamilyDCCS(csi.monsterDccsPool);
            foreach (var family in familyDCCS)
            {
                var name = family.selectionChatString.Replace(",", "");
                if (!SavedConfig.Families.ContainsKey(family.selectionChatString))
                {
                    List<string> names = [];
                    foreach (var c in family.categories) foreach (var c2 in c.cards)
                            names.Add(c2.spawnCard.name.Replace(",", ""));
                    SavedConfig.Families.Add(family.selectionChatString, names);
                }
                families.Add(family);
            }
            var interactableDCCS = GetMainDCCS(csi.interactableDccsPool);
            if (interactableDCCS != null)
            {
                foreach (var category in interactableDCCS.categories)
                {
                    foreach (var interactable in category.cards)
                    {
                        var isc = interactable.spawnCard as InteractableSpawnCard;
                        if (isc == null) continue;
                        var name = isc.name.Replace(",", "");
                        if (!SavedConfig.Category.ContainsKey(name)) SavedConfig.Category.Add(name, category.name);
                        if (!SavedConfig.Credit.ContainsKey(name)) SavedConfig.Credit.Add(name, interactable.cost);
                        if (!SavedConfig.SpawnDistance.ContainsKey(name)) SavedConfig.SpawnDistance.Add(name, (int)interactable.spawnDistance);
                        if (!SavedConfig.PreventOverhead.ContainsKey(name)) SavedConfig.PreventOverhead.Add(name, interactable.preventOverhead);
                        if (!SavedConfig.CategoryWeight.ContainsKey(category.name)) SavedConfig.CategoryWeight.Add(category.name, category.selectionWeight);
                        if (interactables.ContainsKey(isc)) interactables[isc] += interactable.selectionWeight;
                        else interactables.Add(isc, interactable.selectionWeight);
                    }
                }
            }
            if (monsters.Count == 0 && monstersLoop.Count == 0 && families.Count == 0 && interactables.Count == 0) { Finalize_(); return; }
            Main.Log.LogInfo("Adding Configs for " + GetStageName(def));
            SavedConfig.AddDefault(def, "Monsters", JoinAndOrderBy(monsters, x => x.Key.name.Replace(",", "") + "=" + x.Value, x => x.Key.name.Replace(",", "")), "Format: NAME=WEIGHT, separated by commas.");
            foreach (var item in monsters) if (!totalMonsters.ContainsKey(item.Key.name)) totalMonsters.Add(item.Key.name.Replace(",", ""), GetEnemyName(item.Key));
            SavedConfig.AddDefault(def, "Monsters Post Loop", JoinAndOrderBy(monstersLoop, x => x.Key.name.Replace(",", "") + "=" + x.Value, x => x.Key.name.Replace(",", "")), "monsters that can ONLY be spawned post loop. Format: NAME=WEIGHT, separated by commas.");
            foreach (var item in monstersLoop) if (!totalMonsters.ContainsKey(item.Key.name)) totalMonsters.Add(item.Key.name.Replace(",", ""), GetEnemyName(item.Key));
            SavedConfig.AddDefault(def, "Family Events", JoinAndOrderBy(families, x => x.selectionChatString.Replace(",", ""), x => x.selectionChatString.Replace(",", "")), "list of possible family events. Separated by commas.");
            foreach (var item in families) if (!totalFamilies.ContainsKey(item.selectionChatString)) totalFamilies.Add(item.selectionChatString.Replace(",", ""), GetFamilyName(item));
            SavedConfig.AddDefault(def, "Interactables", JoinAndOrderBy(interactables, x => x.Key.name.Replace(",", "") + "=" + x.Value, x => x.Key.name.Replace(",", "")), "list of shrines, chests and other interactables to spawn. Format: NAME=WEIGHT, separated by commas.");
            foreach (var item in interactables) if (!totalInteractables.ContainsKey(item.Key.name)) totalInteractables.Add(item.Key.name.Replace(",", ""), GetInteractableName(item.Key));
            Finalize_();
        }
        public static void Finalize_()
        {
            processedScenes++;
            if (processedScenes >= SceneCatalog.allStageSceneDefs.Length)
            {
                Main.Log.LogInfo("Finalizing Load");
                var enemyList = JoinAndOrderBy(totalMonsters, x => x.Key + "=" + x.Value, x => x.Value);
                Main.Config.Bind("!! General", "List of Enemies", enemyList, "for convenience, changing this field does not do anything").Value = string.Join(", ", totalMonsters.Select(x => x.Key));
                var familyList = JoinAndOrderBy(totalFamilies, x => x.Key + "=" + x.Value, x => x.Value);
                Main.Config.Bind("!! General", "List of Family Events", familyList, "for convenience, changing this field does not do anything").Value = string.Join(", ", totalFamilies.Select(x => x.Key));
                var interactableList = JoinAndOrderBy(totalInteractables, x => x.Key + "=" + x.Value, x => x.Value);
                Main.Config.Bind("!! General", "List of Interactables", interactableList, "for convenience, changing this field does not do anything").Value = string.Join(", ", totalInteractables.Select(x => x.Key));
                SavedConfig.Save(enemyList, familyList, interactableList);
                Main.ResetConfig.Value = false;
                Main.ResetConfig2.Value = false;
                UnityEngine.Object.DestroyImmediate(Main.box.rootObject);
                DisplayEndMessage();
            }
        }
        public async static void DisplayEndMessage()
        {
            await Task.Delay(250);
            Main.box = SimpleDialogBox.Create();
            Main.box.headerToken = new SimpleDialogBox.TokenParamsPair("Scan Complete");
            Main.box.descriptionToken = new SimpleDialogBox.TokenParamsPair("Config files have been generated!\nA restart is required for the change to take effect.");
            Main.box.AddActionButton(() => GameObject.Find("MainMenu").GetComponent<MainMenuController>().titleMenuScreen.GetComponent<TitleMenuController>().consoleFunctions.SubmitCmd("quit"), "Close Game");
        }

        public static string JoinAndOrderBy(IEnumerable<string> arr, string joiner = ", ") => JoinAndOrderBy(arr, x => x, x => x, joiner);
        public static string JoinAndOrderBy<T>(IEnumerable<T> arr, Func<T, string> fn, Func<T, string> cmp, string joiner = ", ")
        {
            var ret = arr.ToList();
            ret.Sort((a, b) => cmp(a).CompareTo(cmp(b)));
            return string.Join(joiner, ret.Select(fn));
        }

        public static string GetStageName(SceneDef def)
        {
            return Language.GetString(def.nameToken).Replace("\n", "").Replace("\t", "").Replace("\\", "").Replace("\"", "").Replace("\'", "").Replace("[", "").Replace("]", "") + $" ({def.cachedName})";
        }

        public static int ID = 0;
        public static Dictionary<object, int> IDToKey = [];
        public static string GetEnemyName(CharacterSpawnCard csc)
        {
            if (IDToKey.ContainsKey(csc)) return "OBJECT_" + IDToKey[csc];
            if (csc && csc.prefab && csc.prefab.GetComponent<CharacterMaster>()
                && csc.prefab.GetComponent<CharacterMaster>().bodyPrefab && csc.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>()) return Language.GetString(csc.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().baseNameToken).Trim();
            var ret = csc.prefab.name.Trim();
            if (string.IsNullOrEmpty(ret))
            {
                IDToKey.Add(csc, ID);
                ID++;
                return "OBJECT_" + IDToKey[csc];
            }
            return ret;
        }

        public static string GetFamilyName(FamilyDirectorCardCategorySelection dccs)
        {
            if (IDToKey.ContainsKey(dccs)) return "OBJECT_" + IDToKey[dccs];
            var familyName = dccs.selectionChatString.ToLower();
            if (familyName.StartsWith("family_")) familyName = familyName["family_".Length..];
            var ret = ToTitleCase(familyName);
            if (string.IsNullOrEmpty(ret))
            {
                IDToKey.Add(dccs, ID);
                ID++;
                return "OBJECT_" + IDToKey[dccs];
            }
            return ret;
        }

        public static string ToTitleCase(string str)
        {
            str = str.ToLower();
            if (str.Length > 0) str = str[0..1].ToUpper() + (str.Length > 1 ? str[1..] : "");
            for (int i = 1; i < str.Length; i++) if (str[i - 1] == ' ')
                    str = str[..i] + str[i..(i + 1)].ToUpper() + (str.Length > (i + 1) ? str[(i + 1)..] : "");
            return str.Trim();
        }

        public static string GetInteractableName(InteractableSpawnCard isc)
        {
            if (IDToKey.ContainsKey(isc)) return "OBJECT_" + IDToKey[isc];
            if (isc && isc.prefab && isc.prefab.GetComponent<PurchaseInteraction>()) return Language.GetString(isc.prefab.GetComponent<PurchaseInteraction>().displayNameToken).Trim();
            if (isc && isc.prefab && isc.prefab.GetComponent<BarrelInteraction>()) return Language.GetString(isc.prefab.GetComponent<BarrelInteraction>().displayNameToken).Trim();
            if (isc && isc.prefab && isc.prefab.GetComponent<MultiShopController>() && isc.prefab.GetComponent<MultiShopController>().terminalPrefab && isc.prefab.GetComponent<MultiShopController>().terminalPrefab.GetComponent<PurchaseInteraction>()) return Language.GetString(isc.prefab.GetComponent<MultiShopController>().terminalPrefab.GetComponent<PurchaseInteraction>().displayNameToken).Trim();
            var ret = isc.prefab.name.Trim();
            if (string.IsNullOrEmpty(ret))
            {
                IDToKey.Add(isc, ID);
                ID++;
                return "OBJECT_" + IDToKey[isc];
            }
            return ret;
        }
        public static DirectorCardCategorySelection GetMainDCCS(DccsPool dp)
        {
            if (dp == null || dp.poolCategories == null) return null;
            foreach (var category in dp.poolCategories)
            {
                bool found = false;
                var l = category.includedIfConditionsMet.ToList();
                l.Sort((a, b) => DLCList(b).CompareTo(DLCList(a)));
                foreach (var entry in l) if (HasDLC(entry)) 
                {
                    found = true;
                    if (entry.dccs is not FamilyDirectorCardCategorySelection) return entry.dccs;
                }
                if (!found) foreach (var entry in category.includedIfNoConditionsMet) if (entry.dccs is not FamilyDirectorCardCategorySelection) return entry.dccs;
                foreach (var entry in category.alwaysIncluded) if (entry.dccs is not FamilyDirectorCardCategorySelection) return entry.dccs;
            }
            return null;
        }
        public static FamilyDirectorCardCategorySelection[] GetFamilyDCCS(DccsPool dp)
        {
            if (dp == null || dp.poolCategories == null) return [];
            List<FamilyDirectorCardCategorySelection> ret = [];
            foreach (var category in dp.poolCategories)
            {
                foreach (var entry in category.alwaysIncluded) if (entry.dccs is FamilyDirectorCardCategorySelection) ret.Add(entry.dccs as FamilyDirectorCardCategorySelection);
                bool found = false;
                foreach (var entry in category.includedIfConditionsMet) if (HasDLC(entry)) 
                { 
                    found = true; 
                    if (entry.dccs is FamilyDirectorCardCategorySelection) 
                        ret.Add(entry.dccs as FamilyDirectorCardCategorySelection); 
                }
                if (!found) foreach (var entry in category.includedIfNoConditionsMet) 
                    if (entry.dccs is FamilyDirectorCardCategorySelection) 
                        ret.Add(entry.dccs as FamilyDirectorCardCategorySelection);
            }
            return ret.ToArray();
        }
        public static int DLCList(DccsPool.ConditionalPoolEntry entry)
        {
            int ret = 0;
            if (entry.requiredExpansions.Any(x => x.name == "DLC1")) ret += 1;
            if (entry.requiredExpansions.Any(x => x.name == "DLC2")) ret += 2;
            return ret;
        }
        public static bool HasDLC(DccsPool.ConditionalPoolEntry entry) => entry.requiredExpansions.All(x =>
        {
            if (x.name == "DLC1") return SceneCatalog.FindSceneIndex("snowyforest") != SceneIndex.Invalid;
            if (x.name == "DLC2") return SceneCatalog.FindSceneIndex("habitat") != SceneIndex.Invalid;
            Main.Log.LogWarning("unchecked DLC found: " + x.name + "! let prod know of this.");
            return false;
        });
    }
}
