using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine;
using R2API.AddressReferencedAssets;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using RoR2.Networking;
using RoR2.UI;
using R2API;
[assembly: HG.Reflection.SearchableAttribute.OptIn]
namespace RealerStageTweaker
{
    [BepInPlugin("zzz." + PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "RealerStageTweaker";
        public const string PluginVersion = "1.0.3";
        public static ManualLogSource Log;
        public static PluginInfo pluginInfo;
        public static ConfigFile Config;
        public static MPInput input;
        public static MPButton button;
        public static SimpleDialogBox box;
        public static ConfigEntry<bool> ResetConfig;
        public static ConfigEntry<bool> ResetConfig2;
        public static Dictionary<string, CharacterSpawnCard> Enemies = [];
        public static Dictionary<string, InteractableSpawnCard> Interactables = [];

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
            RoR2Application.onLoad += () => input = GameObject.Find("MPEventSystem Player0").GetComponent<MPInput>();
            ResetConfig = Config.Bind("!! General", "Refresh Config", true, "fetches all monster and interactable cards. requires restart.");
            ResetConfig2 = Config.Bind("!! General", "Reset Config", true, "resets the config.");
            var BlacklistStage1 = Config.Bind("!! General", "Blacklist for Stage 1", "lakesnight, villagenight, habitatfall", "list of stages that should not appear on stage 1. compatibility with the original StageTweaker.");
            if (ResetConfig.Value)
            {
                On.RoR2.RuleBook.IsChoiceActive += (orig, self, choice) => true;
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter += (orig, self, mainMenuController) =>
                {
                    orig(self, mainMenuController);
                    Time.timeScale = 0f;
                    input.eventSystem.cursorOpenerCount++;
                    input.eventSystem.cursorOpenerForGamepadCount++;
                    box = SimpleDialogBox.Create();
                    box.headerToken = new SimpleDialogBox.TokenParamsPair("Scanning Stages");
                    box.descriptionToken = new SimpleDialogBox.TokenParamsPair("Thank you for using Realer Stage Tweaker!\nCollecting stage and enemy data, please wait...");
                    AddressReferencedAsset.LoadReferencesAsync();
                };
            }
            else
            {
                SavedConfig.Load();
                RoR2Application.onLoad += () => SavedConfig.GetConfigs();
                On.RoR2.ClassicStageInfo.RebuildCards += (orig, self, a, b) =>
                {
                    var monsterCredit = SavedConfig.GetMonsterCredit(SceneCatalog.currentSceneDef);
                    if (monsterCredit != -1 && self.sceneDirectorMonsterCredits != monsterCredit) { Log.LogInfo("Patching Monster Credits"); self.sceneDirectorMonsterCredits = (int)monsterCredit; }
                    var interactableCredit = SavedConfig.GetInteractableCredit(SceneCatalog.currentSceneDef);
                    if (interactableCredit != -1 && self.sceneDirectorInteractibleCredits != interactableCredit) { Log.LogInfo("Patching Interactable Credits"); self.sceneDirectorInteractibleCredits = (int)interactableCredit; }
                    var monsters = SavedConfig.GetMonster(SceneCatalog.currentSceneDef);
                    var monstersLoop = SavedConfig.GetMonsterLoop(SceneCatalog.currentSceneDef);
                    if (monsters != null && monstersLoop != null) Apply.Monster(self, monsters, monstersLoop);
                    var families = SavedConfig.GetFamily(SceneCatalog.currentSceneDef);
                    if (families != null) Apply.Family(self, families);
                    var interactables = SavedConfig.GetInteractable(SceneCatalog.currentSceneDef);
                    if (interactables != null) Apply.Interactable(self, interactables);
                    Apply._Finalize(self);
                    orig(self, a, b);
                };
                On.RoR2.Run.Start += (orig, self) =>
                {
                    var list = BlacklistStage1.Value.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => SceneCatalog.GetSceneDefFromSceneName(x.Trim()));
                    var entry = self.startingSceneGroup._sceneEntries;
                    self.startingSceneGroup._sceneEntries = entry.Where(x => !list.Contains(x.sceneDef)).ToArray();
                    orig(self);
                    self.startingSceneGroup._sceneEntries = entry;
                };
            }
        }

