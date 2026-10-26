using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KitchenLib.Studio.Editor
{
    public static class MirrorGenerationMenu
    {
        public static void RegenerateMirrors()
        {
            PlateUpInstallation installation = PlateUpCatalogImportMenu.ResolveInstallation(true);
            if (installation == null) return;
            string root = installation.GameRoot;

            try
            {
                string stdout = GenerateMirrors(root); AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log(stdout); EditorUtility.DisplayDialog("KitchenLib Studio", "PlateUp mirrors regenerated successfully.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Mirror generation failed", exception.Message, "OK");
            }
        }

        public static string GenerateMirrors(string plateUpRoot)
        {
            string assemblyPath = ResolveGameDataAssembly(plateUpRoot);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "KitchenLibStudio", "Generated"));
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string seed = Path.Combine(repositoryRoot, "CodeGeneration", "MirrorSeed");
            string manifestPath = Path.Combine(seed, "plateup-schema.json");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("The native mirror seed is missing. Restore CodeGeneration/MirrorSeed before regenerating the environment.", manifestPath);
            }

            string installedHash = ComputeSha256(assemblyPath);
            if (!IsMirrorSeedCompatible(File.ReadAllText(manifestPath), installedHash))
            {
                throw new InvalidOperationException("The native mirror seed does not match the installed Kitchen.GameData.dll. Update the checked-in mirror seed for this PlateUp version before regenerating.");
            }

            SynchronizeMirrorOutput(output);
            CopyDirectory(seed, output);
            return "Restored native PlateUp mirror sources from the compatible checked-in seed.";
        }

        public static string ResolveGameDataAssembly(string plateUpRoot)
        {
            if (string.IsNullOrWhiteSpace(plateUpRoot))
            {
                throw new ArgumentException("PlateUp root is required.", nameof(plateUpRoot));
            }

            string path = Path.GetFullPath(Path.Combine(PlateUpPlatformLayout.ResolveLayout(plateUpRoot).ManagedDirectory, "Kitchen.GameData.dll"));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Kitchen.GameData.dll was not found beneath the selected PlateUp installation.", path);
            }

            return path;
        }

        public static bool IsMirrorSeedCompatible(string manifestJson, string assemblySha256)
        {
            if (string.IsNullOrWhiteSpace(manifestJson) || string.IsNullOrWhiteSpace(assemblySha256)) return false;
            Match match = Regex.Match(manifestJson, "\\\"sourceSha256\\\"\\s*:\\s*\\\"(?<hash>[0-9A-Fa-f]+)\\\"");
            return match.Success && string.Equals(match.Groups["hash"].Value, assemblySha256, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldPreserveDuringMirrorSync(string fileName)
        {
            return string.Equals(fileName, "vanilla-gdo-catalog.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "vanilla-gdo-catalog.json.meta", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "vanilla-snapshot-catalog.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "vanilla-snapshot-catalog.json.meta", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "vanilla-snapshot-index.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "vanilla-snapshot-index.json.meta", StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) result.Append(value.ToString("X2"));
                return result.ToString();
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(destination, directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar)));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void SynchronizeMirrorOutput(string output)
        {
            if (!Directory.Exists(output)) return;
            foreach (string directory in Directory.GetDirectories(output)) Directory.Delete(directory, true);
            foreach (string file in Directory.GetFiles(output))
            {
                if (!ShouldPreserveDuringMirrorSync(Path.GetFileName(file))) File.Delete(file);
            }
        }

        public static string QuoteWindowsArgument(string argument)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(nameof(argument));
            }

            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return argument;
            }

            var result = new StringBuilder(argument.Length + 2);
            result.Append('"');
            int backslashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(character);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }
}
