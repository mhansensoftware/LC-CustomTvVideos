using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Video;

namespace CustomTvVideos.Patches
{
    [HarmonyPatch(typeof(TVScript))]
    internal class TvScriptPatch
    {
        private static ManualLogSource logger;
        private static FileInfo[] videoFiles;

        private static int currentClip;
        private static bool setupDone;
        private static bool wasTvOnLastFrame;
        private static float currentClipTime; // Will be used for pause function

        internal static void Init(FileInfo[] videos, ManualLogSource logSource)
        {
            videoFiles = videos;
            logger = logSource;
        }

        private static void IncrementCurrentClip()
        {
            if (currentClip + 1 >= videoFiles.Length)
            {
                currentClip = 0; // Wrap around.
                return;
            }

            currentClip++;
        }

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

        [HarmonyPatch(typeof(TVScript), "TVFinishedClip")]
        [HarmonyPrefix]
        private static bool TV_TVFinishedClip(TVScript __instance, VideoPlayer source)
        {
            logger.LogInfo("TV_TVFinishedClip");

            if (__instance.tvOn && !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                IncrementCurrentClip();
                FileInfo videoFile = videoFiles[currentClip];

                logger.LogInfo($"Playing {videoFile.FullName}");

                SetVideo(__instance, videoFile);
            }

            return false;
        }

        [HarmonyPatch(typeof(TVScript), "TurnTVOnOff")]
        [HarmonyPrefix]
        private static bool TV_TurnTVOnOff(TVScript __instance, bool on)
        {
            logger.LogInfo("TV_TurnTVOnOff");

#if DEBUG
            // Expensive
            System.Diagnostics.StackTrace DEBUG_trace = new System.Diagnostics.StackTrace();
            DEBUG_DebugUtils.LogStackTrace(DEBUG_trace);
#endif

            __instance.tvOn = on;
            if (!setupDone)
            {
                __instance.video.clip = null;
                __instance.tvSFX.clip = null;

                IncrementCurrentClip();
                FileInfo videoFile = videoFiles[currentClip];

                SetVideo(__instance, videoFile);
                setupDone = true;
            }

            if (on)
            {
                IncrementCurrentClip();
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

        [HarmonyPatch(typeof(TVScript), "Update")]
        [HarmonyPrefix]
        private static bool TV_Update(TVScript __instance)
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
            }

            return false;
        }

        private static void SetTVScreenMaterial(TVScript instance, bool b)
        {
            MethodInfo method = ((object)instance).GetType().GetMethod("SetTVScreenMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(instance, new object[1] { b });
        }
    }
}
