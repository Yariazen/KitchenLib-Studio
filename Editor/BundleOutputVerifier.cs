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
        public static readonly ExpectedGdo ScalarProof = new ExpectedGdo(
            "KitchenData.GameDifficultySettings",
            "Kitchen.GameData",
            "KLStudio Scalar Proof",
            -1950922447,
            false);

        public static readonly ExpectedGdo DishLinkProof = new ExpectedGdo(
            "KitchenData.Dish",
            "Kitchen.GameData",
            "KLStudio Link Proof Dish",
            -1950922446,
            false,
            true);
        public static readonly ExpectedGdo FullDishProof = new ExpectedGdo(
            "KitchenData.Dish", "Kitchen.GameData", "KLStudio Full Dish Proof", -1950922445, false, true);

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
        {
            var issues = new List<ValidationIssue>();
            string[] sidecars = expected.RequiresLinkManifest ? new[] { GdoOverlayManifest.AssetName } : Array.Empty<string>();
            GdoPackageAuditReport audit = GdoPackageAudit.Audit(packageRoot, new[] { bundlePath }, sidecars, new[] { expected.AssemblyName });
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
                int expectedSerializedAssetCount = expected.RequiresLinkManifest ? 2 : 1;
                if (serializedAssetNames.Length != expectedSerializedAssetCount)
                {
                    string expectedDescription = expected.RequiresLinkManifest
                        ? "exactly two serialized assets: one GDO and one required GDO link manifest"
                        : "exactly one serialized GDO asset";
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
                if (gdos.Length != 1)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, bundlePath, string.Empty, $"Expected exactly one GameDataObject, found {gdos.Length}."));
                    return issues;
                }
                TextAsset[] linkManifests = bundle.LoadAllAssets<TextAsset>()
                    .Where(asset => string.Equals(asset.name, Path.GetFileNameWithoutExtension(GdoOverlayManifest.AssetName), StringComparison.Ordinal))
                    .ToArray();
                int expectedManifestCount = expected.RequiresLinkManifest ? 1 : 0;
                if (linkManifests.Length != expectedManifestCount)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Fatal,
                        bundlePath,
                        string.Empty,
                        $"Expected exactly {expectedManifestCount} GDO link manifest asset(s), found {linkManifests.Length}."));
                }

                GameDataObject actual = gdos[0];
                string actualType = actual.GetType().FullName;
                string actualAssembly = actual.GetType().Assembly.GetName().Name;
                if (!string.Equals(actualType, expected.TypeFullName, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, actual.name, string.Empty, $"Bundle GDO type is '{actualType}', expected '{expected.TypeFullName}'."));
                }
                if (!string.Equals(actualAssembly, expected.AssemblyName, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, actual.name, string.Empty, $"Bundle GDO assembly is '{actualAssembly}', expected '{expected.AssemblyName}'."));
                }
                if (!string.Equals(actual.name, expected.AssetName, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, actual.name, "name", $"Bundle GDO asset name is '{actual.name}', expected '{expected.AssetName}'."));
                }
                if (actual.ID != expected.ID)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, actual.name, "ID", $"Bundle GDO ID is {actual.ID}, expected {expected.ID}."));
                }

                var difficulty = actual as GameDifficultySettings;
                if (difficulty != null && difficulty.IsActive != expected.IsActive)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, actual.name, "IsActive", "Bundle GDO active state does not match the expected fixture."));
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
