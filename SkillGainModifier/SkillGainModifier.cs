using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using SkillType = Skills.SkillType;

namespace SkillGainModifier
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class SkillGainModifier : BaseUnityPlugin
    {
        public const string pluginGUID = "jujuz1.mods.skillgainmodifier";
        public const string pluginName = "SkillGainModifier";
        public const string pluginVersion = "1.0.0";

        private readonly Harmony harmonyInstance = new Harmony(pluginGUID);

        private readonly static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        // To hold skill progress
        private static Dictionary<SkillType, float> skillData = new Dictionary<SkillType, float>();

        // Configs

        private static ConfigEntry<bool> loggingEnabled;

        private static ConfigEntry<bool> noSkillDrainEnabled;

        public void Awake()
        {
            loggingEnabled = Config.Bind<bool>("Logging", "Logging Enabled", true, "Enable logging");

            noSkillDrainEnabled = Config.Bind<bool>("No skill drain", "No skill drain enabled", true, "Enable no skill drain. The default length for it is 10 minutes, and its too big of a hassle to modify to work correctly. So here is a feature to enable or disable it :)");

            // Other user supplied values

            Assembly assembly = Assembly.GetExecutingAssembly();
            harmonyInstance.PatchAll(assembly);
        }

        private static void LogInfo(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogInfo(message);
            }
        }

        // Logging wrappers for the BepInEx logging system
        // NOTE: LogDebug is by default not captured, one has to enable it via BepInEx/config/BepInEx.cfg
        private static void LogDebug(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogDebug(message);
            }
        }

        private static void LogError(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogDebug(message);
            }
        }

        /// Patches ///

        /// Raising skills ///

        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        public static class Patch_Skills_RaiseSkill
        {
            private static void Prefix(SkillType skillType, ref float factor)
            {
                // Per-skill modifiers

                LogDebug($"{skillType} gain before: {factor}");
                // User supplied value
                factor *= 2.5f; // 2.5 times increase
                LogDebug($"After: {factor}");
            }

            private static void Postfix()
            {
                // Print the skills to debug??
                // Or the skill raised, its value before and after
                // Also how much it should have been normally raised vs modified?
                //LogDebug($"RaiseSkill Postfix");
            }
        }

        // Can be used to print something continouosly for debugging
        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        public static class Patch_Player_UseStamina
        {
            private static void Prefix(Player __instance)
            {
                // LogDebug...
            }
        }

        // No skill drain status effect was too much of a hassle to get working
        // The system is really not made for easy modifying at runtime

        // TLDR: Look at StatusEffect and SEMan
        // cloning StatusEffects and a too complex system for applying effects and removing them
        // Also the player has m_DeathCooldown but effects have m_ttl, spaghetti.. code...
        // Also I realized one can modify the difficulty of the game via the world modifiers

        // Decided to just add a bool to enable or disable no skill drain
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateStats), typeof(float))]
        public static class Patch_Player_UpdateStats
        {
            private static void Postfix(Player __instance)
            {
                if (!noSkillDrainEnabled.Value)
                {
                    // Normally: m_timeSinceDeath += dt;
                    // Here we just HardDeath() to return true instantly
                    __instance.m_timeSinceDeath = 999999f; // Same as the default in the game
                }
            }
        }

        /// On death skill reduction ///

        [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
        public static class Patch_Skills_LowerAllSkills
        {
            private static void Prefix(Skills __instance, ref float factor)
            {
                LogDebug($"Game m_skillReductionRate: {Game.m_skillReductionRate}");
                LogDebug($"Skills m_skillReductionRate: {__instance.m_DeathLowerFactor}\n");
                LogDebug($"Factor before modifying: {factor}");
                // By setting the value explicitly here we allow full control of the skill drain rate
                factor = 0.0f;
                LogDebug($"Factor after modifying: {factor}\n");

                // Save all skill progress in here and apply in postfix
                // to avoid losing any progress via m_accumulator = 0
                // being done in LowerAllSkills

                LogInfo($"Saving skill progress...\n");
                foreach (var kv in __instance.m_skillData)
                {
                    skillData.Add(kv.Key, kv.Value.m_accumulator);
                    LogDebug($"Added {kv.Key}, {kv.Value.m_accumulator}");
                }
            }

            // Modify progress back to saved
            private static void Postfix(Skills __instance)
            {
                LogDebug("Skill progress before applying saved:\n");
                foreach (var kv in __instance.m_skillData)
                {
                    LogDebug($"{kv.Key}: {kv.Value.m_accumulator}");
                }

                if (skillData.Count > 0)
                {
                    LogInfo($"Applying saved skill progress...\n");
                    foreach (var kv in __instance.m_skillData)
                    {
                        float accumulated = 0.0f; // TryGetValue would return this nonetheless, but to be extra safe
                        bool foundValueBySkill = skillData.TryGetValue(kv.Key, out accumulated);
                        if (!foundValueBySkill)
                        {
                            LogError($"Couldn't find value by key {kv.Key}. Setting value to default (0.0f)");
                        }
                        else
                        {
                            LogDebug($"Found {kv.Key}, setting value to {accumulated}");
                        }

                        __instance.m_skillData[kv.Key].m_accumulator = accumulated;
                    }

                    // Has to be called in order to add values in Prefix
                    skillData.Clear();
                }
            }
        }

        /// Corpse run ///

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Awake))]
        public static class Patch_TombStone_Awake
        {
            private static void Postfix(TombStone __instance)
            {
                StatusEffect se = __instance.m_lootStatusEffect;
                LogDebug($"Effect: {se}"); // Effect: CorpseRun (SE_Stats)
                LogDebug($"ttl: {se.m_ttl}"); // ttl: 50 (default)

                se.m_ttl = 5.0f; // User supplied value
                LogDebug($"Set {se} ttl to: {se.m_ttl}");
            }
        }
    }
}
