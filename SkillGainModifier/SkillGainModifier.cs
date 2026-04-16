using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace MyBepInExPlugin
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class SkillGainModifier : BaseUnityPlugin
    {
        public const string pluginGUID = "jujuz1.mods.skillgainmodifier";
        public const string pluginName = "SkillGainModifier";
        public const string pluginVersion = "1.0.0";

        private readonly Harmony harmonyInstance = new Harmony(pluginGUID);

        private readonly static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmonyInstance.PatchAll(assembly);
        }

        /// Raising skills ///

        // In player class
        //public override void RaiseSkill(Skills.SkillType skill, float value = 1f)
        //{
        //    if (skill != Skills.SkillType.None)
        //    {
        //        float multiplier = 1f;
        //        m_seman.ModifyRaiseSkill(skill, ref multiplier);
        //        value *= multiplier;
        //        m_skills.RaiseSkill(skill, value);
        //    }
        //}

        //public void RaiseSkill(SkillType skillType, float factor = 1f)
        //{
        //    if (skillType == SkillType.None)
        //    {
        //        return;
        //    }

        //    Skill skill = GetSkill(skillType);
        //    float level = skill.m_level;
        //    if (skill.Raise(factor))
        //    {
        //        if (m_useSkillCap)
        //        {
        //            RebalanceSkills(skillType);
        //        }

        //        m_player.OnSkillLevelup(skillType, skill.m_level);
        //        MessageHud.MessageType type = (((int)level != 0) ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center);
        //        m_player.Message(type, "$msg_skillup $skill_" + skill.m_info.m_skill.ToString().ToLower() + ": " + (int)skill.m_level, 0, skill.m_info.m_icon);
        //        Gogan.LogEvent("Game", "Levelup", skillType.ToString(), (int)skill.m_level);
        //    }
        //}

        /// What gets called ///

        // In Skills class
        //public bool Raise(float factor)
        //{
        //    if (m_level >= 100f)
        //    {
        //        return false;
        //    }

        //    float num = m_info.m_increseStep * factor * Game.m_skillGainRate;
        //    m_accumulator += num;
        //    float nextLevelRequirement = GetNextLevelRequirement();
        //    if (m_accumulator >= nextLevelRequirement)
        //    {
        //        m_level += 1f;
        //        m_level = Mathf.Clamp(m_level, 0f, 100f);
        //        m_accumulator = 0f;
        //        return true;
        //    }

        //    return false;
        //}

        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        public static class Patch_Player_RaiseSkill
        {
            private static void Prefix(Skills.SkillType skill, ref float value)
            {
                // Figure out a way to get current skill levels

                logger.LogDebug($"Skill: {skill}");
                float oldValue = value;
                logger.LogDebug($"Value before: {oldValue}");

                // User supplied value
                value *= 2.5f; // 2.5 times increase
                logger.LogDebug($"Value after: {value}");
            }

            private static void Postfix()
            {
                // Print the skills to debug
                // Or the skill raised, its value before and after
                // Also how much it should have been normally raised vs modified
                //logger.LogDebug($"RaiseSkill Postfix");
            }
        }

        /// On death skill reduction ///

        // class Skills
        // m_skills.OnDeath

        //public void OnDeath()
        //{
        //    LowerAllSkills(m_DeathLowerFactor * Game.m_skillReductionRate);
        //}

        // Default values for: m_DeathLowerFactor * Game.m_skillReductionRate
        // m_DeathLowerFactor = 0.25f, Game.m_skillReductionRate = 1.0f
        // so factor is 1.25f

        //public void LowerAllSkills(float factor)
        //{
        //    foreach (KeyValuePair<SkillType, Skill> skillDatum in m_skillData)
        //    {
        //        float num = skillDatum.Value.m_level * factor;
        //        skillDatum.Value.m_level -= num;
        //        skillDatum.Value.m_accumulator = 0f;
        //    }

        //    m_player.Message(MessageHud.MessageType.TopLeft, "$msg_skills_lowered");
        //}

        [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
        public static class Patch_Skills_LowerAllSkills
        {
            private static void Prefix(Skills __instance, ref float factor)
            {
                // Get the skill data before modifying

                logger.LogDebug($"Game m_skillReductionRate: {Game.m_skillReductionRate}");
                logger.LogDebug($"Skills m_skillReductionRate: {__instance.m_DeathLowerFactor}");
                logger.LogDebug($"Factor before modifying: {factor}");
                // By setting the value explicitly here we allow full control of the skill drain rate
                factor = 0.0f;
                logger.LogDebug($"Factor after modifying: {factor}");

                // No skill level lost, but progess is

                //var skillsData = __instance.m_skillData;

                // Save all skill progress in here and apply in postfix
                // to avoid losing any progress via m_accumulator = 0
            }
        }

        // Postfix modify progress back to normal!



        // Modifying no skill drain and corpse run

        //[HarmonyPatch(typeof(TombStone), nameof(TombStone.GiveBoost))]
        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Awake))]
        public static class Patch_TombStone_Awake
        {
            private static void Postfix(TombStone __instance)
            {
                StatusEffect se = __instance.m_lootStatusEffect;
                logger.LogDebug($"Effect: {se}"); // Effect: CorpseRun (SE_Stats)
                logger.LogDebug($"ttl: {se.m_ttl}"); // ttl: 50 (default)

                se.m_ttl = 5.0f; // User supplied value
                logger.LogDebug($"Set {se} ttl to: {se.m_ttl}");
            }
        }
    }
}
