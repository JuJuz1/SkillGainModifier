using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SkillType = Skills.SkillType;

namespace SkillGainModifier
{
    // A class to hold the current level and progress of the Skill
    class SkillData
    {
        public float level;
        public float progress;
        public override string ToString()
        {
            return $"level: {level}, progress: {progress}";
        }
    };

    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class SkillGainModifier : BaseUnityPlugin
    {
        public const string pluginGUID = "jujuz1.mods.skillgainmodifier";
        public const string pluginName = "SkillGainModifier";
        public const string pluginVersion = "0.1.0";

        private readonly Harmony harmonyInstance = new Harmony(pluginGUID);

        private readonly static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        // To hold skill progress
        private static Dictionary<SkillType, SkillData> skillData = new Dictionary<SkillType, SkillData>();

        private static string configFileName = pluginGUID + ".cfg";
        private static string configFileFullPath = BepInEx.Paths.ConfigPath + Path.DirectorySeparatorChar + configFileName;

        // Configs

        private static ConfigEntry<bool> loggingEnabled;

        private static ConfigEntry<float> corpseRunDuration;
        private static ConfigEntry<bool> noSkillDrainEnabled;

        private static readonly Dictionary<SkillType, ConfigEntry<float>> skillGainModifiers = new Dictionary<SkillType, ConfigEntry<float>>();
        // Having reduction modifiers for all skills in addition to gain modifiers kind of defeats the purpose am I right?
        private static ConfigEntry<float> skillReductionModifier;

        // Config reloading timers
        private DateTime lastReloadTime;
        private const long ONE_SECOND_IN_TICKS = 10_000_000;
        private const long RELOAD_DELAY = ONE_SECOND_IN_TICKS;

        // Warnings about reduction modifier
        private static DateTime lastReductionWarningTime;
        private const long WARNING_INTERVAL = ONE_SECOND_IN_TICKS * 10;

        public void Awake()
        {
            Config.SaveOnConfigSet = false; // Disable saving when binding each following config

            loggingEnabled = Config.Bind<bool>("General", "Logging Enabled", true, "Enable logging");

            noSkillDrainEnabled = Config.Bind<bool>("General", "No skill drain enabled", true, "Enable no skill drain. The default length for it is 10 minutes, and its too big of a hassle to modify to work correctly. So here is a feature to enable or disable it :)");
            corpseRunDuration = Config.Bind<float>("General", "Duration", 60.0f, "Corpse run duration. The default for it is 50 seconds, here we set a default of 60 seconds");

            skillGainModifiers[SkillType.All] = Config.Bind(
                "Skill Gain",
                "Global",
                2.5f,
                "Multiplier for all XP gain. A factor of 2.5x is a solid default value. I mean who in the hell is reaching level 100 in any playthrough with the default of 1x?"
            );

            foreach (SkillType skillType in System.Enum.GetValues(typeof(SkillType)))
            {
                if (skillType == SkillType.None || skillType == SkillType.All)
                {
                    continue;
                }

                skillGainModifiers[skillType] = Config.Bind(
                    "Skill Gain",
                    skillType.ToString(),
                    0.0f,
                    ""
                //"$"Multiplier for {skill} XP gain. Overrides all XP gain modifier if other than 0!"
                );
            }

            skillReductionModifier = Config.Bind<float>("Skill reduction", "Modifier", 0.0f, "A multiplier for skill reduction when dying. RECOMMENDED VALUES: 0-0.2, see later for explanation. The default value of 0 means the skill level AND progress is not affected at all. A value of 1 signals to use the game's default world modifiers. Any value other than 0 resets skill progress! The game does the following when calculating the new level: new level = modifier * current level. Using a value above one will always set the level 0!!! So be careful with too big modifiers. The code checks that the minimum level is capped to 0 for safety");

            Config.Save();
            Config.SaveOnConfigSet = true; // Re-enable saving on config changes

            SetupWatcher();

            WarnAboutReductionModifier(atStartup: true);

            Assembly assembly = Assembly.GetExecutingAssembly();
            harmonyInstance.PatchAll(assembly);
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        // Config hot reloading
        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new FileSystemWatcher(BepInEx.Paths.ConfigPath, configFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.Now;
            var time = now.Ticks - lastReloadTime.Ticks;

            if (!File.Exists(configFileFullPath))
            {
                LogError($"Config doesn't exist, check {configFileFullPath}");
                return;
            }

            if (time < RELOAD_DELAY)
            {
                LogDebug($"Attempting new reload in {(float)(RELOAD_DELAY - time) / (float)(RELOAD_DELAY)}s");
                return;
            }

            lastReloadTime = now;

            try
            {
                LogInfo("Attempting to reload configuration...");
                Config.Reload();
            }
            catch
            {
                LogError($"There was an issue loading {configFileName}");
            }

            LogInfo("Reloaded configuration!\n");
        }

        // Logging wrappers for the BepInEx logging system

        private static void LogInfo(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogInfo(message);
            }
        }

        // NOTE: By default, LogDebug is not captured, one has to enable it via BepInEx/config/BepInEx.cfg
        private static void LogDebug(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogDebug(message);
            }
        }

