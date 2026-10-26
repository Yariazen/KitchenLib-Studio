using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class GdoGraphPrefabProjection
    {
        public static bool TryApply(GdoBundleGraphAsset graph,
            out IReadOnlyList<GdoGraphDiagnostic> diagnostics, bool recordUndo = true)
        {
            var issues = new List<GdoGraphDiagnostic>();
            var active = graph.Edges.Where(edge =>
                graph.FindPrefab(edge.OutputNodeId) != null).ToArray();
            var resolved = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (GdoGraphEdgeRecord edge in active)
            {
                GdoGraphPrefabRecord source = graph.FindPrefab(edge.OutputNodeId);
                IReadOnlyList<GdoGraphDiagnostic> sourceIssues = GdoGraphPrefabResolver.Validate(graph, source);
                if (sourceIssues.Count != 0) { issues.AddRange(sourceIssues); continue; }
                try { resolved[source.Id] = GdoGraphPrefabResolver.Resolve(graph, source); }
                catch (Exception exception)
                {
                    issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Error, "PrefabResolutionFailed",
                        source.Id, string.Empty, string.Empty, exception.Message));
                }
            }
            if (issues.Count != 0) { diagnostics = issues; return false; }

            foreach (GdoGraphPrefabProjectionState stale in graph.PrefabProjectionStates.Where(state =>
                !active.Any(edge => edge.InputNodeId == state.OwnerNodeId &&
                    edge.InputPortPath == state.FieldPath)))
                ValidateTarget(graph, stale.OwnerNodeId, stale.FieldPath, issues);
            foreach (GdoGraphEdgeRecord edge in active)
                ValidateTarget(graph, edge.InputNodeId, edge.InputPortPath, issues);
            if (issues.Count != 0) { diagnostics = issues; return false; }

            if (recordUndo) Undo.RegisterCompleteObjectUndo(graph, "Apply prefab graph connections");
            foreach (GdoGraphPrefabProjectionState stale in graph.PrefabProjectionStates.Where(state =>
                !active.Any(edge => edge.InputNodeId == state.OwnerNodeId &&
                    edge.InputPortPath == state.FieldPath)).ToArray())
            {
                if (!TrySet(graph, stale.OwnerNodeId, stale.FieldPath, stale.PreviousValue, recordUndo, issues)) continue;
                graph.RemovePrefabProjectionState(stale.OwnerNodeId, stale.FieldPath);
            }
            foreach (GdoGraphEdgeRecord edge in active)
            {
                GdoGraphNodeRecord owner = graph.FindNode(edge.InputNodeId);
                string propertyName = FieldName(edge.InputPortPath);
                var serialized = new SerializedObject(owner.Asset);
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (!graph.PrefabProjectionStates.Any(state => state.OwnerNodeId == owner.Id &&
                    state.FieldPath == edge.InputPortPath))
                    graph.AddPrefabProjectionState(GdoGraphPrefabProjectionState.Create(owner.Id,
                        edge.InputPortPath, property.objectReferenceValue as GameObject));
                if (recordUndo) Undo.RecordObject(owner.Asset, "Assign graph prefab");
                property.objectReferenceValue = resolved[edge.OutputNodeId];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(owner.Asset);
            }
            EditorUtility.SetDirty(graph);
            diagnostics = issues;
            return issues.Count == 0;
        }

        private static void ValidateTarget(GdoBundleGraphAsset graph, string ownerId, string fieldPath,
            ICollection<GdoGraphDiagnostic> issues)
        {
            GdoGraphNodeRecord owner = graph.FindNode(ownerId);
            if (owner == null || owner.IsBroken)
            {
                issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Error, "InvalidPrefabTarget",
                    ownerId, string.Empty, fieldPath, "Prefab edge target is missing or broken."));
                return;
            }
            var serialized = new SerializedObject(owner.Asset);
            SerializedProperty property = serialized.FindProperty(FieldName(fieldPath));
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Error, "InvalidPrefabTarget",
                    owner.Id, AssetDatabase.GetAssetPath(owner.Asset), fieldPath,
                    "Prefab target is not a serialized object-reference field."));
        }

        private static bool TrySet(GdoBundleGraphAsset graph, string ownerId, string fieldPath,
            GameObject value, bool recordUndo, ICollection<GdoGraphDiagnostic> issues)
        {
            GdoGraphNodeRecord owner = graph.FindNode(ownerId);
            if (owner == null || owner.IsBroken) return false;
            var serialized = new SerializedObject(owner.Asset);
            SerializedProperty property = serialized.FindProperty(FieldName(fieldPath));
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return false;
            if (recordUndo) Undo.RecordObject(owner.Asset, "Restore prefab assignment");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner.Asset);
            return true;
        }

        private static string FieldName(string path)
        {
            int separator = path.LastIndexOf('.');
            return separator < 0 ? path : path.Substring(separator + 1);
        }
    }
}
