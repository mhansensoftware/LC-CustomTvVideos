using BepInEx;
using BepInEx.Logging;
using CustomTvVideos.Patches;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;

namespace CustomTvVideos
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class CustomTvVideos : BaseUnityPlugin
    {
        private static string videosDir = Path.Combine(Paths.BepInExRootPath, MyPluginInfo.PLUGIN_NAME, "VideoClips");
#if DEBUG
        private static string debugDir = Path.Combine(Paths.BepInExRootPath, MyPluginInfo.PLUGIN_NAME, "Debug");
        private static string debug_logFile = Path.Combine(debugDir, "log.txt");
        private static StreamWriter debug_logWriter;
#endif
        private static ManualLogSource logger;

        private Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        [UsedImplicitly] // Make it so code analysis does not complain
        private void Awake()
        {
            try
            {
                logger = Logger;

                CreateDirectoryIfItDoesNotExist();
#if DEBUG
                SetupCustomLoggingLocation();
                DEBUG_TerminalPatch.Init(Logger);
                DEBUG_DebugUtils.Init(Logger);
#endif
                
                LoadClips();

                harmony.PatchAll(typeof(TvScriptPatch));
                Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Encountered an error in Awake()\n {ex}");
            }
        }

        private bool LoadClips()
        {
            VideoGetter videoGetter = new VideoGetter(Logger);
            FileInfo[] fileInfo;
            if (videoGetter.TryGetValue(out string[] videoFiles))
            {
                List<FileInfo> files = new List<FileInfo>();
                foreach (var videoFile in videoFiles)
                {
                    files.Add(new FileInfo(videoFile));
                }

                fileInfo = files.ToArray();
                Logger.LogInfo($"Got {videoFiles.Length} video files.");
            }
            else
            {
                Logger.LogFatal("Unable to get video clips.");
                gameObject.SetActive(false); // Disable
                return false;
            }

            if (fileInfo.Length == 0)
            {
                Logger.LogInfo("No video clips where loaded, Aborting...");
                gameObject.SetActive(false);  // Disable
                return false;
            }

            TvScriptPatch.Init(fileInfo, logger);

            return true;
        }


#if DEBUG

        private void SetupCustomLoggingLocation()
        {
            if (File.Exists(debug_logFile))
                File.Delete(debug_logFile);
            debug_logWriter = new StreamWriter(debug_logFile, true);
            Logger.LogEvent += Logger_LogEvent;
        }

        private void Logger_LogEvent(object sender, LogEventArgs e)
        {
            try
            {
                debug_logWriter.WriteLine(e.ToString());
                debug_logWriter.Flush();
            }
            catch
            { }
        }
#endif

        private void CreateDirectoryIfItDoesNotExist()
        {
#if DEBUG
            Directory.CreateDirectory(debugDir);
            Logger.LogInfo($"Debug Directory: {debugDir}");
#endif
            Directory.CreateDirectory(videosDir);
            Logger.LogInfo($"Video Directory: {videosDir}");
        }
    }
}