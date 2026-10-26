using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using KitchenLib.Studio.Editor.Bootstrap;

namespace KitchenLib.Studio.Editor.Catalogs.Mods
{
    internal sealed class WorkshopModFileRecord
    {
        public WorkshopModFileRecord(string relativePath, long length, string sha256)
        { RelativePath = relativePath; Length = length; Sha256 = sha256; }
        public string RelativePath { get; }
        public long Length { get; }
        public string Sha256 { get; }
    }

    internal sealed class WorkshopModRecord
    {
        public WorkshopModRecord(string workshopId, IEnumerable<WorkshopModFileRecord> assemblies,
            IEnumerable<WorkshopModFileRecord> assetBundles)
        {
            WorkshopId = workshopId;
            ProviderKey = "workshop:" + workshopId;
            Assemblies = assemblies.ToArray();
            AssetBundles = assetBundles.ToArray();
        }
        public string WorkshopId { get; }
        public string ProviderKey { get; }
        public IReadOnlyList<WorkshopModFileRecord> Assemblies { get; }
        public IReadOnlyList<WorkshopModFileRecord> AssetBundles { get; }
    }

    internal sealed class WorkshopModDiscoveryResult
    {
        public WorkshopModDiscoveryResult(IEnumerable<WorkshopModRecord> mods, IEnumerable<string> diagnostics)
        { Mods = mods.ToArray(); Diagnostics = diagnostics.ToArray(); }
        public IReadOnlyList<WorkshopModRecord> Mods { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    internal sealed class WorkshopModDiscovery
    {
        private static readonly HashSet<string> AssetBundleExtensions = new HashSet<string>(
            new[] { ".assets", ".assetbundle", ".bundle" }, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<WorkshopModRecord> Discover(PlateUpInstallation installation) => Scan(installation).Mods;

        public WorkshopModDiscoveryResult Scan(PlateUpInstallation installation)
        {
            var mods = new List<WorkshopModRecord>();
            var diagnostics = new List<string>();
            string root = installation?.WorkshopRoot;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                diagnostics.Add("The PlateUp Workshop content directory does not exist: " + (root ?? "<unset>"));
                return new WorkshopModDiscoveryResult(mods, diagnostics);
            }

            foreach (string directory in Directory.GetDirectories(root).OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
            {
                string workshopId = Path.GetFileName(directory);
                if (!ulong.TryParse(workshopId, out _)) continue;
                try
                {
                    string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                    WorkshopModFileRecord[] assemblies = files.Where(path => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).Select(path => Describe(directory, path)).ToArray();
                    WorkshopModFileRecord[] bundles = files.Where(path => AssetBundleExtensions.Contains(Path.GetExtension(path)))
                        .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).Select(path => Describe(directory, path)).ToArray();
                    if (assemblies.Length != 0 || bundles.Length != 0)
                        mods.Add(new WorkshopModRecord(workshopId, assemblies, bundles));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    diagnostics.Add("Could not inspect Workshop item " + workshopId + ": " + exception.Message);
                }
            }
            return new WorkshopModDiscoveryResult(mods, diagnostics);
        }

        private static WorkshopModFileRecord Describe(string root, string path)
        {
            var info = new FileInfo(path);
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                string value = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
                return new WorkshopModFileRecord(path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar), info.Length, value);
            }
        }
    }
}
