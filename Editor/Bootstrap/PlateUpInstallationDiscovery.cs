using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal sealed class PlateUpInstallationDiscovery
    {
        public const string AppId = "1599600";
        private readonly RuntimePlatform _platform;
        private StringComparer PathComparer => PlateUpPlatformLayout.IsWindows(_platform) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public PlateUpInstallationDiscovery() : this(PlateUpPlatformLayout.CurrentPlatform) { }
        public PlateUpInstallationDiscovery(RuntimePlatform platform) { _platform = platform; }

        public PlateUpDiscoveryResult Discover() => Discover(DefaultSteamCandidates(_platform,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

        public PlateUpDiscoveryResult Discover(IEnumerable<string> steamCandidates)
        {
            var diagnostics = new List<string>();
            var installations = new List<PlateUpInstallation>();
            var steamRoots = NormalizeDistinct(steamCandidates);
            if (steamRoots.Count == 0) diagnostics.Add("Steam was not found; select the PlateUp folder manually.");

            foreach (string steamRoot in steamRoots)
            {
                var libraries = new List<string> { steamRoot };
                string[] libraryFiles = new[] { "steamapps", "config" }
                    .Select(folder => Path.Combine(steamRoot, folder, "libraryfolders.vdf")).Where(File.Exists).ToArray();
                foreach (string libraryFile in libraryFiles)
                {
                    try { libraries.AddRange(ReadLibraryFolders(libraryFile)); }
                    catch (Exception exception) when (IsReadError(exception))
                    {
                        diagnostics.Add("Could not read libraryfolders.vdf: " + exception.Message);
                    }
                }
                if (libraryFiles.Length == 0) diagnostics.Add("Steam libraryfolders.vdf was not found.");

                foreach (string library in NormalizeDistinct(libraries))
                    TryDiscoverLibrary(steamRoot, library, installations, diagnostics);
            }

            PlateUpInstallation[] distinct = installations
                .GroupBy(item => item.GameRoot, PathComparer)
                .Select(group => group.First())
                .OrderBy(item => item.GameRoot, PathComparer)
                .ToArray();
            if (distinct.Length > 1) diagnostics.Add("Multiple valid PlateUp installations were found; select one manually.");
            return new PlateUpDiscoveryResult(distinct, diagnostics);
        }

        public PlateUpInstallationValidation ValidateManualRoot(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
                return new PlateUpInstallationValidation(null, new[] { "No PlateUp folder was selected." });
            try
            {
                PlateUpPlatformLayout layout = PlateUpPlatformLayout.ResolveLayout(selectedPath, _platform);
                string[] diagnostics = DiagnoseInvalidLayout(layout).ToArray();
                return new PlateUpInstallationValidation(diagnostics.Length == 0
                    ? new PlateUpInstallation(layout, null, null, null, null, null) : null, diagnostics);
            }
            catch (Exception exception) when (IsPathError(exception) || IsReadError(exception))
            {
                return new PlateUpInstallationValidation(null, new[] { "The selected PlateUp folder could not be read: " + exception.Message });
            }
        }

        public PlateUpInstallationValidation ValidateSavedRoot(string selectedPath)
        {
            PlateUpInstallationValidation manual = ValidateManualRoot(selectedPath);
            if (!manual.IsValid) return manual;

            string gameRoot = manual.Installation.GameRoot;
            DirectoryInfo current = new DirectoryInfo(gameRoot);
            while (current != null && !PathComparer.Equals(current.Name, "common")) current = current.Parent;
            if (current?.Parent == null || !PathComparer.Equals(current.Parent.Name, "steamapps")) return manual;

            string libraryRoot = current.Parent.Parent?.FullName;
            string manifestPath = libraryRoot == null ? null : Path.Combine(libraryRoot, "steamapps", "appmanifest_" + AppId + ".acf");
            if (manifestPath == null || !File.Exists(manifestPath)) return manual;

            var installations = new List<PlateUpInstallation>();
            var diagnostics = new List<string>();
            TryDiscoverLibrary(libraryRoot, libraryRoot, installations, diagnostics);
            PlateUpInstallation match = installations.FirstOrDefault(item => PathComparer.Equals(item.GameRoot, gameRoot));
            if (match != null) return new PlateUpInstallationValidation(match, diagnostics);
            if (diagnostics.Count == 0) diagnostics.Add("The saved PlateUp folder does not match its Steam manifest.");
            return new PlateUpInstallationValidation(null, diagnostics);
        }

        private void TryDiscoverLibrary(string steamRoot, string libraryRoot, ICollection<PlateUpInstallation> installations, ICollection<string> diagnostics)
        {
            string manifestPath = Path.Combine(libraryRoot, "steamapps", "appmanifest_" + AppId + ".acf");
            if (!File.Exists(manifestPath))
            {
                diagnostics.Add("PlateUp manifest was not found in a Steam library: " + libraryRoot);
                return;
            }

            try
            {
                SteamVdfNode appState = SteamVdf.Parse(File.ReadAllText(manifestPath)).GetObject("AppState");
                if (appState == null || !string.Equals(appState.GetString("appid"), AppId, StringComparison.Ordinal))
                {
                    diagnostics.Add("PlateUp manifest has the wrong app ID.");
                    return;
                }
                if (!string.Equals(appState.GetString("StateFlags"), "4", StringComparison.Ordinal))
                {
                    diagnostics.Add("PlateUp installation is incomplete or updating.");
                    return;
                }

                string installDir = appState.GetString("installdir");
                // Reject either separator on every host; Path.GetFileName alone is host-dependent.
                if (string.IsNullOrWhiteSpace(installDir) || installDir == "." || installDir == ".."
                    || installDir.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || Path.IsPathRooted(installDir))
                {
                    diagnostics.Add("PlateUp manifest contains an invalid install directory.");
                    return;
                }
                PlateUpPlatformLayout layout = PlateUpPlatformLayout.ResolveLayout(Path.Combine(libraryRoot, "steamapps", "common", installDir), _platform);
                string[] invalid = DiagnoseInvalidLayout(layout).ToArray();
                if (invalid.Length != 0)
                {
                    foreach (string diagnostic in invalid) diagnostics.Add(diagnostic);
                    return;
                }
                installations.Add(new PlateUpInstallation(layout, steamRoot, libraryRoot,
                    Path.Combine(libraryRoot, "steamapps", "workshop", "content", AppId), manifestPath, appState.GetString("buildid")));
            }
            catch (Exception exception) when (IsReadError(exception) || IsPathError(exception))
            {
                diagnostics.Add("PlateUp manifest or installation could not be read: " + exception.Message);
            }
        }

        private static IEnumerable<string> ReadLibraryFolders(string path)
        {
            SteamVdfNode folders = SteamVdf.Parse(File.ReadAllText(path)).GetObject("libraryfolders")
                ?? throw new SteamVdfParseException("Missing libraryfolders object", 1, 1);
            foreach (KeyValuePair<string, SteamVdfNode> pair in folders.Objects)
            {
                if (IsLibraryIndex(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value.GetString("path")))
                    yield return pair.Value.GetString("path");
            }
            foreach (KeyValuePair<string, string> pair in folders.Values)
            {
                if (IsLibraryIndex(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value)) yield return pair.Value;
            }
        }

        private static bool IsLibraryIndex(string key) => !string.IsNullOrEmpty(key) && key.All(value => value >= '0' && value <= '9');

        private List<string> NormalizeDistinct(IEnumerable<string> paths)
        {
            var seen = new HashSet<string>(PathComparer);
            var result = new List<string>();
            foreach (string path in paths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    string normalized = CanonicalizePath(Path.GetFullPath(path));
                    string volume = Path.GetPathRoot(normalized);
                    if (normalized.Length > volume.Length) normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (seen.Add(normalized)) result.Add(normalized);
                }
                catch (Exception exception) when (IsPathError(exception) || exception is PathTooLongException) { }
            }
            return result;
        }

        private static string CanonicalizePath(string path)
        {
            // Linux's conventional Steam roots often point to the same directory through symlinks.
            if (PlateUpPlatformLayout.IsWindows(PlateUpPlatformLayout.CurrentPlatform)) return path;
            IntPtr resolved = IntPtr.Zero;
            try
            {
                resolved = RealPath(path, IntPtr.Zero);
                return resolved == IntPtr.Zero ? path : Marshal.PtrToStringAnsi(resolved) ?? path;
            }
            catch (DllNotFoundException) { return path; }
            catch (EntryPointNotFoundException) { return path; }
            finally { if (resolved != IntPtr.Zero) Free(resolved); }
        }

        [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
        private static extern IntPtr RealPath(string path, IntPtr buffer);
        [DllImport("libc", EntryPoint = "free")]
        private static extern void Free(IntPtr pointer);

        private static IEnumerable<string> DiagnoseInvalidLayout(PlateUpPlatformLayout layout)
        {
            if (!File.Exists(layout.ExecutablePath)) yield return "PlateUp executable is missing: " + layout.ExecutablePath;
            if (!File.Exists(Path.Combine(layout.ManagedDirectory, "Kitchen.GameData.dll")))
                yield return "Kitchen.GameData.dll is missing from " + layout.ManagedDirectory;
            if (!HasSupportedAsset(layout.DataRoot)) yield return "No supported serialized asset file was found in " + layout.DataRoot;
        }

        private static bool HasSupportedAsset(string dataRoot) => Directory.Exists(dataRoot) &&
            (File.Exists(Path.Combine(dataRoot, "resources.assets")) || File.Exists(Path.Combine(dataRoot, "globalgamemanagers.assets"))
            || Directory.EnumerateFiles(dataRoot, "sharedassets*.assets", SearchOption.TopDirectoryOnly).Any());

        public static IReadOnlyList<string> DefaultSteamCandidates(RuntimePlatform platform, string homeDirectory)
        {
            var candidates = new List<string>();
            if (PlateUpPlatformLayout.IsWindows(platform))
            {
                AddRegistry(candidates, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
                AddRegistry(candidates, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
                AddRegistry(candidates, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(programFiles)) candidates.Add(Path.Combine(programFiles, "Steam"));
            }
            else if (!string.IsNullOrWhiteSpace(homeDirectory))
            {
                if (PlateUpPlatformLayout.IsMac(platform))
                    candidates.Add(Path.Combine(homeDirectory, "Library", "Application Support", "Steam"));
                else if (platform == RuntimePlatform.LinuxEditor || platform == RuntimePlatform.LinuxPlayer)
                {
                    candidates.Add(Path.Combine(homeDirectory, ".local", "share", "Steam"));
                    candidates.Add(Path.Combine(homeDirectory, ".steam", "steam"));
                    candidates.Add(Path.Combine(homeDirectory, ".steam", "root"));
                    candidates.Add(Path.Combine(homeDirectory, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
                }
            }
            return candidates;
        }

        public static string StandardGameRoot(RuntimePlatform platform, string programFilesX86)
        {
            if (!PlateUpPlatformLayout.IsWindows(platform) || string.IsNullOrWhiteSpace(programFilesX86)) return null;
            return Path.Combine(programFilesX86, "Steam", "steamapps", "common", "PlateUp");
        }

        private static bool IsReadError(Exception exception) => exception is IOException || exception is UnauthorizedAccessException || exception is SteamVdfParseException;
        private static bool IsPathError(Exception exception) => exception is ArgumentException || exception is NotSupportedException;

        private static void AddRegistry(ICollection<string> candidates, RegistryKey hive, string keyPath, string valueName)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(keyPath))
                {
                    if (key?.GetValue(valueName) is string value) candidates.Add(value.Replace('/', Path.DirectorySeparatorChar));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is PlatformNotSupportedException) { }
        }
    }
}
