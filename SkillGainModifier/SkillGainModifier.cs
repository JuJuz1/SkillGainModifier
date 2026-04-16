using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace MyBepInExPlugin
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        const string pluginGUID = "com.example.GUID";
        const string pluginName = "MyPlugin";
        const string pluginVersion = "1.0.0";

        private readonly Harmony HarmonyInstance = new Harmony(pluginGUID);

        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

        public void Awake()
        {
            Main.logger.LogInfo("Thank you for using my mod!");

            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        public static class Patch_Player_UseStamina
        {
            private static bool Prefix()
            {
                return false;
            }
        }
    }
}
