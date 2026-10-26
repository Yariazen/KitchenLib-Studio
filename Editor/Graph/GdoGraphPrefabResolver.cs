using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

using KitchenLib.Studio.Editor.Packaging;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphPrefabResolver
    {
        public static string PrimitiveAssetPath(GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (record == null) throw new ArgumentNullException(nameof(record));
            return GdoBundleProjectRepository.PrefabFolder(graph) + "/" + record.Id + ".prefab";
        }

        public static GameObject Resolve(GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            IReadOnlyList<GdoGraphDiagnostic> issues = Validate(graph, record);
            if (issues.Count != 0) throw new InvalidOperationException(issues[0].Message);
            if (record.SourceMode == GdoGraphPrefabSourceMode.WorkspacePrefab)
            {
                string path = AssetDatabase.GUIDToAssetPath(record.WorkspacePrefabGuid);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            EnsureFolder(GdoBundleProjectRepository.PrefabFolder(graph));
            GameObject instance = GameObject.CreatePrimitive(record.PrimitiveType);
            try
            {
                instance.name = record.DisplayName;
                instance.transform.localScale = record.Scale;
                if (!record.IncludeCollider)
                {
                    Collider collider = instance.GetComponent<Collider>();
                    if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
                }
                if (record.Material != null)
                {
                    Renderer renderer = instance.GetComponent<Renderer>();
                    if (renderer != null) renderer.sharedMaterial = record.Material;
                }
                string path = PrimitiveAssetPath(graph, record);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                if (saved == null) throw new InvalidOperationException("Unity could not save the generated primitive prefab.");
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        public static IReadOnlyList<GdoGraphDiagnostic> Validate(
            GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            var issues = new List<GdoGraphDiagnostic>();
            if (graph == null || record == null)
            {
                issues.Add(Issue(record?.Id, "Prefab source is missing."));
                return issues;
            }
            if (record.SourceMode == GdoGraphPrefabSourceMode.Primitive)
            {
                if (record.Scale.x <= 0 || record.Scale.y <= 0 || record.Scale.z <= 0)
                    issues.Add(Issue(record.Id, "Primitive scale components must be greater than zero."));
                return issues;
            }
            string path = AssetDatabase.GUIDToAssetPath(record.WorkspacePrefabGuid);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (source == null || PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.NotAPrefab)
                issues.Add(Issue(record.Id, "Workspace prefab is missing or is not a prefab asset."));
            else if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.StartsWith(KitchenLibStudioGeneratedPaths.AssetRoot + "/", StringComparison.OrdinalIgnoreCase))
                issues.Add(Issue(record.Id, "Workspace prefab cannot come from an Editor or KitchenLibStudio generated folder."));
            return issues;
        }

        private static GdoGraphDiagnostic Issue(string id, string message) =>
            new GdoGraphDiagnostic(ValidationSeverity.Error, "InvalidPrefabSource", id ?? string.Empty,
                string.Empty, string.Empty, message);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
