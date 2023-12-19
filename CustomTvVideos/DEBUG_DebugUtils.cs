#if DEBUG
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CustomTvVideos
{
    internal static class DEBUG_DebugUtils
    {
        private static ManualLogSource logger;

        public static void Init(ManualLogSource logSource)
        {
            logger = logSource;
        }

        public static void LogStackTrace(StackTrace DEBUG_Trace)
        {
            logger.LogDebug(DEBUG_Trace);
        }
    }
}
#endif