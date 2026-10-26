using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace KitchenLib.Studio.Editor
{
    public sealed class PlateUpInstallationDiscovery
    {
        public const string AppId = "1599600";

        public PlateUpDiscoveryResult Discover() => Discover(DefaultSteamCandidates());

        public PlateUpDiscoveryResult Discover(IEnumerable<string> steamCandidates)
        {
            var diagnostics = new List<string>();
            var installations = new List<PlateUpInstallation>();
            var steamRoots = NormalizeDistinct(steamCandidates);
            if (steamRoots.Count == 0) diagnostics.Add("Steam was not found; select the PlateUp folder manually.");

            foreach (string steamRoot in steamRoots)
            {
                var libraries = new List<string> { steamRoot };
                string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFile))
                {
                    try { libraries.AddRange(ReadLibraryFolders(libraryFile)); }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SteamVdfParseException)
                    {
                        diagnostics.Add("Could not read libraryfolders.vdf: " + exception.Message);
                    }
                }
                else diagnostics.Add("Steam libraryfolders.vdf was not found.");

                foreach (string library in NormalizeDistinct(libraries))
                    TryDiscoverLibrary(steamRoot, library, installations, diagnostics);
            }

            PlateUpInstallation[] distinct = installations
                .GroupBy(item => item.GameRoot, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.GameRoot, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (distinct.Length > 1) diagnostics.Add("Multiple valid PlateUp installations were found; select one manually.");
            return new PlateUpDiscoveryResult(distinct, diagnostics);
        }

        public PlateUpInstallationValidation ValidateManualRoot(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
                return new PlateUpInstallationValidation(null, new[] { "No PlateUp folder was selected." });
            string root;
            try { root = Path.GetFullPath(selectedPath); }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return new PlateUpInstallationValidation(null, new[] { "The selected path is invalid: " + exception.Message });
            }

            string valid = FindValidGameRoot(root);
            if (valid == null) return new PlateUpInstallationValidation(null, DiagnoseInvalidRoot(root));
            return new PlateUpInstallationValidation(new PlateUpInstallation(valid, null, null, null, null, null), Array.Empty<string>());
        }

        public PlateUpInstallationValidation ValidateSavedRoot(string selectedPath)
        {
            PlateUpInstallationValidation manual = ValidateManualRoot(selectedPath);
            if (!manual.IsValid) return manual;

            string gameRoot = manual.Installation.GameRoot;
            DirectoryInfo current = new DirectoryInfo(gameRoot);
            while (current != null && !string.Equals(current.Name, "common", StringComparison.OrdinalIgnoreCase))
                current = current.Parent;
            if (current?.Parent == null || !string.Equals(current.Parent.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                return manual;

            string libraryRoot = current.Parent.Parent?.FullName;
            string manifestPath = libraryRoot == null ? null : Path.Combine(libraryRoot, "steamapps", "appmanifest_" + AppId + ".acf");
            if (manifestPath == null || !File.Exists(manifestPath)) return manual;

            var installations = new List<PlateUpInstallation>();
            var diagnostics = new List<string>();
            TryDiscoverLibrary(libraryRoot, libraryRoot, installations, diagnostics);
            PlateUpInstallation match = installations.FirstOrDefault(item =>
                string.Equals(item.GameRoot, gameRoot, StringComparison.OrdinalIgnoreCase));
            if (match != null) return new PlateUpInstallationValidation(match, diagnostics);
            if (diagnostics.Count == 0) diagnostics.Add("The saved PlateUp folder does not match its Steam manifest.");
            return new PlateUpInstallationValidation(null, diagnostics);
        }

        private static void TryDiscoverLibrary(string steamRoot, string libraryRoot, ICollection<PlateUpInstallation> installations, ICollection<string> diagnostics)
        {
            string manifestPath = Path.Combine(libraryRoot, "steamapps", "appmanifest_" + AppId + ".acf");
            if (!File.Exists(manifestPath))
            {
                diagnostics.Add("PlateUp manifest was not found in a Steam library.");
                return;
            }

            SteamVdfNode appState;
            try { appState = SteamVdf.Parse(File.ReadAllText(manifestPath)).GetObject("AppState"); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SteamVdfParseException)
            {
                diagnostics.Add("PlateUp manifest could not be read: " + exception.Message);
                return;
            }
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
            if (string.IsNullOrWhiteSpace(installDir) || !string.Equals(Path.GetFileName(installDir), installDir, StringComparison.Ordinal))
            {
                diagnostics.Add("PlateUp manifest contains an invalid install directory.");
                return;
            }
            string candidate = Path.Combine(libraryRoot, "steamapps", "common", installDir);
            string gameRoot = FindValidGameRoot(candidate);
            if (gameRoot == null)
            {
                foreach (string diagnostic in DiagnoseInvalidRoot(candidate)) diagnostics.Add(diagnostic);
                return;
            }

            installations.Add(new PlateUpInstallation(
                gameRoot,
                steamRoot,
                libraryRoot,
                Path.Combine(libraryRoot, "steamapps", "workshop", "content", AppId),
                manifestPath,
                appState.GetString("buildid")));
        }

        private static IEnumerable<string> ReadLibraryFolders(string path)
        {
            SteamVdfNode folders = SteamVdf.Parse(File.ReadAllText(path)).GetObject("libraryfolders")
                ?? throw new SteamVdfParseException("Missing libraryfolders object", 1, 1);
            foreach (KeyValuePair<string, SteamVdfNode> pair in folders.Objects)
            {
                if (int.TryParse(pair.Key, out _) && !string.IsNullOrWhiteSpace(pair.Value.GetString("path")))
                    yield return pair.Value.GetString("path");
            }
            foreach (KeyValuePair<string, string> pair in folders.Values)
            {
                if (int.TryParse(pair.Key, out _) && !string.IsNullOrWhiteSpace(pair.Value)) yield return pair.Value;
            }
        }

        private static List<string> NormalizeDistinct(IEnumerable<string> paths)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (string path in paths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    string normalized = Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (seen.Add(normalized)) result.Add(normalized);
                }
                catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException) { }
            }
            return result;
        }

        private static string FindValidGameRoot(string root)
        {
            if (IsValidGameRoot(root)) return Path.GetFullPath(root);
            string name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string nested = string.IsNullOrEmpty(name) ? null : Path.Combine(root, name);
            return nested != null && IsValidGameRoot(nested) ? Path.GetFullPath(nested) : null;
        }

        private static bool IsValidGameRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
            string data = Path.Combine(root, "PlateUp_Data");
            return File.Exists(Path.Combine(root, "PlateUp.exe"))
                && File.Exists(Path.Combine(data, "Managed", "Kitchen.GameData.dll"))
                && HasSupportedAsset(data);
        }

        private static bool HasSupportedAsset(string dataRoot)
        {
            if (!Directory.Exists(dataRoot)) return false;
            return File.Exists(Path.Combine(dataRoot, "resources.assets"))
                || File.Exists(Path.Combine(dataRoot, "globalgamemanagers.assets"))
                || Directory.GetFiles(dataRoot, "sharedassets*.assets", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static IEnumerable<string> DiagnoseInvalidRoot(string root)
        {
            string data = Path.Combine(root, "PlateUp_Data");
            if (!File.Exists(Path.Combine(root, "PlateUp.exe"))) yield return "PlateUp.exe is missing.";
            if (!File.Exists(Path.Combine(data, "Managed", "Kitchen.GameData.dll"))) yield return "PlateUp_Data/Managed/Kitchen.GameData.dll is missing.";
            if (!HasSupportedAsset(data)) yield return "No supported serialized asset file was found in PlateUp_Data.";
        }

        private static IEnumerable<string> DefaultSteamCandidates()
        {
            var candidates = new List<string>();
            AddRegistry(candidates, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            AddRegistry(candidates, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86)) candidates.Add(Path.Combine(programFilesX86, "Steam"));
            return candidates;
        }

        private static void AddRegistry(ICollection<string> candidates, RegistryKey hive, string keyPath, string valueName)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(keyPath))
                {
                    if (key?.GetValue(valueName) is string value) candidates.Add(value.Replace('/', Path.DirectorySeparatorChar));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is System.Security.SecurityException) { }
        }
    }
}
