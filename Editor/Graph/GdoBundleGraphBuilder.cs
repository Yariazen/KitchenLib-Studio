using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

using KitchenLib.Studio.Editor.Packaging;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoBundleGraphBuilder
    {
        public static BundleBuildResult Build(GdoBundleGraphAsset graph, string outputRoot = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (string.IsNullOrEmpty(outputRoot))
            {
                outputRoot = Path.Combine(KitchenLibStudioGeneratedPaths.ProjectRoot,
                    "Build", SafeName(graph.BundleName), "content");
            }

            using (StudioBuildProcessorRegistry.BeginBuild())
            using (var journal = StudioBuildProcessorRegistry.HasProcessors ? new StudioBuildAuthoringJournal(graph) : null)
                return BuildCore(graph, outputRoot, journal == null);
        }

        private static BundleBuildResult BuildCore(GdoBundleGraphAsset graph, string outputRoot, bool recordUndo)
        {
            GdoGraphDiagnostic[] preflight = GdoBundleGraphValidator.Validate(graph)
                .Where(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal).ToArray();
            if (preflight.Length != 0) return Failure(graph, outputRoot, preflight);
            if (!GdoGraphProjectionApplier.TryApply(graph, out IReadOnlyList<GdoGraphDiagnostic> applyDiagnostics, false, false, recordUndo))
                return Failure(graph, outputRoot, applyDiagnostics);
            GdoGraphDiagnostic[] diagnostics = GdoBundleGraphValidator.Validate(graph)
                .Where(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal).ToArray();
            if (diagnostics.Length != 0) return Failure(graph, outputRoot, diagnostics);

            string[] assetPaths = graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local && !node.IsBroken)
                .Select(node => AssetDatabase.GetAssetPath(node.Asset)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            // Save only the authored objects covered by this product's transaction. A build must not
            // save or clear unrelated editor work, including when a processor later rejects the build.
            foreach (GdoGraphNodeRecord node in graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local && !node.IsBroken))
            {
                AssetDatabase.SaveAssetIfDirty(node.Asset);
                var companion = Overlays.GdoOverlayAuthoringRepository.Find(node.Asset);
                if (companion != null) AssetDatabase.SaveAssetIfDirty(companion);
            }
            AssetDatabase.SaveAssetIfDirty(graph);
            return GdoBundleBuilder.Build(new BundleBuildRequest(assetPaths, outputRoot, graph.BundleFileName,
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph))));
        }

        private static BundleBuildResult Failure(GdoBundleGraphAsset graph, string outputRoot,
            IEnumerable<GdoGraphDiagnostic> diagnostics) => new BundleBuildResult(
                Path.Combine(outputRoot ?? string.Empty, graph.BundleFileName ?? string.Empty),
                diagnostics.Select(issue => new ValidationIssue(issue.Severity, issue.AssetPath,
                    issue.PropertyPath, issue.Message)).ToArray());

        private static string SafeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "GdoBundle" : value;
        }
    }
}
