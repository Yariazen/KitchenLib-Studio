using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Packaging
{
    internal sealed class BundleBuildRequest
    {
        public BundleBuildRequest(IReadOnlyList<string> assetPaths, string outputRoot, string bundleFileName,
            string productId = "selected-assets")
        {
            AssetPaths = assetPaths;
            OutputRoot = outputRoot;
            BundleFileName = bundleFileName;
            ProductId = productId;
        }

        public IReadOnlyList<string> AssetPaths { get; }
        public string OutputRoot { get; }
        public string BundleFileName { get; }
        public string ProductId { get; }
    }

    internal sealed class BundleBuildResult
    {
        public BundleBuildResult(string bundlePath, IReadOnlyList<ValidationIssue> issues, GdoPackageAuditReport auditReport = null)
        {
            BundlePath = bundlePath;
            Issues = issues;
            AuditReport = auditReport;
        }

        public string BundlePath { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public GdoPackageAuditReport AuditReport { get; }
        public bool Succeeded => Issues.All(issue => issue.Severity != ValidationSeverity.Error && issue.Severity != ValidationSeverity.Fatal);
    }

    internal static class GdoBundleBuilder
    {
        public const string StagingRoot = "Assets/__KitchenLibStudioBuild";
        public const string LinkManifestAssetPath = StagingRoot + "/" + GdoOverlayManifest.AssetName;

        public static BundleBuildResult Build(BundleBuildRequest request)
        {
            using (StudioBuildProcessorRegistry.BeginBuild()) return Prepare(request);
        }

        private static BundleBuildResult Prepare(BundleBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var issues = new List<ValidationIssue>();
            if (!KitchenLibStudioBootstrap.TryValidateCurrentOutput(out string mirrorError))
                issues.Add(new ValidationIssue(ValidationSeverity.Error, KitchenLibStudioGeneratedPaths.AssetRoot,
                    "GeneratedMirrors", mirrorError));
            ValidateBundleFileName(request.BundleFileName, issues);
            var assets = new List<GameDataObject>();
            if (request.AssetPaths == null || request.AssetPaths.Count == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, string.Empty, "AssetPaths", "At least one GDO asset is required."));
            }
            else
            {
                foreach (string assetPath in request.AssetPaths)
                {
                    GameDataObject asset = AssetDatabase.LoadAssetAtPath<GameDataObject>(assetPath);
                    if (asset == null)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, assetPath, string.Empty, "Asset path does not contain a GameDataObject."));
                    }
                    else
                    {
                        assets.Add(asset);
                    }
                }
            }

            issues.AddRange(GdoValidator.Validate(assets));
            IReadOnlyList<GdoOverlayAuthoringAsset> companions = GdoOverlayAuthoringRepository.FindForTargets(assets);
            issues.AddRange(GdoValidator.ValidateLinks(assets, companions));
            string bundlePath = Path.Combine(request.OutputRoot ?? string.Empty, request.BundleFileName ?? string.Empty);
            if (issues.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal))
            {
                return new BundleBuildResult(bundlePath, issues);
            }
            using (var preparation = StudioBuildProcessorRegistry.HasProcessors ? new StudioBuildPreparation(assets, companions) : null)
            {
                string json = null;
                try
                {
                    // Validate the original authoring before any processor can replace a value.
                    if (companions.Count > 0)
                        json = GdoOverlayManifestWriter.Write(assets, companions, KitchenLibStudioGeneratedPaths.SchemaPath);
                    if (preparation != null)
                    {
                        StudioBuildProcessorRegistry.Process(preparation, request.ProductId, issues);
                        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal))
                            return new BundleBuildResult(bundlePath, issues);
                        companions = preparation.Companions;
                        issues.AddRange(GdoValidator.Validate(assets));
                        issues.AddRange(GdoValidator.ValidateLinks(assets, companions));
                        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal))
                            return new BundleBuildResult(bundlePath, issues);
                        if (companions.Count > 0)
                            json = GdoOverlayManifestWriter.Write(assets, companions, KitchenLibStudioGeneratedPaths.SchemaPath);
                    }
                }
                catch (Exception)
                {
                    issues.Add(StudioBuildProcessorRegistry.Error("BuildPreparation", "Authored or processed companion data failed mandatory manifest validation."));
                    return new BundleBuildResult(bundlePath, issues);
                }
                return BuildValidated(request, assets, companions, bundlePath, json, issues);
            }
        }

        private static BundleBuildResult BuildValidated(BundleBuildRequest request, List<GameDataObject> assets,
            IReadOnlyList<GdoOverlayAuthoringAsset> companions, string bundlePath, string json, List<ValidationIssue> issues)
        {
            ExpectedGdo[] expected = assets.Select(asset => new ExpectedGdo(asset.GetType().FullName,
                asset.GetType().Assembly.GetName().Name, asset.name, asset.ID,
                asset is GameDifficultySettings difficulty && difficulty.IsActive, companions.Count > 0)).ToArray();

            try
            {
                AssetDatabase.DeleteAsset(StagingRoot);
                var bundleAssetPaths = request.AssetPaths.ToList();
                if (companions.Count > 0)
                {
                    CreateStagedManifest(json);
                    bundleAssetPaths.Add(LinkManifestAssetPath);
                }

                Directory.CreateDirectory(request.OutputRoot);
                var build = new AssetBundleBuild
                {
                    assetBundleName = request.BundleFileName,
                    assetNames = bundleAssetPaths.ToArray()
                };
                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                    request.OutputRoot,
                    new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle,
                    BuildTarget.StandaloneWindows64);
                if (manifest == null || !File.Exists(bundlePath))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, bundlePath, string.Empty, "Unity did not produce the requested AssetBundle."));
                }
                if (!issues.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal))
                {
                    string packageRoot = Directory.GetParent(Path.GetFullPath(request.OutputRoot))?.FullName ?? Path.GetFullPath(request.OutputRoot);
                    string[] sidecars = companions.Count > 0 ? new[] { GdoOverlayManifest.AssetName } : Array.Empty<string>();
                    GdoPackageAuditReport audit = GdoPackageAudit.Audit(
                        packageRoot, new[] { bundlePath }, sidecars, new[] { "Kitchen.GameData" });
                    GdoPackageAudit.Write(packageRoot, audit);
                    foreach (GdoPackageAuditFinding finding in audit.Findings.Where(finding => finding.Severity == ValidationSeverity.Fatal))
                        issues.Add(new ValidationIssue(ValidationSeverity.Fatal, finding.Path, finding.Category, finding.Message));
                    issues.AddRange(BundleOutputVerifier.Verify(packageRoot, bundlePath, expected, companions.Count > 0)
                        .Where(issue => !issues.Any(existing => existing.Severity == issue.Severity &&
                            existing.AssetPath == issue.AssetPath && existing.PropertyPath == issue.PropertyPath &&
                            existing.Message == issue.Message)));
                    return new BundleBuildResult(bundlePath, issues, audit);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(StagingRoot);
            }

            return new BundleBuildResult(bundlePath, issues);
        }

        private static void CreateStagedManifest(string json)
        {
            if (!AssetDatabase.IsValidFolder(StagingRoot))
            {
                AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioBuild");
            }

            string projectRoot = KitchenLibStudioGeneratedPaths.ProjectRoot;
            string manifestPath = Path.Combine(projectRoot, LinkManifestAssetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(manifestPath, json, new System.Text.UTF8Encoding(false));
            File.WriteAllText(
                manifestPath + ".meta",
                "fileFormatVersion: 2\nguid: 6ea65bc3e9ab4b17a37ee69e593ae150\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
                new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(LinkManifestAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ValidateBundleFileName(string fileName, ICollection<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, fileName ?? string.Empty, "BundleFileName", "Bundle file name must be a single file name without directory traversal."));
                return;
            }

            if (!string.Equals(Path.GetExtension(fileName), ".assets", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, fileName, "BundleFileName", "Bundle file extension must be .assets."));
            }
        }
    }
}
