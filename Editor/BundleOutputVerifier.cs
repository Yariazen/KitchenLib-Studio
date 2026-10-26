using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class ExpectedGdo
    {
        public ExpectedGdo(string typeFullName, string assemblyName, string assetName, int id, bool isActive, bool requiresLinkManifest = false)
        {
            TypeFullName = typeFullName;
            AssemblyName = assemblyName;
            AssetName = assetName;
            ID = id;
            IsActive = isActive;
            RequiresLinkManifest = requiresLinkManifest;
        }

        public string TypeFullName { get; }
        public string AssemblyName { get; }
        public string AssetName { get; }
        public int ID { get; }
        public bool IsActive { get; }
        public bool RequiresLinkManifest { get; }
    }

    public static class BundleOutputVerifier
    {
        public static IReadOnlyList<ValidationIssue> Verify(string packageRoot, string bundlePath, ExpectedGdo expected)
            => Verify(packageRoot, bundlePath, new[] { expected }, expected.RequiresLinkManifest);

        public static IReadOnlyList<ValidationIssue> Verify(string packageRoot, string bundlePath,
            IReadOnlyList<ExpectedGdo> expected, bool requiresLinkManifest)
        {
            if (expected == null || expected.Count == 0) throw new ArgumentException("At least one expected GDO is required.", nameof(expected));
            var issues = new List<ValidationIssue>();
            string[] sidecars = requiresLinkManifest ? new[] { GdoOverlayManifest.AssetName } : Array.Empty<string>();
            GdoPackageAuditReport audit = GdoPackageAudit.Audit(packageRoot, new[] { bundlePath }, sidecars,
                expected.Select(item => item.AssemblyName).Distinct(StringComparer.Ordinal));
            foreach (GdoPackageAuditFinding finding in audit.Findings)
                issues.Add(new ValidationIssue(finding.Severity, finding.Path, finding.Category, finding.Message));
            if (audit.Findings.Any(finding => finding.Category == "MalformedBundle" || finding.Category == "MissingBundle"))
                return issues;

            if (!File.Exists(bundlePath))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, bundlePath, string.Empty, "AssetBundle file does not exist."));
                return issues;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, bundlePath, string.Empty, "AssetBundle could not be loaded."));
                return issues;
            }

            try
            {
                string[] serializedAssetNames = bundle.GetAllAssetNames();
                int expectedSerializedAssetCount = expected.Count + (requiresLinkManifest ? 1 : 0);
                if (serializedAssetNames.Length != expectedSerializedAssetCount)
                {
                    string expectedDescription = expected.Count + " GDO asset(s)" +
                        (requiresLinkManifest ? " and one required GDO overlay manifest" : string.Empty);
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Fatal,
                        bundlePath,
                        string.Empty,
                        $"Expected {expectedDescription}; found {serializedAssetNames.Length}."));
                    return issues;
                }

                UnityEngine.Object[] allAssets = bundle.LoadAllAssets();
                foreach (UnityEngine.Object asset in allAssets)
                {
                    string assemblyName = asset.GetType().Assembly.GetName().Name;
                    if (assemblyName.IndexOf(".Editor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        asset.GetType().Namespace?.StartsWith("KitchenLib.Studio.Editor", StringComparison.Ordinal) == true)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Fatal, asset.name, string.Empty, "Bundle contains an editor-only serialized type."));
                    }
                }

                GameDataObject[] gdos = bundle.LoadAllAssets<GameDataObject>();
                if (gdos.Length != expected.Count)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, bundlePath, string.Empty,
                        $"Expected exactly {expected.Count} GameDataObject asset(s), found {gdos.Length}."));
                    return issues;
                }
                TextAsset[] linkManifests = bundle.LoadAllAssets<TextAsset>()
                    .Where(asset => string.Equals(asset.name, Path.GetFileNameWithoutExtension(GdoOverlayManifest.AssetName), StringComparison.Ordinal))
                    .ToArray();
                int expectedManifestCount = requiresLinkManifest ? 1 : 0;
                if (linkManifests.Length != expectedManifestCount)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Fatal,
                        bundlePath,
                        string.Empty,
                        $"Expected exactly {expectedManifestCount} GDO link manifest asset(s), found {linkManifests.Length}."));
                }

                foreach (ExpectedGdo item in expected)
                {
                GameDataObject actual = gdos.SingleOrDefault(value => value.ID == item.ID);
                if (actual == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, bundlePath, "ID",
                        $"Expected GDO ID {item.ID} was not found in the final bundle."));
                    continue;
                }
                string actualType = actual.GetType().FullName;
                string actualAssembly = actual.GetType().Assembly.GetName().Name;
                if (!string.Equals(actualType, item.TypeFullName, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, actual.name, string.Empty, $"Bundle GDO type is '{actualType}', expected '{item.TypeFullName}'."));
                }
                if (!string.Equals(actualAssembly, item.AssemblyName, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, actual.name, string.Empty, $"Bundle GDO assembly is '{actualAssembly}', expected '{item.AssemblyName}'."));
                }
                if (!string.Equals(actual.name, item.AssetName, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, actual.name, "name", $"Bundle GDO asset name is '{actual.name}', expected '{item.AssetName}'."));
                }

                var difficulty = actual as GameDifficultySettings;
                if (difficulty != null && difficulty.IsActive != item.IsActive)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, actual.name, "IsActive", "Bundle GDO active state does not match the authored value."));
                }
                }
            }
            finally
            {
                bundle.Unload(true);
            }

            return issues;
        }
    }
}
