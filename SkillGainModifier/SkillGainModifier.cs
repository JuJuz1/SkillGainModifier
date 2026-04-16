using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

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

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        public static class Patch_Player_UseStamina
        {
            private static bool Prefix()
            {
                //logger.LogDebug("Prefixed Player.UseStamina");
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        public static class Patch_Player_RaiseSkill
        {
            private static void Prefix(Skills.SkillType skill, ref float value)
            {
                // Figure out a way to get current skill levels
                //AccessTools.Method(typeof(Player), )
                logger.LogDebug($"Skill: {skill}");
                logger.LogDebug($"Value before: {value}");
                value *= 1.5f; // 50% increase
                logger.LogDebug($"Value after: {value}");
            }

            private static void Postfix()
            {
                // Print the skills to debug
                // Or the skill raised, its value before and after
                // Also how much it should have been normally raised vs modified
                logger.LogDebug($"RaiseSkill Postfix");
            }
        }

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
    }
}
