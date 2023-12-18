using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace CustomTvVideos
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [HarmonyPatch(typeof(TVScript))]
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
        private static FileInfo[] videoFiles;

        private void Awake()
        {
            logger = Logger;
#if DEBUG
            if (File.Exists(debug_logFile))
                File.Delete(debug_logFile);
            debug_logWriter = new StreamWriter(debug_logFile, true);
            Logger.LogEvent += Logger_LogEvent;
#endif
            CreateDirectoryIfItDoesNotExist();
            VideoGetter videoGetter = new VideoGetter(Logger);
            if (videoGetter.TryGetValue(out string[] videoFiles))
            {
                List<FileInfo> fileClipTupleList = new List<FileInfo>();
                foreach (var videoFile in videoFiles)
                {
                    fileClipTupleList.Add(new FileInfo(videoFile));
                }

                CustomTvVideos.videoFiles = fileClipTupleList.ToArray();
                Logger.LogInfo($"Got {videoFiles.Length} video files.");
            }
            else
            {
                Logger.LogFatal("Unable to get video clips.");
                gameObject.SetActive(false); // Disable
                return;
            }

            if (CustomTvVideos.videoFiles.Length == 0)
            {
                Logger.LogInfo("No video clips where loaded, Aborting...");
                gameObject.SetActive(false);  // Disable
                return;
            }

            harmony.PatchAll(typeof(CustomTvVideos));
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }


#if DEBUG
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

        private static int currentClip = 0;

        private static void SetVideo(TVScript tv, FileInfo videoFile)
        {
            tv.video.clip = null;
            tv.tvSFX.clip = null;

            tv.video.url = $"file://{videoFile.FullName}";
            tv.video.source = (VideoSource)1;
            tv.video.controlledAudioTrackCount = 1;
            tv.video.audioOutputMode = (VideoAudioOutputMode)1;
            tv.video.SetTargetAudioSource((ushort)0, tv.tvSFX);
            tv.video.Prepare();
            tv.video.Stop();
            tv.tvSFX.Stop();
        }

#if DEBUG
        [HarmonyPatch(typeof(Terminal), "Start")]
        [HarmonyPostfix]
        public static void Terminal_Start(Terminal __instance)
        {
            __instance.groupCredits = 1000;
            logger.LogInfo("Terminal_Start");
        }
#endif

        [HarmonyPatch(typeof(TVScript), "TVFinishedClip")]
        [HarmonyPrefix]
        public static bool TV_TVFinishedClip(TVScript __instance, VideoPlayer source)
        {
            logger.LogInfo("TV_TVFinishedClip");

            if (__instance.tvOn && !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                currentClip = (currentClip + 1) % videoFiles.Length;
                FileInfo videoFile = videoFiles[currentClip];

                logger.LogInfo($"Playing {videoFile.FullName}");

                SetVideo(__instance, videoFile);
            }

            return false;
        }

        [HarmonyPatch(typeof(TVScript), "TurnTVOnOff")]
        [HarmonyPrefix]
        public static bool TV_TurnTVOnOff(TVScript __instance, bool on)
        {
            logger.LogInfo("TV_TurnTVOnOff");

            __instance.tvOn = on;
            if ((int)__instance.video.source != 1 || __instance.video.url == "")
            {
                __instance.video.clip = null;
                __instance.tvSFX.clip = null;

                currentClip = (currentClip + 1) % videoFiles.Length;
                FileInfo videoFile = videoFiles[currentClip];

                SetVideo(__instance, videoFile);
            }

            if (on)
            {
                FileInfo videoFile = videoFiles[currentClip];
                SetVideo(__instance, videoFile);
                SetTVScreenMaterial(__instance, true);
                __instance.video.Play();
                __instance.tvSFX.Play();
                __instance.tvSFX.PlayOneShot(__instance.switchTVOn);
                WalkieTalkie.TransmitOneShotAudio(__instance.tvSFX, __instance.switchTVOn, 1f);
            }
            else
            {
                SetTVScreenMaterial(__instance, false);
                __instance.tvSFX.Stop();
                __instance.tvSFX.PlayOneShot(__instance.switchTVOff);
                __instance.video.Stop();
                WalkieTalkie.TransmitOneShotAudio(__instance.tvSFX, __instance.switchTVOff, 1f);
            }

            return false;
        }

        private static bool wasTvOnLastFrame;
        private static float currentClipTime;
        private static float timeSinceTurningOffTV;

        [HarmonyPatch(typeof(TVScript), "Update")]
        [HarmonyPrefix]
        public static bool TV_Update(TVScript __instance)
        {
            if (NetworkManager.Singleton.ShutdownInProgress || GameNetworkManager.Instance.localPlayerController == null)
            {
                return false;
            }

            if (!__instance.tvOn || GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                if (wasTvOnLastFrame)
                {
                    wasTvOnLastFrame = false;
                    SetTVScreenMaterial(__instance, false);
                    currentClipTime = (float)__instance.video.time;
                    __instance.video.Stop();
                }

                if (__instance.IsServer && !__instance.tvOn)
                {
                    timeSinceTurningOffTV += Time.deltaTime;
                }

                currentClipTime += Time.deltaTime;
                if ((double)currentClipTime > __instance.video.length)
                {
                    currentClip = (currentClip + 1) % videoFiles.Length;
                    FileInfo videoFile = videoFiles[currentClip];
                    SetVideo(__instance, videoFile);
                }
            }

            return false;
        }

        public static void SetTVScreenMaterial(TVScript instance, bool b)
        {
            MethodInfo method = ((object)instance).GetType().GetMethod("SetTVScreenMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(instance, new object[1] { b });
        }
    }
}