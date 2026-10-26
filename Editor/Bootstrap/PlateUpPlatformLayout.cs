using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    /// <summary>Paths inside one platform's PlateUp installation, independent of Steam metadata.</summary>
    internal sealed class PlateUpPlatformLayout
    {
        private PlateUpPlatformLayout(string gameRoot, string dataRoot, string executablePath)
        {
            GameRoot = gameRoot;
            DataRoot = dataRoot;
            ManagedDirectory = Path.Combine(dataRoot, "Managed");
            ExecutablePath = executablePath;
        }

        public string GameRoot { get; }
        public string DataRoot { get; }
        public string ManagedDirectory { get; }
        public string ExecutablePath { get; }

        // Compile-time editor platform also works on background asset-reader threads.
        public static RuntimePlatform CurrentPlatform
        {
            get
            {
#if UNITY_EDITOR_OSX
                return RuntimePlatform.OSXEditor;
#elif UNITY_EDITOR_LINUX
                return RuntimePlatform.LinuxEditor;
#else
                return RuntimePlatform.WindowsEditor;
#endif
            }
        }

        public static PlateUpPlatformLayout ResolveLayout(string installRoot) => ResolveLayout(installRoot, CurrentPlatform);

        public static PlateUpPlatformLayout ResolveLayout(string installRoot, RuntimePlatform platform)
        {
            string root = Path.GetFullPath(installRoot ?? string.Empty);
            if (IsMac(platform) && string.Equals(new DirectoryInfo(root).Name, "PlateUp.app", StringComparison.OrdinalIgnoreCase))
                root = Directory.GetParent(root)?.FullName ?? root;

            PlateUpPlatformLayout direct = Create(root, platform);
            if (File.Exists(direct.ExecutablePath)) return direct;

            // Steam has shipped a second PlateUp directory beneath the manifest's installdir.
            string name = new DirectoryInfo(root).Name;
            if (!string.IsNullOrEmpty(name))
            {
                PlateUpPlatformLayout nested = Create(Path.Combine(root, name), platform);
                if (File.Exists(nested.ExecutablePath)) return nested;
            }
            return direct;
        }

        internal static bool IsMac(RuntimePlatform platform) => platform == RuntimePlatform.OSXEditor || platform == RuntimePlatform.OSXPlayer;
        internal static bool IsWindows(RuntimePlatform platform) => platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.WindowsPlayer;

        private static PlateUpPlatformLayout Create(string root, RuntimePlatform platform)
        {
            if (IsMac(platform))
            {
                string contents = Path.Combine(root, "PlateUp.app", "Contents");
                return new PlateUpPlatformLayout(root, Path.Combine(contents, "Resources", "Data"),
                    Path.Combine(contents, "MacOS", "PlateUp"));
            }
            if (IsWindows(platform))
                return new PlateUpPlatformLayout(root, Path.Combine(root, "PlateUp_Data"), Path.Combine(root, "PlateUp.exe"));
            if (platform != RuntimePlatform.LinuxEditor && platform != RuntimePlatform.LinuxPlayer)
                throw new PlatformNotSupportedException("PlateUp Steam discovery supports Windows, macOS, and Linux.");

            string executable = new[] { "PlateUp.x86_64", "PlateUp.x86", "PlateUp" }
                .Select(name => Path.Combine(root, name)).FirstOrDefault(File.Exists) ?? Path.Combine(root, "PlateUp.x86_64");
            return new PlateUpPlatformLayout(root, Path.Combine(root, "PlateUp_Data"), executable);
        }
    }
}