        private static void LogWarning(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogWarning(message);
            }
        }

        private static void LogError(string message)
        {
            if (loggingEnabled.Value)
            {
                logger.LogError(message);
            }
        }

        private static void WarnAboutReductionModifier(bool atStartup = false)
        {
            bool aboveOne = skillReductionModifier.Value > 1.0f;
            bool belowZero = skillReductionModifier.Value < 0.0f;

            if (aboveOne || belowZero)
            {
                if (atStartup)
                {
                    if (aboveOne)
                    {
                        LogWarning($"Any reduction modifier above one results in the level being 0 when dying! Current modifier: {skillReductionModifier.Value}. Recommended values are 0-0.2! The mod will warn you about this every {WARNING_INTERVAL / ONE_SECOND_IN_TICKS}s");
                    }
                    else if (belowZero)
                    {
                        LogWarning($"Any reduction modifier below zero results in the level increasing when dying! Current modifier: {skillReductionModifier.Value}. Recommended values are 0-0.2! The mod will warn you about this every {WARNING_INTERVAL / ONE_SECOND_IN_TICKS}s");
                    }

                    return;
                }

                var now = DateTime.Now;
                var time = now.Ticks - lastReductionWarningTime.Ticks;

                if (time < WARNING_INTERVAL)
                {
                    return;
                }

                lastReductionWarningTime = now;

                if (aboveOne)
                {
                    LogWarning($"Any reduction modifier above one results in the level being 0 when dying! Current modifier: {skillReductionModifier.Value}. Recommended values are 0-0.2!");
                }
                else if (belowZero)
                {
                    LogWarning($"Any reduction modifier below zero results in the level increasing when dying! Current modifier: {skillReductionModifier.Value}. Recommended values are 0-0.2!");
                }
            }
        }

        /// Patches ///

        /// Raising skills ///

        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        public static class Patch_Skills_RaiseSkill
        {
            private static void Prefix(SkillType skillType, ref float factor)
            {
                ConfigEntry<float> entry;
                bool found = skillGainModifiers.TryGetValue(skillType, out entry);
                if (!found)
                {
                    LogError($"Couldn't find value by key {skillType}. Raising skill by default (1.0f)");
                    return;
                }

                LogDebug($"{skillType} gain before: {factor}");
                // Default value of 0 means use global
                if (entry.Value == 0.0f)
                {
                    float globalFactor = skillGainModifiers[SkillType.All].Value;
                    LogDebug($"Using global value: {globalFactor}");
                    factor *= globalFactor;
                }
                else
                {
                    LogDebug($"Using overriden value: {entry.Value}");
                    if (entry.Value < 0.0f)
                    {
                        LogWarning($"{skillType} has a negative modifier of {entry.Value}. This will result in negative xp gain, which will not be shown in the UI beyond the progress bar. Are you sure this is correct? Applying negative xp gain...");
                    }

                    factor *= entry.Value;
                }

                LogDebug($"After: {factor}\n");
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

                    // Save all skill levels and progress here and apply in postfix
                    // to avoid losing any progress via m_accumulator = 0 being done

                    LogInfo($"Reduction modifier is 0. Saving skill progress...\n");
                    foreach (var kv in __instance.m_skillData)
                    {
                        var data = new SkillData { level = kv.Value.m_level, progress = kv.Value.m_accumulator };
                        skillData.Add(kv.Key, data);
                        LogDebug($"Added {kv.Key}, {data}");
                    }

                    return;
                }

                if (skillReductionModifier.Value < 0.0f)
                {
                    LogWarning($"Any reduction modifier below zero results in the level increasing when dying! Current modifier: {skillReductionModifier.Value}. Recommended values are 0-0.2! Applying positive xp gain...");
                }

                factor = skillReductionModifier.Value;
                LogDebug($"Factor after modifying: {factor}\n");
            }

            // Modify progress back to saved if skillReductionModifier is 0
            private static void Postfix(Skills __instance)
            {
                LogDebug("Skill progress before applying saved:");

                bool gaveWarningAboutModifierBeingAboveOne = false;

                foreach (var kv in __instance.m_skillData)
                {
                    float oldLevel = kv.Value.m_level;

                    string correctedLevel = "";
                    // Fix negative levels if used too big of a modifier
                    if (kv.Value.m_level < 0)
                    {
                        if (!gaveWarningAboutModifierBeingAboveOne)
                        {
                            WarnAboutReductionModifier();
                            gaveWarningAboutModifierBeingAboveOne = true;
                        }

                        LogWarning($"Skill {kv.Key} would have had a level of {oldLevel} if it wasn't corrected to 0!");
                        kv.Value.m_level = 0;
                        correctedLevel = $"Corrected level: {kv.Value.m_level}";
                    }

                    LogDebug($"{kv.Key}: level: {oldLevel} progress: {kv.Value.m_accumulator}. {correctedLevel}");
                }

                // If any data was saved
                if (skillData.Count > 0)
                {
                    LogInfo($"Applying saved skill progress...\n");
                    foreach (var kv in __instance.m_skillData)
                    {
                        var data = new SkillData { level = 0.0f, progress = 0.0f }; // TryGetValue would return zero-initialized nonetheless, but to be extra safe
                        bool foundValueBySkill = skillData.TryGetValue(kv.Key, out data);
                        if (!foundValueBySkill)
                        {
                            LogError($"Couldn't find value by key {kv.Key}. Setting value to default (0.0f)");
                        }
                        else
                        {
                            LogDebug($"Found {kv.Key}, setting data to {data}");
                        }

                        __instance.m_skillData[kv.Key].m_level = data.level;
                        __instance.m_skillData[kv.Key].m_accumulator = data.progress;
                    }

                    skillData.Clear();
                }
            }
        }

        // Can be used to print something continouosly for debugging
        //[HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        //public static class Patch_Player_UseStamina
        //{
        //    private static void Prefix(Player __instance)
        //    {
        //        // LogDebug...
        //    }
        //}

        /// No skill drain ///

        // No skill drain status effect was too much of a hassle to get working
        // The system is really not made for easy modifying at runtime

        // TLDR: Look at StatusEffect and SEMan
        // cloning StatusEffects and a too complex system for applying effects and removing them
        // Also the player has m_DeathCooldown but effects have m_ttl, spaghetti.. code...
        // Also I realized one can modify the difficulty of the game via the world modifiers

        // Decided to just add a bool to enable or disable no skill drain

        // This is run every FixedUpdate so we can call this here quite handily
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateStats), typeof(float))]
        public static class Patch_Player_UpdateStats
        {
            private static void Postfix(Player __instance)
            {
                WarnAboutReductionModifier();

                if (!noSkillDrainEnabled.Value && __instance.m_timeSinceDeath <= 10000.0f) // Safe margin
                {
                    // Normally: m_timeSinceDeath += dt;
                    // Here we just make HardDeath() return true instantly
                    LogDebug("No skill drain disabled, timeSinceDeath set to 999999.0f");
                    __instance.m_timeSinceDeath = 999999.0f; // Same as the default in the game
                    // This doesn't remove the icon and the timer from the UI though... but works functionally!
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
                LogDebug($"Set {se} ttl (duration) to: {se.m_ttl}");
            }
        }
    }
}
