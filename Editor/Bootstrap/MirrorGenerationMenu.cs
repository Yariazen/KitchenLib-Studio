using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KitchenLib.Studio.Editor
{
    public static class MirrorGenerationMenu
    {
        public static void RegenerateMirrors()
        {
            PlateUpInstallation installation = PlateUpInstallationSettings.ResolveInstallation(true);
            if (installation == null) return;
            string root = installation.GameRoot;

            try
            {
                string stdout = GenerateMirrors(root); KitchenLibStudioBootstrap.CompletePublication();
                Debug.Log(stdout); EditorUtility.DisplayDialog("KitchenLib Studio", "PlateUp mirrors regenerated successfully.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Mirror generation failed", exception.Message, "OK");
            }
        }

        public static string GenerateMirrors(string plateUpRoot)
        {
            ResolveGameDataAssembly(plateUpRoot);
            throw new NotSupportedException("The mirror generator is not installed in this development snapshot.");
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