        [SystemInitializer]
        public static void SystemInitializer()
        {
            if (!ResetConfig.Value) return;
            AddressReferencedAsset.OnAddressReferencedAssetsLoaded += GenerateConfigs;
        }

        // [SystemInitializer(typeof(SceneCatalog))]
        public static void GenerateConfigs()
        {
            // DirectorAPI.InitCustomMixEnemyArtifactDccs();
            if (ResetConfig.Value) Log.LogInfo("Deleting Existing Config");
            DirectorAPI.InitCustomMixEnemyArtifactDccs();
            foreach (var def in SceneCatalog.allStageSceneDefs) {
                var key = def.sceneAddress?.AssetGUID;
                if (!string.IsNullOrEmpty(key))
                {
                    if (!NetworkManagerSystem.IsAddressablesKeyValid(key, typeof(SceneInstance))) continue;
                    var _scene = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, true);
                    _scene.Completed += _ => loadScene(def);
                }
                else
                {
                    var _scene = SceneManager.LoadSceneAsync(def.cachedName, LoadSceneMode.Additive);
                    _scene.completed += _ => loadScene(def);
                }
            }
            static void loadScene(SceneDef def)
            {
                var scene = SceneManager.GetSceneByName(def.cachedName);
                var csi = scene.GetRootGameObjects().First(x => x.TryGetComponent<ClassicStageInfo>(out _)).GetComponent<ClassicStageInfo>();
                ClassicStageInfo.instance = csi;
                DirectorAPI.SetHooks();
                var si = DirectorAPI.GetStageInfo(csi);
                Run.instance = new Run() { networkRuleBookComponent = new() { ruleBook = new() { } } };
                RunArtifactManager.instance = new() { _enabledArtifacts = [], run = Run.instance };
                // im using all part of my knowledge learnt from the experiences
                if (csi)
                {
                    if (!csi.monsterDccsPool && csi.monsterCategories) DirectorAPI.PortToNewMonsterSystem(csi);
                    if (!csi.interactableDccsPool && csi.interactableCategories) DirectorAPI.PortToNewInteractableSystem(csi);
                    // msu compat
                    if (csi.monsterDccsPool && !csi.monsterDccsPool.poolCategories.Any(x => x.name == DirectorAPI.Helpers.MonsterPoolCategories.Standard))
                    {
                        if (csi.monsterDccsPool.poolCategories.Length == 0) csi.monsterDccsPool.poolCategories = [new() { name = DirectorAPI.Helpers.MonsterPoolCategories.Standard, categoryWeight = 1, alwaysIncluded = [], includedIfConditionsMet = [], includedIfNoConditionsMet = [] }];
                        else csi.monsterDccsPool.poolCategories[0].name = DirectorAPI.Helpers.MonsterPoolCategories.Standard;
                    }
                    if (csi.monsterDccsPool) DirectorAPI.ApplyMonsterChanges(csi, si);
                    if (csi.interactableDccsPool) DirectorAPI.ApplyInteractableChanges(csi, si);
                }
                Setup.Init(def, csi);
                SceneManager.UnloadSceneAsync(def.cachedName);
            }
        }
    }

    [BepInPlugin("___." + Main.PluginGUID, Main.PluginName, Main.PluginVersion)]
    public class Pre : BaseUnityPlugin
    {
        public static List<SpawnCard> ToSearch = [];
        public void Awake()
        {
            On.RoR2.SpawnCard.ctor += (orig, self) =>
            {
                orig(self);
                ToSearch.Add(self);
            };
        }
    }
}
