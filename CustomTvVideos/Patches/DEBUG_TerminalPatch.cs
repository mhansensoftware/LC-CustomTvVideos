#if DEBUG
using BepInEx.Logging;
using HarmonyLib;

namespace CustomTvVideos.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    internal class DEBUG_TerminalPatch
    {
        private static ManualLogSource logger;

        internal static void Init(ManualLogSource logSource)
        {
            logger = logSource;
        }

        [HarmonyPatch(typeof(Terminal), "Start")]
        [HarmonyPostfix]
        private static void Terminal_Start(Terminal __instance)
        {
            __instance.groupCredits = 1000;
            logger.LogInfo("Terminal_Start");
        }
    }
}
#endif