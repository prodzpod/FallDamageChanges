using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace LimitedInteractables
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "FallDamageChanges";
        public const string PluginVersion = "1.0.0";
        public static ManualLogSource Log;
        public static PluginInfo pluginInfo;
        public static ConfigFile Config;
        public static ConfigEntry<bool> FallIsLethal;
        public static ConfigEntry<float> FallMultiplier;
        public static ConfigEntry<float> OOBMultiplier;
        public static ConfigEntry<float> FallThreshold;
        public static ConfigEntry<float> OOBThreshold;
        public static ConfigEntry<float> FallIFrames;
        public static ConfigEntry<float> OOBIFrames;
        public static ConfigEntry<float> CritFall;
        public static List<CharacterBody> oob = new();

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);

            FallIsLethal = Config.Bind("General", "Fall Damage is Lethal", false, "Eclipse Real??");
            FallMultiplier = Config.Bind("General", "Fall Damage Multiplier", 1f, "Affects all fall damage.");
            OOBMultiplier = Config.Bind("General", "Out of Bounds Damage Multiplier", 1f, "Affects damage from falling off the map.");
            FallThreshold = Config.Bind("General", "Fall Damage Threshold", 0f, "Portion of HP that fall damage can't go below. default is 0 for 1HP. set to 1 to disable fall damage.");
            OOBThreshold = Config.Bind("General", "Out of Bounds Damage Threshold", 0.3f, "Portion of HP that falling off the map can't deal below. Default is just enough to not pop potion/stealthkit/gloop/break low health stuff.");
            FallIFrames = Config.Bind("General", "Fall Damage Invulnerability Seconds", 0.1f, "Amount of time invulnerable since fall damage. default is default OSP.");
            OOBIFrames = Config.Bind("General", "Out of Bounds Damage Invulnerability Seconds", 0.5f, "Amount of time invulnerable since tp back. default is commonly modded OSP.");
            CritFall = Config.Bind("General", "Critical Fall Chance", 0f, "The Cracked In Me Awakens...");

            On.RoR2.TeleportHelper.OnTeleport += (orig, obj, pos, vel) =>
            {
                orig(obj, pos, vel);
                if (vel.y <= 0) return;
                CharacterBody body = obj.GetComponent<CharacterBody>();
                if (!oob.Contains(body)) oob.Add(body);
            };
            IL.RoR2.GlobalEventManager.OnCharacterHitGroundServer += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchStloc(5));
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<float, CharacterBody, float>>((orig, self) =>
                {
                    orig *= FallMultiplier.Value;
                    if (oob.Contains(self)) orig *= OOBMultiplier.Value;
                    float hp = Mathf.Max(self.healthComponent.health - (orig * self.maxHealth / 60f), FallThreshold.Value * self.maxHealth);
                    if (oob.Contains(self)) hp = Mathf.Max(hp, OOBThreshold.Value * self.maxHealth);
                    return inverseHP(hp, self);

                    float inverseHP(float orig, CharacterBody self) { return (self.healthComponent.health - orig) * 60f / self.maxHealth; }
                });
                c.GotoNext(x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.TakeDamage)));
                c.EmitDelegate<Func<DamageInfo, DamageInfo>>(info =>
                {
                    bool crit = Run.instance.runRNG.RangeFloat(0, 1) < CritFall.Value;
                    if (FallIsLethal.Value || crit)
                    {
                        info.damageType &= ~DamageType.NonLethal;
                        info.damageType |= DamageType.BypassOneShotProtection;
                    }
                    if (crit) info.crit = true;
                    return info;
                });
            };
            On.RoR2.GlobalEventManager.OnCharacterHitGroundServer += (orig, self, body, vel) =>
            {
                orig(self, body, vel);
                body.healthComponent.ospTimer = oob.Contains(body) ? OOBIFrames.Value : FallIFrames.Value;
                oob.Remove(body);
            };
        }
    }
}
