using System.IO;
using System;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal static class KitchenLibStudioGeneratedPaths
    {
        public const string AssetRoot = "Assets/KitchenLibStudio.Generated";
        public const string StateRoot = "Library/KitchenLibStudio";
        public const string SchemaFileName = "plateup-schema.json";
        public const string VanillaGdoCatalogFileName = "vanilla-gdo-catalog.json";
        public const string VanillaSnapshotCatalogFileName = "vanilla-snapshot-catalog.json";
        public const string VanillaSnapshotIndexFileName = "vanilla-snapshot-index.json";
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string AssetDirectory => Path.GetFullPath(Path.Combine(ProjectRoot, AssetRoot));
        public static string StateDirectory => Path.GetFullPath(Path.Combine(ProjectRoot, StateRoot));
        public static string MirrorStatePath => Path.Combine(StateDirectory, "mirror-state.json");
        public static string SchemaPath => GeneratedFilePath(SchemaFileName);
        public static string VanillaGdoCatalogPath => GeneratedFilePath(VanillaGdoCatalogFileName);
        public static string VanillaSnapshotCatalogPath => GeneratedFilePath(VanillaSnapshotCatalogFileName);
        public static string VanillaSnapshotIndexPath => GeneratedFilePath(VanillaSnapshotIndexFileName);

        public static string GeneratedAssetPath(string relativePath) => AssetRoot + "/" + NormalizeRelative(relativePath);
        public static string GeneratedFilePath(string relativePath) => Path.Combine(AssetDirectory,
            NormalizeRelative(relativePath).Replace('/', Path.DirectorySeparatorChar));

        public static string PackageAssetPath(string relativePath)
        {
            PackageInfo package = RequirePackage();
            return package.assetPath.TrimEnd('/') + "/" + NormalizeRelative(relativePath);
        }

        public static string PackageFilePath(string relativePath)
        {
            PackageInfo package = RequirePackage();
            return Path.Combine(package.resolvedPath,
                NormalizeRelative(relativePath).Replace('/', Path.DirectorySeparatorChar));
        }

        private static PackageInfo RequirePackage() => PackageInfo.FindForAssembly(typeof(KitchenLibStudioGeneratedPaths).Assembly)
            ?? throw new InvalidOperationException("KitchenLib Studio package information is unavailable.");

        private static string NormalizeRelative(string relativePath)
        {
            string value = (relativePath ?? string.Empty).Replace('\\', '/').Trim('/');
            if (value.Length == 0 || value.Contains(":") || value.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                throw new ArgumentException("A package-relative path without traversal is required.", nameof(relativePath));
            return value;
        }
    }
}
