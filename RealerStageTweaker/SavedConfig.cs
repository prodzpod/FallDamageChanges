using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RoR2;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets;

namespace RealerStageTweaker
{
    public class SavedConfig
    {
        public static Dictionary<string, string> Defaults = [];
        public static Dictionary<string, List<string>> Families = [];
        public static Dictionary<string, string> Category = [];
        public static Dictionary<string, float> Credit = [];
        public static Dictionary<string, float> SceneCredit = [];
        public static Dictionary<string, float> CategoryWeight = [];
        public static Dictionary<string, int> SpawnDistance = [];
        public static Dictionary<string, bool> PreventOverhead = [];

        public static Dictionary<SceneDef, ConfigEntry<float>> ConfigMonsterCredit = [];
        public static Dictionary<SceneDef, ConfigEntry<float>> ConfigInteractableCredit = [];
        public static Dictionary<SceneDef, ConfigEntry<string>> ConfigMonsters = [];
        public static Dictionary<SceneDef, ConfigEntry<string>> ConfigMonstersLoop = [];
        public static Dictionary<SceneDef, ConfigEntry<string>> ConfigFamilies = [];
        public static Dictionary<SceneDef, ConfigEntry<string>> ConfigInteractables = [];

        public static void AddDefault(SceneDef def, string name, string val, string desc)
        {
            var key = def.cachedName + "." + name;
            Defaults[key] = val;
            var config = Main.Config.Bind(Setup.GetStageName(def), name, val, desc);
            if (Main.ResetConfig2.Value) config.Value = (string)config.DefaultValue;
        }
        public static void Save(string enemyList, string familyList, string interactableList)
        {
            Main.Log.LogInfo("Saving Persistent Configs");
            File.WriteAllText(System.IO.Path.Combine(Paths.ConfigPath, "RealerStageTweakerPersistent.json"), new JObject()
            {
                new JProperty("Defaults", JObject.Parse(JsonConvert.SerializeObject(Defaults))),
                new JProperty("Families", JObject.Parse(JsonConvert.SerializeObject(Families))),
                new JProperty("Category", JObject.Parse(JsonConvert.SerializeObject(Category))),
                new JProperty("Credit", JObject.Parse(JsonConvert.SerializeObject(Credit))),
                new JProperty("SceneCredit", JObject.Parse(JsonConvert.SerializeObject(SceneCredit))),
                new JProperty("CategoryWeight", JObject.Parse(JsonConvert.SerializeObject(CategoryWeight))),
                new JProperty("SpawnDistance", JObject.Parse(JsonConvert.SerializeObject(SpawnDistance))),
                new JProperty("PreventOverhead", JObject.Parse(JsonConvert.SerializeObject(PreventOverhead))),
                new JProperty("EnemyList", enemyList),
                new JProperty("FamilyList", familyList),
                new JProperty("InteractableList", interactableList),
            }.ToString());
            Main.Log.LogInfo("Saved Persistent Configs");
        }
        public struct _SavedConfig
        {
            public Dictionary<string, string> Defaults;
            public Dictionary<string, List<string>> Families;
            public Dictionary<string, string> Category;
            public Dictionary<string, float> Credit;
            public Dictionary<string, float> SceneCredit;
            public Dictionary<string, float> CategoryWeight;
            public Dictionary<string, int> SpawnDistance;
            public Dictionary<string, bool> PreventOverhead;
            public string EnemyList;
            public string FamilyList;
            public string InteractableList;
        }
        public static void Load()
        {
            Main.Log.LogInfo("Loading Persistent Configs");
            var main = JsonConvert.DeserializeObject<_SavedConfig>(File.ReadAllText(System.IO.Path.Combine(Paths.ConfigPath, "RealerStageTweakerPersistent.json")));
            Defaults = main.Defaults;
            Families = main.Families;
            Category = main.Category;
            Credit = main.Credit;
            SceneCredit = main.SceneCredit;
            CategoryWeight = main.CategoryWeight;
            SpawnDistance = main.SpawnDistance;
            PreventOverhead = main.PreventOverhead;
            Main.Config.Bind("!! General", "List of Enemies", main.EnemyList, "for convenience, changing this field does not do anything");
            Main.Config.Bind("!! General", "List of Family Events", main.FamilyList, "for convenience, changing this field does not do anything");
            Main.Config.Bind("!! General", "List of Interactables", main.InteractableList, "for convenience, changing this field does not do anything");
            // enemiesreturn compat
            Sync("cscColossusGrassy", "cscColossusDefault");
            Main.Log.LogInfo("Loaded Persistent Configs");
        }
        public static void Sync(string from, string to)
        {
            if (Category.TryGetValue(from, out var d1)) Category[to] = d1;
            if (Credit.TryGetValue(from, out var d2)) Credit[to] = d2;
            if (SceneCredit.TryGetValue(from, out var d3)) SceneCredit[to] = d3;
            if (CategoryWeight.TryGetValue(from, out var d4)) CategoryWeight[to] = d4;
            if (SpawnDistance.TryGetValue(from, out var d5)) SpawnDistance[to] = d5;
            if (PreventOverhead.TryGetValue(from, out var d6)) PreventOverhead[to] = d6;
        }
        public static void GetConfigs()
        {
            foreach (var def in SceneCatalog.allStageSceneDefs) 
            {
                var name = Setup.GetStageName(def);
                if (SceneCredit.TryGetValue(def.cachedName + ".monster", out var mc))
                    ConfigMonsterCredit.Add(def, Main.Config.Bind(name, "Monster Credits", mc, "Amount of monster credits this stage has at the beginning."));
                if (SceneCredit.TryGetValue(def.cachedName + ".interactable", out var ic))
                    ConfigInteractableCredit.Add(def, Main.Config.Bind(name, "Interactable Credits", ic, "Amount of interactable credits this stage has at the beginning."));
                if (Defaults.TryGetValue(def.cachedName + ".Monsters", out var def1))
                    ConfigMonsters.Add(def, Main.Config.Bind(name, "Monsters", def1, "Format: NAME=WEIGHT, separated by commas."));
                if (Defaults.TryGetValue(def.cachedName + ".Monsters Post Loop", out var def2))
                    ConfigMonstersLoop.Add(def, Main.Config.Bind(name, "Monsters Post Loop", def2, "monsters that can ONLY be spawned post loop. Format: NAME=WEIGHT, separated by commas."));
                if (Defaults.TryGetValue(def.cachedName + ".Family Events", out var def3))
                    ConfigFamilies.Add(def, Main.Config.Bind(name, "Family Events", def3, "list of possible family events. Separated by commas."));
                if (Defaults.TryGetValue(def.cachedName + ".Interactables", out var def4))
                    ConfigInteractables.Add(def, Main.Config.Bind(name, "Interactables", def4, "list of shrines, chests and other interactables to spawn. Format: NAME=WEIGHT, separated by commas."));
            }
            foreach (var sc in Pre.ToSearch)
            {
                if (sc is CharacterSpawnCard csc) if (!Main.Enemies.ContainsKey(csc.name.Replace(",", ""))) Main.Enemies.Add(csc.name.Replace(",", ""), csc);
                if (sc is InteractableSpawnCard isc) if (!Main.Interactables.ContainsKey(isc.name.Replace(",", ""))) Main.Interactables.Add(isc.name.Replace(",", ""), isc);
            }
            // do the harb on em
            foreach (var locator in Addressables.ResourceLocators) foreach (var key in locator.Keys) if (key.ToString().Contains("/csc"))
            {
                var rq = Addressables.LoadAssetAsync<CharacterSpawnCard>(key.ToString());
                rq.Completed += res =>
                {
                    if (res.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) return;
                    var csc = res.Result;
                    if (!csc) return;
                    var name = csc.name.Replace(",", "");
                    if (!Main.Enemies.ContainsKey(name)) Main.Enemies.Add(name, csc);
                };
            }
            foreach (var locator in Addressables.ResourceLocators) foreach (var key in locator.Keys) if (key.ToString().Contains("/isc"))
            {
                var rq = Addressables.LoadAssetAsync<InteractableSpawnCard>(key.ToString());
                rq.Completed += res =>
                {
                    if (res.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) return;
                    var isc = res.Result;
                    if (!isc) return;
                    var name = isc.name.Replace(",", "");
                    if (!Main.Interactables.ContainsKey(name)) Main.Interactables.Add(name, isc);
                };
            }
            Main.Log.LogInfo($"Loaded {Main.Enemies.Count} Enemy Spawncards (+ all basegame cards)");
            Main.Log.LogInfo($"Loaded {Main.Interactables.Count} Interactable Spawncards (+ all basegame cards)");
        }
        public static float GetMonsterCredit(SceneDef def)
        {
            if (ConfigMonsterCredit.TryGetValue(def, out var mc)) return mc.Value;
            Main.Log.LogWarning("this scene does not have persistent info! regenerate your config..."); return -1;
        }
        public static float GetInteractableCredit(SceneDef def)
        {
            if (ConfigInteractableCredit.TryGetValue(def, out var ic)) return ic.Value;
            Main.Log.LogWarning("this scene does not have persistent info! regenerate your config..."); return -1;
        }
        public static Dictionary<string, float> GetMonster(SceneDef def)
        {
            if (!ConfigMonsters.TryGetValue(def, out var val)) return null;
            return new Dictionary<string, float>(val.Value.Split(',').Where(x =>
            {
                if (string.IsNullOrWhiteSpace(x)) return false;
                if (x.Split('=').Length != 2) { Main.Log.LogWarning("Config is malformed! " + x); return false; }
                return true;
            }).Select(x => { var y = x.Split('='); return new KeyValuePair<string, float>(y[0].Trim(), float.Parse(y[1])); }));
        }
        public static Dictionary<string, float> GetMonsterLoop(SceneDef def)
        {
            if (!ConfigMonstersLoop.TryGetValue(def, out var val)) return null;
            return new Dictionary<string, float>(val.Value.Split(',').Where(x =>
            {
                if (string.IsNullOrWhiteSpace(x)) return false;
                if (x.Split('=').Length != 2) { Main.Log.LogWarning("Config is malformed! " + x); return false; }
                return true;
            }).Select(x => { var y = x.Split('='); return new KeyValuePair<string, float>(y[0].Trim(), float.Parse(y[1])); }));
        }
        public static List<string> GetFamily(SceneDef def)
        {
            if (!ConfigFamilies.TryGetValue(def, out var val)) return null;
            return val.Value.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        }
        public static Dictionary<string, float> GetInteractable(SceneDef def)
        {
            if (!ConfigInteractables.TryGetValue(def, out var val)) return null;
            return new Dictionary<string, float>(val.Value.Split(',').Where(x =>
            {
                if (string.IsNullOrWhiteSpace(x)) return false;
                if (x.Split('=').Length != 2) { Main.Log.LogWarning("Config is malformed! " + x); return false; }
                return true;
            }).Select(x => { var y = x.Split('='); return new KeyValuePair<string, float>(y[0].Trim(), float.Parse(y[1])); }));
        }
    }
}
