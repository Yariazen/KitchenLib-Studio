using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace KitchenLib.Studio.Editor
{
    /// <summary>Rename-based publication with a durable recovery record outside the imported asset tree.</summary>
    public static class AtomicGeneratedDirectory
    {
        private static string Transaction => Path.Combine(KitchenLibStudioGeneratedPaths.StateDirectory, "Publication");
        private static string Backup => Path.Combine(Transaction, "Previous");
        private static string Pending => Path.Combine(Transaction, "pending");
        private static string PreviousState => Path.Combine(Transaction, "mirror-state.json");

        internal static void Publish(string stagingRoot, string finalAssetRoot) => Publish(stagingRoot, finalAssetRoot, () => { }, CancellationToken.None);

        internal static void Publish(string stagingRoot, string finalAssetRoot, Action commit, CancellationToken cancellation)
        {
            ValidatePaths(stagingRoot, finalAssetRoot);
            if (File.Exists(Pending)) throw new InvalidOperationException("An interrupted mirror publication must be recovered before generating again.");
            PlateUpMirrorGenerator.ValidateOutput(stagingRoot);
            cancellation.ThrowIfCancellationRequested();
            if (Directory.Exists(Transaction)) Directory.Delete(Transaction, true);
            Directory.CreateDirectory(Transaction);
            if (File.Exists(KitchenLibStudioGeneratedPaths.MirrorStatePath)) File.Copy(KitchenLibStudioGeneratedPaths.MirrorStatePath, PreviousState);
            bool existed = Directory.Exists(finalAssetRoot);
            // The record is flushed before moving the old directory. A crash always leaves a recoverable generation.
            using (var stream = new FileStream(Pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.WriteByte(existed ? (byte)1 : (byte)0); stream.Flush(true); }
            try
            {
                if (existed) Directory.Move(finalAssetRoot, Backup);
                cancellation.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(finalAssetRoot));
                Directory.Move(stagingRoot, finalAssetRoot);
                cancellation.ThrowIfCancellationRequested();
                commit();
                // After this durable commit point the new generation is authoritative, even if cleanup is interrupted.
                File.Delete(Pending);
            }
            catch { RecoverInterruptedPublication(); throw; }
            TryCleanup();
        }

        public static void RecoverInterruptedPublication()
        {
            if (!File.Exists(Pending)) return;
            RejectLinks(KitchenLibStudioGeneratedPaths.ProjectRoot, Transaction);
            RejectLinks(KitchenLibStudioGeneratedPaths.ProjectRoot, KitchenLibStudioGeneratedPaths.AssetDirectory);
            byte[] record = File.ReadAllBytes(Pending);
            if (record.Length != 1 || record[0] > 1) throw new InvalidDataException("Invalid mirror publication recovery record.");
            bool existed = record[0] == 1;
            string final = KitchenLibStudioGeneratedPaths.AssetDirectory;
            // With no backup and a previous generation, the crash preceded the first rename (or recovery finished it).
            if (Directory.Exists(Backup))
            {
                if (Directory.Exists(final)) Directory.Delete(final, true);
                Directory.Move(Backup, final);
            }
            else if (!existed && Directory.Exists(final)) Directory.Delete(final, true);
            string state = KitchenLibStudioGeneratedPaths.MirrorStatePath;
            if (File.Exists(PreviousState))
            {
                string temporary = state + ".recovery.tmp";
                File.Copy(PreviousState, temporary, true);
                if (File.Exists(state)) File.Replace(temporary, state, null); else File.Move(temporary, state);
            }
            else if (File.Exists(state)) File.Delete(state);
            File.Delete(Pending);
            TryCleanup();
        }

        private static void TryCleanup()
        {
            try { if (Directory.Exists(Transaction)) Directory.Delete(Transaction, true); }
            catch (IOException) { /* A committed publication remains valid; cleanup is retried next run. */ }
            catch (UnauthorizedAccessException) { }
        }

        internal static void ValidatePaths(string stagingRoot, string finalAssetRoot)
        {
            string staging = Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.Combine(KitchenLibStudioGeneratedPaths.StateDirectory, "Staging");
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(finalAssetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    KitchenLibStudioGeneratedPaths.AssetDirectory, comparison)
                || !string.Equals(Path.GetDirectoryName(staging), parent, comparison))
                throw new InvalidOperationException("Mirror generation may only publish a direct Library/KitchenLibStudio/Staging child to Assets/KitchenLibStudio.Generated in the consuming project.");
            RejectLinks(KitchenLibStudioGeneratedPaths.ProjectRoot, staging);
            RejectLinks(KitchenLibStudioGeneratedPaths.ProjectRoot, finalAssetRoot);
            RejectLinks(KitchenLibStudioGeneratedPaths.ProjectRoot, Transaction);
        }

        private static void RejectLinks(string root, string target)
        {
            for (string path = Path.GetFullPath(target); path != null && path.Length >= root.Length; path = Path.GetDirectoryName(path))
                if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Generated paths cannot pass through symbolic links: " + path);
            if (Directory.Exists(target))
                foreach (string child in Directory.EnumerateFileSystemEntries(target))
                {
                    FileAttributes attributes = File.GetAttributes(child);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Generated directories cannot contain symbolic links: " + child);
                    if ((attributes & FileAttributes.Directory) != 0) RejectLinks(root, child);
                }
        }
    }
}
