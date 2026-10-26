using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    [Serializable]
    internal sealed class KitchenLibStudioMirrorState
    {
        public string generatorVersion;
        public string sourceFingerprint;
        public bool complete;
        // Relative generated file inventory: a partial deletion also invalidates publication.
        public string[] outputFiles;

        public static KitchenLibStudioMirrorState Read()
        {
            try
            {
                return File.Exists(KitchenLibStudioGeneratedPaths.MirrorStatePath)
                    ? JsonUtility.FromJson<KitchenLibStudioMirrorState>(File.ReadAllText(KitchenLibStudioGeneratedPaths.MirrorStatePath))
                    : null;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { return null; }
        }

        public static void WriteAtomically(KitchenLibStudioMirrorState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Directory.CreateDirectory(KitchenLibStudioGeneratedPaths.StateDirectory);
            string destination = KitchenLibStudioGeneratedPaths.MirrorStatePath;
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(state, true));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public bool HasCompleteOutput()
        {
            if (!complete || string.IsNullOrWhiteSpace(generatorVersion) || string.IsNullOrWhiteSpace(sourceFingerprint)
                || outputFiles == null || outputFiles.Length == 0) return false;
            if (!outputFiles.Contains("Kitchen.GameData.asmdef") || !outputFiles.Contains("Sirenix/Sirenix.Serialization.asmdef")) return false;
            return outputFiles.All(IsPublishedFile)
                && outputFiles.Any(path => path.EndsWith(".cs", StringComparison.Ordinal) && !path.StartsWith("Sirenix/", StringComparison.Ordinal))
                && outputFiles.Any(path => path.StartsWith("Sirenix/", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal));
        }

        public bool IsCurrent(PlateUpInstallation installation)
        {
            if (installation == null || !HasCompleteOutput()
                || !string.Equals(generatorVersion, PlateUpMirrorGenerator.Version, StringComparison.Ordinal)) return false;
            try
            {
                using (PlateUpMirrorModel model = PlateUpMirrorModel.Read(
                    Path.Combine(installation.ManagedDirectory, "Kitchen.GameData.dll"), installation.ManagedDirectory))
                    return string.Equals(sourceFingerprint, model.SourceFingerprint, StringComparison.Ordinal);
            }
            // Steam may be replacing files while the editor checks. Inspection failure must keep the gate closed.
            catch (Exception) { return false; }
        }

        private static bool IsPublishedFile(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("\\") || relative.Contains(":")
                || relative.StartsWith("/", StringComparison.Ordinal) || relative.Split('/').Any(part => part == ".." || part == "." || part.Length == 0)) return false;
            return File.Exists(Path.Combine(KitchenLibStudioGeneratedPaths.AssetDirectory, relative));
        }
    }
}
