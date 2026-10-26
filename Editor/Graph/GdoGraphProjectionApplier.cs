using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphProjectionApplier
    {
        public static bool TryApply(GdoBundleGraphAsset graph, out IReadOnlyList<GdoGraphDiagnostic> diagnostics,
            bool allowProjectionDrift = true, bool allowIncompleteRelationships = false, bool recordUndo = true)
        {
            var journal = new GdoGraphCompanionJournal(graph);
            bool applied;
            try { applied = ApplyCore(graph, out diagnostics, allowProjectionDrift, allowIncompleteRelationships, recordUndo); }
            catch (Exception exception)
            {
                journal.Restore();
                diagnostics = new[] { new GdoGraphDiagnostic(ValidationSeverity.Fatal, "ProjectionApplyFailed",
                    string.Empty, string.Empty, string.Empty, exception.Message) };
                return false;
            }
            if (!applied) journal.Restore();
            return applied;
        }

        private static bool ApplyCore(GdoBundleGraphAsset graph, out IReadOnlyList<GdoGraphDiagnostic> diagnostics,
            bool allowProjectionDrift, bool allowIncompleteRelationships, bool recordUndo)
        {
            List<GdoGraphDiagnostic> issues = GdoBundleGraphValidator.Validate(graph).ToList();
            if (issues.Any(issue => Blocks(issue, allowProjectionDrift, allowIncompleteRelationships)))
            {
                diagnostics = issues;
                return false;
            }
            GdoGraphProjectionResult projection = GdoGraphRelationshipProjector.Project(graph);
            // Projection is evaluated again after validation. An extension can fail on this pass;
            // never apply its partial fields merely because the preceding evaluation succeeded.
            foreach (GdoGraphProjectionDiagnostic diagnostic in projection.Diagnostics)
                issues.Add(new GdoGraphDiagnostic(diagnostic.Severity, diagnostic.Category,
                    diagnostic.OwnerNodeId, string.Empty, diagnostic.Path, diagnostic.Message));
            foreach (GdoGraphProjectedField field in projection.Fields)
                if (!GdoOverlayLimits.TryValidate(new[] { field.Value }, out GdoOverlayPersistenceDiagnostic limit))
                    issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Fatal, limit.Category, field.OwnerNodeId,
                        string.Empty, field.FieldKey + "." + limit.Path, limit.Message));
            if (issues.Any(issue => Blocks(issue, allowProjectionDrift, allowIncompleteRelationships)))
            {
                diagnostics = issues.OrderBy(issue => issue.SortKey, StringComparer.Ordinal).ToArray();
                return false;
            }

            if (!GdoGraphPrefabProjection.TryApply(graph, out IReadOnlyList<GdoGraphDiagnostic> prefabIssues,
                recordUndo))
            {
                issues.AddRange(prefabIssues);
                diagnostics = issues.OrderBy(issue => issue.SortKey, StringComparer.Ordinal).ToArray();
                return false;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply GDO bundle graph relationships");
            try
            {
                foreach (IGrouping<string, GdoGraphProjectedField> ownerFields in projection.Fields.GroupBy(field => field.OwnerNodeId))
                {
                    GdoGraphNodeRecord owner = graph.Nodes.Single(node => node.Id == ownerFields.Key);
                    GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(owner.Asset) ??
                        GdoOverlayAuthoringRepository.GetOrCreate(owner.Asset);
                    if (recordUndo) Undo.RegisterCompleteObjectUndo(companion, "Apply GDO graph relationships");
                    foreach (GdoGraphProjectedField field in ownerFields)
                        companion.UpsertField(field.DeclaringType, field.FieldName, field.Value);
                    EditorUtility.SetDirty(companion);
                }
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
                diagnostics = issues.Where(issue => issue.Category != "ProjectionDrift")
                    .OrderBy(issue => issue.SortKey, StringComparer.Ordinal).ToArray();
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Fatal, "ProjectionApplyFailed", string.Empty,
                    string.Empty, string.Empty, exception.Message));
                diagnostics = issues.OrderBy(issue => issue.SortKey, StringComparer.Ordinal).ToArray();
                return false;
            }
        }

        private static bool Blocks(GdoGraphDiagnostic issue, bool allowProjectionDrift, bool allowIncompleteRelationships)
        {
            if (issue.Severity != ValidationSeverity.Error && issue.Severity != ValidationSeverity.Fatal) return false;
            if (allowProjectionDrift && issue.Category == "ProjectionDrift") return false;
            if (allowIncompleteRelationships && issue.Category == "MissingRequiredConnection") return false;
            return true;
        }
    }
}
