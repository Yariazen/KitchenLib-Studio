using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace KitchenLib.Studio.Editor
{
    public static class PlateUpSourceFingerprint
    {
        public static void Validate(string gameRoot, VanillaAssetCatalogData catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            string dataRoot = PlateUpPlatformLayout.ResolveLayout(gameRoot).DataRoot;
            foreach (VanillaAssetSourceData source in catalog.sources ?? Array.Empty<VanillaAssetSourceData>())
            {
                string path = Path.Combine(dataRoot, source.relativePath ?? string.Empty);
                if (!File.Exists(path)) throw new FileNotFoundException("A catalogued PlateUp source is missing.", path);
                string actual = Compute(path);
                if (!string.Equals(actual, source.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The installed PlateUp source no longer matches the snapshot catalogue: " + source.relativePath);
            }
            string aggregate = ComputeStrings((catalog.sources ?? Array.Empty<VanillaAssetSourceData>()).Select(value => value.sha256));
            if (!string.Equals(aggregate, catalog.sourceFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The snapshot catalogue source fingerprint is inconsistent.");
        }

        public static string Compute(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        public static string ComputeStrings(System.Collections.Generic.IEnumerable<string> values)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", values ?? Enumerable.Empty<string>())))).Replace("-", string.Empty);
        }
    }
}
