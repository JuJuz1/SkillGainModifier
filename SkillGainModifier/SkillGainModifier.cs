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

        private static ConfigEntry<float> corpseRunDuration;

        private static ConfigEntry<bool> loggingEnabled;

        private static ConfigEntry<bool> noSkillDrainEnabled;

        private static Dictionary<SkillType, ConfigEntry<float>> skillGainModifiers = new Dictionary<SkillType, ConfigEntry<float>>();
        private static ConfigEntry<float> skillReductionModifier; // Having reduction modifiers for all skills in addition to gain modifiers kind of defeats the purpose am I right?

        public void Awake()
        {
            loggingEnabled = Config.Bind<bool>("Logging", "Logging Enabled", true, "Enable logging");

            noSkillDrainEnabled = Config.Bind<bool>("No skill drain", "Enabled", true, "Enable no skill drain. The default length for it is 10 minutes, and its too big of a hassle to modify to work correctly. So here is a feature to enable or disable it :)");
            corpseRunDuration = Config.Bind<float>("Corpse run duration", "Duration", 60.0f, "Corpse run duration. The default for it is 50 seconds, here we set a default of 60 seconds");

            var all = SkillType.All;
            skillGainModifiers[all] = Config.Bind(
                "Skill Gain",
                all.ToString(),
                2.5f,
                "Multiplier for all XP gain. A factor of 2.5x is a solid default value. I mean who in the hell is reaching level 100 in any playthrough with the default of 1x?"
            );

            foreach (SkillType skill in System.Enum.GetValues(typeof(SkillType)))
            {
                if (skill == SkillType.None || skill == SkillType.All)
                {
                    continue;
                }

                skillGainModifiers[skill] = Config.Bind(
                    "Skill Gain",
                    skill.ToString(),
                    0.0f,
                    $"Multiplier for {skill} XP gain. Overrides all XP gain modifier if other than 0!"
                );
            }

            skillReductionModifier = Config.Bind<float>("Skill reduction", "Modifier", 0.0f, "A multiplier for skill reduction. The default value of 0 means the skill level AND progress is not affected at all. Any value other than 0 resets skill progress! A value of 1 signals to use the game's default world modifiers");

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
                ConfigEntry<float> entry;
                // TODO: allow non-exisiting values via other file type like yml
                bool found = skillGainModifiers.TryGetValue(skillType, out entry);
                if (!found)
                {
                    LogError($"Couldn't find value by key {skillType}. Raising skill by default (1.0f)");
                    return;
                }

                LogDebug($"{skillType} gain before: {factor}");
                if (entry.Value == 0.0f)
                {
                    LogDebug($"Using global value!");
                    factor *= skillGainModifiers[SkillType.All].Value;
                }
                else
                {
                    LogDebug($"Using overriden value!");
                    factor *= entry.Value;
                }

                LogDebug($"After: {factor}\n");
            }

            private static void Postfix()
            {
                // Print the skills to debug??
                // Or the skill raised, its value before and after
                // Also how much it should have been normally raised vs modified?
                //LogDebug($"RaiseSkill Postfix");
            }
        }

        /// On death skill reduction ///

        [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
        public static class Patch_Skills_LowerAllSkills
        {
            private static void Prefix(Skills __instance, ref float factor)
            {
                // The game essentially just multiplies the current level and removes that amount from the current level
                // Does some rebalancing after that

                LogDebug($"Game m_skillReductionRate: {Game.m_skillReductionRate}");
                LogDebug($"Skills m_skillReductionRate: {__instance.m_DeathLowerFactor}\n");
                // default: Game.m_skillReductionRate * __instance.m_DeathLowerFactor
                if (skillReductionModifier.Value == 1.0f)
                {
                    LogDebug("Default skill reduction!");
                    return;
                }

                LogDebug($"Factor before modifying: {factor}");

                if (skillReductionModifier.Value == 0.0f)
                {
                    LogDebug("No skill reduction!");

                    // Save all skill progress in here and apply in postfix
                    // to avoid losing any progress via m_accumulator = 0
                    // being done in LowerAllSkills

                    LogInfo($"Saving skill progress...\n");
                    foreach (var kv in __instance.m_skillData)
                    {
                        skillData.Add(kv.Key, kv.Value.m_accumulator);
                        LogDebug($"Added {kv.Key}, {kv.Value.m_accumulator}");
                    }

                    return;
                }

                factor = skillReductionModifier.Value;
                LogDebug($"Factor after modifying: {factor}\n");
            }

            // Modify progress back to saved if skillReductionModifier is 0
            private static void Postfix(Skills __instance)
            {
                LogDebug("Skill progress before applying saved:\n");
                foreach (var kv in __instance.m_skillData)
                {
                    LogDebug($"{kv.Key}: {kv.Value.m_accumulator}");
                }

                // If any data was saved
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

                    skillData.Clear();
                }
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

        /// Corpse run ///

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Awake))]
        public static class Patch_TombStone_Awake
        {
            private static void Postfix(TombStone __instance)
            {
                StatusEffect se = __instance.m_lootStatusEffect;
                se.m_ttl = corpseRunDuration.Value;
                LogDebug($"Set {se} ttl to: {se.m_ttl}");
            }
        }
    }
}
