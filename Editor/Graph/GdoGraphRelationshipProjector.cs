using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Generated;
using KitchenLib.Studio.Editor.Api.Build;
using KitchenLib.Studio.Editor.Api.Authoring;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphRelationshipProjector
    {
        public static GdoGraphProjectionResult Project(GdoBundleGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var fields = new List<GdoGraphProjectedField>();
            var diagnostics = new List<GdoGraphProjectionDiagnostic>();
            var customIssues = new List<GdoGraphDiagnostic>();
            var context = new ProjectionContext(graph, StudioCustomGraphPipeline.Project(graph, customIssues));
            foreach (GdoGraphDiagnostic issue in customIssues)
                diagnostics.Add(new GdoGraphProjectionDiagnostic(issue.Category, issue.ElementId, issue.PropertyPath, issue.Message, issue.Severity));
            foreach (GdoGraphNodeRecord owner in graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local && !node.IsBroken))
            {
                GdoTypeDescriptorView descriptor;
                try { descriptor = GdoDescriptorCatalog.ForTarget(owner.Asset); }
                catch (Exception exception)
                {
                    diagnostics.Add(new GdoGraphProjectionDiagnostic("UnavailableDescriptor", owner.Id, string.Empty, exception.Message));
                    continue;
                }
                foreach (GdoInspectorField field in GdoInspectorModel.Build(descriptor)
                    .Section(GdoInspectorFieldKind.Relationship).Fields)
                {
                    string fieldKey = field.Key;
                    try
                    {
                        GdoOverlayValue value = ProjectField(context, owner,
                            field.Descriptor.Value, fieldKey, diagnostics);
                        if (value != null)
                            fields.Add(new GdoGraphProjectedField(owner.Id, field.DeclaringTypeName, field.Name, value));
                    }
                    catch (ArgumentException exception)
                    {
                        diagnostics.Add(new GdoGraphProjectionDiagnostic("InvalidRelationshipValue", owner.Id, fieldKey, exception.Message));
                    }
                }
            }
            return new GdoGraphProjectionResult(fields, diagnostics);
        }

        private static GdoOverlayValue ProjectField(ProjectionContext graph,
            GdoGraphNodeRecord owner,
            GdoValueDescriptorData descriptor, string fieldKey, ICollection<GdoGraphProjectionDiagnostic> diagnostics)
        {
            GdoValueDescriptorData unwrapped = descriptor.Kind == "Nullable" ? descriptor.Element : descriptor;
            if (unwrapped.Kind == "GdoReference")
                return ResolveSingle(graph, owner.Id, fieldKey,
                    unwrapped.ExpectedTypeName, owner.Id, diagnostics);
            if (unwrapped.Kind == "List" || unwrapped.Kind == "Array" || unwrapped.Kind == "Set")
            {
                string elementPath = fieldKey + "[]";
                GdoValueDescriptorData element = unwrapped.Element;
                if (element.Kind == "GdoReference" || (element.Kind == "Nullable" && element.Element.Kind == "GdoReference"))
                {
                    string expected = element.Kind == "GdoReference" ? element.ExpectedTypeName : element.Element.ExpectedTypeName;
                    List<GdoOverlayValue> values = EdgesInto(graph, owner.Id, elementPath)
                        .Select(edge => Resolve(graph, edge, expected, owner.Id, elementPath, diagnostics))
                        .Where(value => value != null).ToList();
                    return Collection(unwrapped.Kind, values, owner.Id, fieldKey, diagnostics);
                }
                GdoGraphRelationshipRecord[] relationships = graph.Relationships
                    .Where(value => value.OwnerNodeId == owner.Id && value.FieldPath == elementPath)
                    .OrderBy(value => value.Order).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
                var projected = new List<GdoOverlayValue>();
                foreach (GdoGraphRelationshipRecord relationship in relationships)
                {
                    GdoOverlayValue template = relationship.Value ?? GdoOverlayValueEditor.CreateDefault(element);
                    projected.Add(ProjectNested(graph, relationship.Id, element, template, elementPath, owner.Id, diagnostics));
                }
                return Collection(unwrapped.Kind, projected, owner.Id, fieldKey, diagnostics);
            }
            if (unwrapped.Kind == "Struct")
            {
                GdoGraphRelationshipRecord relationship = graph.Relationships.FirstOrDefault(value =>
                    value.OwnerNodeId == owner.Id && value.FieldPath == fieldKey);
                GdoOverlayValue template = relationship?.Value ?? GdoOverlayValueEditor.CreateDefault(unwrapped);
                return ProjectNested(graph, relationship?.Id ?? owner.Id, unwrapped, template, fieldKey, owner.Id, diagnostics);
            }
            return GdoOverlayValueEditor.CreateDefault(descriptor);
        }

        private static GdoOverlayValue ProjectNested(ProjectionContext graph, string contextId,
            GdoValueDescriptorData descriptor, GdoOverlayValue template, string path, string ownerId,
            ICollection<GdoGraphProjectionDiagnostic> diagnostics)
        {
            if (descriptor.Kind == "GdoReference")
                return ResolveSingle(graph, contextId, path, descriptor.ExpectedTypeName, ownerId, diagnostics);
            if (descriptor.Kind == "Nullable" && descriptor.Element.Kind == "GdoReference")
                return ResolveSingle(graph, contextId, path, descriptor.Element.ExpectedTypeName, ownerId, diagnostics);
            if (descriptor.Kind == "Nullable")
                return template != null && template.Kind == GdoOverlayValueKind.Null ? template :
                    ProjectNested(graph, contextId, descriptor.Element, template, path, ownerId, diagnostics);
            if (descriptor.Kind == "List" || descriptor.Kind == "Array" || descriptor.Kind == "Set")
            {
                GdoValueDescriptorData element = descriptor.Element;
                if (element.Kind == "GdoReference" || (element.Kind == "Nullable" && element.Element.Kind == "GdoReference"))
                {
                    string expected = element.Kind == "GdoReference" ? element.ExpectedTypeName : element.Element.ExpectedTypeName;
                    var values = new List<GdoOverlayValue>();
                    for (int index = 0; index < (template?.Values.Count ?? 0); index++)
                    {
                        string indexedPath = path + "[" + index + "]";
                        GdoOverlayValue projectedValue = ResolveSingle(graph, contextId, indexedPath, expected, ownerId, diagnostics);
                        if (projectedValue != null) values.Add(projectedValue);
                    }
                    return Collection(descriptor.Kind, values, ownerId, path, diagnostics);
                }
                List<GdoOverlayValue> projected = (template?.Values ?? Array.Empty<GdoOverlayValue>())
                    .Select((value, index) => ProjectNested(graph, contextId, element, value,
                        path + "[" + index + "]", ownerId, diagnostics)).ToList();
                return Collection(descriptor.Kind, projected, ownerId, path, diagnostics);
            }
            if (descriptor.Kind != "Struct") return template;
            return GdoOverlayValue.Struct(descriptor.Members.Select(member =>
            {
                GdoOverlayMember existing = template?.Members.FirstOrDefault(value => value.Name == member.Name);
                GdoOverlayValue current = existing?.Value ?? GdoOverlayValueEditor.CreateDefault(member.Value);
                return new GdoOverlayMember(member.Name, GdoInspectorModel.ContainsGdoReference(member.Value)
                    ? ProjectNested(graph, contextId, member.Value, current, path + "." + member.Name, ownerId, diagnostics)
                    : current);
            }));
        }

        private static GdoOverlayValue ResolveSingle(ProjectionContext graph, string inputNodeId, string inputPath,
            string expectedType, string ownerId, ICollection<GdoGraphProjectionDiagnostic> diagnostics)
        {
            GdoGraphEdgeRecord[] edges = EdgesInto(graph, inputNodeId, inputPath).ToArray();
            if (edges.Length > 1)
            {
                diagnostics.Add(new GdoGraphProjectionDiagnostic("MultipleSingleValueConnections",
                    ownerId, inputPath, "A single reference cannot receive multiple sources."));
                return null;
            }
            return edges.Length == 0 ? GdoOverlayValue.Null() :
                Resolve(graph, edges[0], expectedType, ownerId, inputPath, diagnostics);
        }

        private static GdoOverlayValue Resolve(ProjectionContext graph, GdoGraphEdgeRecord edge, string expectedType,
            string ownerId, string path, ICollection<GdoGraphProjectionDiagnostic> diagnostics)
        {
            string targetId = edge.OutputNodeId;
            if (graph.Graph.FindCustomNode(edge.OutputNodeId) != null)
            {
                if (!graph.Projections.TryGetValue(edge.OutputNodeId, out StudioBuildProjection projection) ||
                    !projection.Values.TryGetValue(edge.OutputPortPath, out StudioAuthoringValue value) ||
                    value.Kind != StudioAuthoringValueKind.Reference)
                {
                    diagnostics.Add(new GdoGraphProjectionDiagnostic("UnresolvedCustomReference", ownerId, path,
                        "Custom output has no valid projected GDO reference."));
                    return null;
                }
                targetId = value.AsReference();
            }
            GdoGraphNodeRecord target = graph.Nodes.FirstOrDefault(node => node.Id == targetId);
            if (target == null || target.IsBroken)
            {
                diagnostics.Add(new GdoGraphProjectionDiagnostic("UnresolvedReference", ownerId, path,
                    "Referenced graph node is missing or broken."));
                return null;
            }
            string actualType = target.Kind == GdoGraphNodeKind.Local ? target.RuntimeTypeName : target.ExpectedType;
            if (!GdoGraphConnectionTypeResolver.IsAssignable(actualType, expectedType))
            {
                diagnostics.Add(new GdoGraphProjectionDiagnostic("IncompatibleReference", ownerId, path,
                    "Projected GDO reference has an incompatible type."));
                return null;
            }
            try
            {
                GdoReferenceToken token = target.Kind == GdoGraphNodeKind.Local
                    ? GdoReferenceToken.ForAsset(target.Asset, expectedType)
                    : GdoReferenceToken.Create(target.ExternalProviderKey, target.ExternalId, expectedType);
                return GdoOverlayValue.Reference(token);
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new GdoGraphProjectionDiagnostic("IncompatibleReference", ownerId, path, exception.Message));
                return null;
            }
        }

        private static IEnumerable<GdoGraphEdgeRecord> EdgesInto(ProjectionContext graph,
            string nodeId, string path) =>
            graph.Edges.Where(edge => edge.InputNodeId == nodeId && edge.InputPortPath == path)
                .OrderBy(edge => edge.Order)
                .ThenBy(edge => edge.OutputNodeId, StringComparer.Ordinal);

        private static GdoOverlayValue Collection(string kind, IList<GdoOverlayValue> values, string ownerId,
            string path, ICollection<GdoGraphProjectionDiagnostic> diagnostics)
        {
            if (kind != "Set") return GdoOverlayValue.List(values);
            try { return GdoOverlayValue.Set(values); }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new GdoGraphProjectionDiagnostic("DuplicateSetValue", ownerId, path, exception.Message));
                return null;
            }
        }
        private sealed class ProjectionContext
        {
            internal ProjectionContext(GdoBundleGraphAsset graph, IReadOnlyDictionary<string, StudioBuildProjection> projections)
            { Graph = graph; Projections = projections; }
            internal GdoBundleGraphAsset Graph { get; }
            internal IReadOnlyDictionary<string, StudioBuildProjection> Projections { get; }
            internal IReadOnlyList<GdoGraphNodeRecord> Nodes => Graph.Nodes;
            internal IReadOnlyList<GdoGraphRelationshipRecord> Relationships => Graph.Relationships;
            internal IReadOnlyList<GdoGraphEdgeRecord> Edges => Graph.Edges;
        }
    }
}
