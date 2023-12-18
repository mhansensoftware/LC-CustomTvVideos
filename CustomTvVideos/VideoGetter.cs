using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CustomTvVideos
{

    internal class VideoGetter : ITryGet<string[]>
    {
        #region Defs
        [Flags]
        private enum PlatformFlag
        {
            Unknown = 0,
            Windows = 1,
            MacOS = 2,
            Linux = 4
        }

        private struct FileType
        {
            public string Extension;
            public PlatformFlag Platform;

            public FileType(string extension, PlatformFlag platform)
            {
                Extension = extension;
                Platform = platform;
            }
        }
        #endregion

        private ManualLogSource logger;

        private static string videosDir = Path.Combine(Paths.BepInExRootPath, MyPluginInfo.PLUGIN_NAME, "VideoClips");

        private static FileType[] videoFileTypes = new FileType[]
                       {
                           new FileType(".asf", PlatformFlag.Windows),
                           new FileType(".avi", PlatformFlag.Windows),
                           new FileType(".dv", PlatformFlag.Windows | PlatformFlag.MacOS),
                           new FileType(".mv4", PlatformFlag.Windows | PlatformFlag.MacOS),
                           new FileType(".mov", PlatformFlag.Windows | PlatformFlag.MacOS),
                           new FileType(".mp4", PlatformFlag.Windows | PlatformFlag.MacOS),
                           new FileType(".mpg", PlatformFlag.Windows | PlatformFlag.MacOS),
                           new FileType(".mpeg", PlatformFlag.Windows | PlatformFlag.MacOS),
                           new FileType(".ogv", PlatformFlag.Windows | PlatformFlag.MacOS | PlatformFlag.Linux),
                           new FileType(".vp8", PlatformFlag.Windows | PlatformFlag.MacOS | PlatformFlag.Linux),
                           new FileType(".webm", PlatformFlag.Windows | PlatformFlag.MacOS | PlatformFlag.Linux),
                           new FileType(".wmv", PlatformFlag.Windows)
                       };

        public VideoGetter(ManualLogSource logger)
        {
            this.logger = logger;
        }

        private static bool IsSupportedOnCurrentPlatform(string extension, PlatformFlag platform)
        {
            return videoFileTypes.Any(x => x.Extension == extension && x.Platform.HasFlag(platform));
        }

        private string[] GetSupportedVideoFiles(string dir, PlatformFlag platform)
        {
            List<string> supportedFiles = new List<string>();
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                FileInfo fileInfo = new FileInfo(file);
                if (!fileInfo.Exists)
                    continue;

                if (IsSupportedOnCurrentPlatform(fileInfo.Extension, platform))
                {
                    supportedFiles.Add(fileInfo.FullName);
                }
                else
                {
                    logger.LogWarning($"File: \"{fileInfo.Name}\" Is not supported.");
                }
            }

            return supportedFiles.ToArray();
        }

        public bool TryGetValue(out string[] value)
        {
            try
            {
                PlatformFlag platform = GetCurrentPlatform();
                string[] videoFiles = GetSupportedVideoFiles(videosDir, platform);

                value = videoFiles;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                value = null;
                return false;
            }
        }

        private static PlatformFlag GetCurrentPlatform()
        {
            RuntimePlatform unityPlatform = Application.platform;
            PlatformFlag platform = unityPlatform switch
            {
                RuntimePlatform.WindowsPlayer => PlatformFlag.Windows,
                RuntimePlatform.LinuxPlayer => PlatformFlag.Linux,
                RuntimePlatform.OSXPlayer => PlatformFlag.MacOS,
                _ => throw new PlatformNotSupportedException($"Platform: \"{unityPlatform}\" is not supported."),
            };

            return platform;
        }
    }
}
