using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

namespace KitchenLib.Studio.Editor
{
    public static class GdoBundleGraphBuilder
    {
        public static BundleBuildResult Build(GdoBundleGraphAsset graph, string outputRoot = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (string.IsNullOrEmpty(outputRoot))
            {
                outputRoot = Path.Combine(KitchenLibStudioGeneratedPaths.ProjectRoot,
                    "Build", SafeName(graph.BundleName), "content");
            }

            GdoGraphDiagnostic[] preflight = GdoBundleGraphValidator.Validate(graph)
                .Where(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal).ToArray();
            if (preflight.Length != 0) return Failure(graph, outputRoot, preflight);
            if (!GdoGraphProjectionApplier.TryApply(graph, out IReadOnlyList<GdoGraphDiagnostic> applyDiagnostics, false))
                return Failure(graph, outputRoot, applyDiagnostics);
            GdoGraphDiagnostic[] diagnostics = GdoBundleGraphValidator.Validate(graph)
                .Where(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal).ToArray();
            if (diagnostics.Length != 0) return Failure(graph, outputRoot, diagnostics);

            string[] assetPaths = graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local && !node.IsBroken)
                .Select(node => AssetDatabase.GetAssetPath(node.Asset)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            AssetDatabase.SaveAssets();
            return GdoBundleBuilder.Build(new BundleBuildRequest(assetPaths, outputRoot, graph.BundleFileName));
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
