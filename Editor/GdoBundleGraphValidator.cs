using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class GdoBundleGraphValidator
    {
        public static IReadOnlyList<GdoGraphDiagnostic> Validate(GdoBundleGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var issues = new List<GdoGraphDiagnostic>();
            foreach (GdoGraphDiagnostic layout in GdoBundleProjectRepository.ValidateLayout(graph))
                issues.Add(new GdoGraphDiagnostic(layout.Severity, "InvalidProjectLayout", layout.ElementId,
                    layout.AssetPath, layout.PropertyPath, layout.Message));
            GdoGraphNodeRecord[] locals = graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local).ToArray();
            foreach (GdoGraphNodeRecord broken in locals.Where(node => node.IsBroken))
                Add(issues, ValidationSeverity.Fatal, "BrokenMember", broken, string.Empty,
                    "The member asset is missing. Its GUID and cached metadata were retained for repair.");

            GdoGraphNodeRecord[] resolved = locals.Where(node => !node.IsBroken).ToArray();
            ValidateIdentities(graph, resolved, issues);
            foreach (GdoGraphNodeRecord zero in resolved.Where(node => node.RuntimeId == 0))
                Add(issues, ValidationSeverity.Error, "ZeroId", zero, "ID", "GDO ID must be non-zero.");
            foreach (GdoGraphNodeRecord external in graph.Nodes.Where(node =>
                node.Kind == GdoGraphNodeKind.External && string.IsNullOrWhiteSpace(node.ExternalProviderKey)))
                Add(issues, ValidationSeverity.Fatal, "MissingExternalProvider", external,
                    "providerKey", "External GDO provider key is required.");
            foreach (GdoGraphPrefabRecord prefab in graph.Prefabs)
                issues.AddRange(GdoGraphPrefabResolver.Validate(graph, prefab));
            foreach (IGrouping<int, GdoGraphNodeRecord> duplicate in locals.Where(node => node.RuntimeId != 0)
                .GroupBy(node => node.RuntimeId).Where(group => group.Count() > 1))
                foreach (GdoGraphNodeRecord node in duplicate)
                    Add(issues, ValidationSeverity.Error, "DuplicateId", node, "ID", "GDO ID " + duplicate.Key + " is duplicated.");

            foreach (GdoGraphNodeRecord node in resolved)
            {
                string path = AssetDatabase.GetAssetPath(node.Asset);
                string graphPath = AssetDatabase.GetAssetPath(graph);
                if (string.IsNullOrEmpty(graphPath) ||
                    !path.StartsWith(GdoBundleProjectRepository.GdoFolder(graph) + "/", StringComparison.Ordinal))
                    Add(issues, ValidationSeverity.Error, "MemberOutsideProject", node, string.Empty,
                        "Local member must be inside this project's authoritative GDOs folder.");
                if (GdoBundleGraphRepository.FindGraphsContaining(node.Asset).Count > 1)
                    Add(issues, ValidationSeverity.Error, "ConflictingMembership", node, string.Empty,
                        "GDO belongs to more than one bundle graph.");
                GdoTypeDescriptorView descriptor;
                try { descriptor = GdoDescriptorCatalog.ForTarget(node.Asset); }
                catch (Exception exception)
                {
                    Add(issues, ValidationSeverity.Fatal, "UnavailableDescriptor", node, string.Empty, exception.Message);
                    continue;
                }
                if (!descriptor.IsAvailable)
                    Add(issues, ValidationSeverity.Fatal, "UnavailableDescriptor", node, string.Empty,
                        "The generated descriptor is unavailable for this GDO type.");
                foreach (GdoGraphPortDescriptor port in GdoGraphPortCatalog.ForDescriptor(descriptor)
                    .Where(port => !port.UsesRelationshipNode))
                {
                    int connections = graph.Edges.Count(edge => edge.InputNodeId == node.Id && edge.InputPortPath == port.Path);
                    if (port.Cardinality == GdoGraphPortCardinality.Single && connections > 1)
                        Add(issues, ValidationSeverity.Error, "ExcessConnection", node, port.Path,
                            "Single-reference port has more than one connection.");
                    if (port.IsRequired && port.CollectionKind == GdoGraphCollectionKind.None && connections == 0)
                        Add(issues, ValidationSeverity.Error, "MissingRequiredConnection", node, port.Path,
                            "Required relationship port has no connection.");
                }
                foreach (GdoGraphPortDescriptor port in GdoUnityObjectPortCatalog.ForDescriptor(descriptor))
                {
                    int connections = graph.Edges.Count(edge => edge.InputNodeId == node.Id &&
                        edge.InputPortPath == port.Path);
                    if (connections > 1)
                        Add(issues, ValidationSeverity.Error, "ExcessConnection", node, port.Path,
                            "GameObject field has more than one prefab connection.");
                }
            }

            GdoGraphProjectionResult projection = GdoGraphRelationshipProjector.Project(graph);
            foreach (GdoGraphProjectionDiagnostic diagnostic in projection.Diagnostics)
            {
                GdoGraphNodeRecord node = graph.Nodes.FirstOrDefault(value => value.Id == diagnostic.OwnerNodeId);
                Add(issues, ValidationSeverity.Error, diagnostic.Category, node, diagnostic.Path, diagnostic.Message,
                    diagnostic.OwnerNodeId);
            }
            foreach (GdoGraphProjectedField field in projection.Fields)
            {
                GdoGraphNodeRecord node = graph.Nodes.FirstOrDefault(value => value.Id == field.OwnerNodeId);
                if (node == null || node.IsBroken) continue;
                GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(node.Asset);
                if (companion != null && companion.TryGetField(field.DeclaringType, field.FieldName, out GdoOverlayField existing) &&
                    !Equivalent(existing.Value, field.Value))
                    Add(issues, ValidationSeverity.Error, "ProjectionDrift", node, field.FieldKey,
                        "Companion relationship value differs from the graph and will be replaced when the graph is applied.");
            }
            return issues.OrderBy(issue => issue.SortKey, StringComparer.Ordinal)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateIdentities(GdoBundleGraphAsset graph, GdoGraphNodeRecord[] resolved,
            ICollection<GdoGraphDiagnostic> issues)
        {
            foreach (IGrouping<string, GdoGraphNodeRecord> duplicate in resolved
                .GroupBy(node => node.Asset.name.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                foreach (GdoGraphNodeRecord node in duplicate)
                    Add(issues, ValidationSeverity.Error, "DuplicateLocalName", node, "name",
                        "Local GDO name is duplicated (ignoring case): " + duplicate.Key + ".");

            // Layout validation already diagnoses an invalid namespace; keep other member checks running.
            bool validNamespace = true;
            try { GdoProjectIdentity.ValidateName(graph.BundleName, nameof(graph.BundleName)); }
            catch (ArgumentException) { validNamespace = false; }

            foreach (GdoGraphNodeRecord node in resolved)
            {
                try { GdoProjectIdentity.ValidateName(node.Asset.name, "name"); }
                catch (ArgumentException exception)
                {
                    Add(issues, ValidationSeverity.Error, "InvalidProjectLayout", node, "name", exception.Message);
                    continue;
                }
                if (!validNamespace) continue;
            }

            var identities = resolved.Select(node => new KeyValuePair<int, GdoGraphNodeRecord>(node.RuntimeId, node)).ToList();
            identities.AddRange(graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.External)
                .Select(node => new KeyValuePair<int, GdoGraphNodeRecord>(node.ExternalId, node)));
            foreach (IGrouping<int, KeyValuePair<int, GdoGraphNodeRecord>> collision in identities.GroupBy(pair => pair.Key)
                .Where(group => group.Count() > 1 && group.Any(pair => pair.Value.Kind == GdoGraphNodeKind.Local)))
                foreach (KeyValuePair<int, GdoGraphNodeRecord> member in collision)
                    Add(issues, ValidationSeverity.Error, "CalculatedIdCollision", member.Value, "ID",
                        "Stored GDO ID " + collision.Key + " collides with another local or known external GDO.");
        }

        private static bool Equivalent(GdoOverlayValue left, GdoOverlayValue right) =>
            JsonUtility.ToJson(GdoOverlaySerializedGraph.Encode(left)) == JsonUtility.ToJson(GdoOverlaySerializedGraph.Encode(right));

        private static void Add(ICollection<GdoGraphDiagnostic> issues, ValidationSeverity severity, string category,
            GdoGraphNodeRecord node, string propertyPath, string message, string fallbackElementId = null)
        {
            issues.Add(new GdoGraphDiagnostic(severity, category, node?.Id ?? fallbackElementId,
                node?.Asset == null ? string.Empty : AssetDatabase.GetAssetPath(node.Asset), propertyPath, message));
        }
    }
}
