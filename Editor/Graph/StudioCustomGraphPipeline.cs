using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor.Api;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Build;
using KitchenLib.Studio.Editor.Api.Graph;
using KitchenLib.Studio.Editor.Api.Validation;
using KitchenLib.Studio.Editor.Validation;
using UnityEditor;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class StudioCustomGraphPipeline
    {
        internal const int MaximumOutputs = 256;
        internal const int MaximumTextLength = 65536;
        internal const int MaximumFindings = 256;

        internal static StudioGraphPortDefinition Port(GdoBundleGraphAsset graph, string id, string key) =>
            StudioGraphNodeRegistry.Find(graph.FindCustomNode(id)?.TypeId)?.Ports.FirstOrDefault(port => port.Key == key);

        internal static bool IsCustomEdge(GdoBundleGraphAsset graph, string output, string input) =>
            graph.FindCustomNode(output) != null || graph.FindCustomNode(input) != null;

        internal static bool CanConnect(GdoBundleGraphAsset graph, string output, string outputKey,
            string input, string inputKey, bool checkCapacity = true)
        {
            if (output == input) return false;
            bool customOutput = graph.FindCustomNode(output) != null;
            bool customInput = graph.FindCustomNode(input) != null;
            StudioGraphPortDefinition from = Port(graph, output, outputKey);
            StudioGraphPortDefinition to = Port(graph, input, inputKey);
            if (customOutput && (from == null || from.Direction != StudioGraphPortDirection.Output)) return false;
            if (customInput && (to == null || to.Direction != StudioGraphPortDirection.Input)) return false;
            if (customOutput && customInput)
            {
                if (!string.Equals(from.SemanticTypeId, to.SemanticTypeId, StringComparison.Ordinal)) return false;
            }
            else if (customOutput)
            {
                string expected = GdoGraphConnectionTypeResolver.ExpectedInputType(graph, input, inputKey);
                if (from.SemanticTypeId != KitchenLibStudioApi.GdoReferenceSemanticTypeId ||
                    string.IsNullOrEmpty(expected) || expected == "UnityEngine.GameObject") return false;
            }
            else if (customInput)
            {
                GdoGraphNodeRecord source = graph.FindNode(output);
                if (source == null || source.IsBroken || outputKey != "Value" ||
                    to.SemanticTypeId != KitchenLibStudioApi.GdoReferenceSemanticTypeId) return false;
            }
            else return false;
            if (!checkCapacity) return true;
            if (from?.Cardinality == StudioGraphPortCardinality.Single &&
                graph.Edges.Any(edge => edge.OutputNodeId == output && edge.OutputPortPath == outputKey)) return false;
            if (to?.Cardinality == StudioGraphPortCardinality.Single &&
                graph.Edges.Any(edge => edge.InputNodeId == input && edge.InputPortPath == inputKey)) return false;
            if (!customInput && GdoGraphConnectionController.InputPort(graph, input, inputKey)?.Cardinality == GdoGraphPortCardinality.Single &&
                graph.Edges.Any(edge => edge.InputNodeId == input && edge.InputPortPath == inputKey)) return false;
            return !graph.Edges.Any(edge => edge.OutputNodeId == output && edge.OutputPortPath == outputKey &&
                edge.InputNodeId == input && edge.InputPortPath == inputKey);
        }

        internal static IReadOnlyList<GdoGraphDiagnostic> Validate(GdoBundleGraphAsset graph)
        {
            var issues = new List<GdoGraphDiagnostic>();
            if (graph.CustomNodes.Count == 0) return issues;
            StudioValidationContext context = Context(graph, issues);
            foreach (StudioCustomGraphNodeRecord record in graph.CustomNodes)
            {
                StudioGraphNodeDefinition definition = StudioGraphNodeRegistry.Find(record.TypeId);
                if (definition == null)
                {
                    Error(issues, "MissingCustomNodeDefinition", record.Id, string.Empty,
                        "Custom node definition is unavailable: " + record.TypeId + ". Stored data and connections were retained.");
                    continue;
                }
                StudioGraphNodeData data = context.Nodes.FirstOrDefault(node => node.NodeId == record.Id);
                if (data == null) continue;
                foreach (StudioGraphFieldDefinition field in definition.Fields)
                {
                    bool found = data.Values.TryGetValue(field.Key, out StudioAuthoringValue value);
                    if ((!found || value.Kind == StudioAuthoringValueKind.Null) && field.Required)
                        Error(issues, "MissingCustomField", record.Id, field.Key, "Required field has no authored value.");
                    else if (found && value.Kind != StudioAuthoringValueKind.Null && value.Kind != field.ValueKind)
                        Error(issues, "CustomFieldKindMismatch", record.Id, field.Key, "Stored value does not match the current field kind.");
                }
                foreach (StudioGraphPortDefinition port in definition.Ports.Where(port => port.Cardinality == StudioGraphPortCardinality.Single))
                    if (data.Connections.Count(connection => connection.PortKey == port.Key) > 1)
                        Error(issues, "CustomPortCardinality", record.Id, port.Key, "Single port has more than one connection.");
                if (definition.Validator == null) continue;
                try { AddFindings(issues, record.Id, definition.Validator.Validate(data, context)); }
                catch (Exception) { Error(issues, "CustomValidatorFailed", record.Id, string.Empty, "Custom node validator failed."); }
            }
            foreach (GdoGraphEdgeRecord edge in graph.Edges.Where(edge => IsCustomEdge(graph, edge.OutputNodeId, edge.InputNodeId)))
                if (!CanConnect(graph, edge.OutputNodeId, edge.OutputPortPath, edge.InputNodeId, edge.InputPortPath, false))
                    Error(issues, "InvalidCustomConnection", graph.FindCustomNode(edge.OutputNodeId)?.Id ?? edge.InputNodeId,
                        edge.Id, "Connection has an unavailable endpoint, port, direction or incompatible semantic type. The connection was retained.");
            return issues;
        }

        internal static IReadOnlyDictionary<string, StudioBuildProjection> Project(GdoBundleGraphAsset graph,
            ICollection<GdoGraphDiagnostic> issues)
        {
            var projections = new Dictionary<string, StudioBuildProjection>(StringComparer.Ordinal);
            if (graph.CustomNodes.Count == 0) return projections;
            StudioValidationContext context = Context(graph, issues);
            foreach (StudioCustomGraphNodeRecord record in graph.CustomNodes)
            {
                StudioGraphNodeDefinition definition = StudioGraphNodeRegistry.Find(record.TypeId);
                StudioGraphNodeData data = context.Nodes.FirstOrDefault(node => node.NodeId == record.Id);
                if (definition == null)
                {
                    Error(issues, "MissingCustomNodeDefinition", record.Id, string.Empty, "Custom node definition is unavailable: " + record.TypeId + ".");
                    continue;
                }
                if (data == null) continue;
                try
                {
                    StudioBuildProjection result = definition.Projector.Project(data, context);
                    if (result == null) throw new InvalidOperationException();
                    AddFindings(issues, record.Id, result.Findings);
                    if (!result.IsSuccess) continue;
                    if (result.Values.Count > MaximumOutputs) throw new InvalidOperationException();
                    foreach (var pair in result.Values)
                    {
                        StudioGraphPortDefinition port = definition.Ports.FirstOrDefault(item => item.Key == pair.Key);
                        if (port == null || port.Direction != StudioGraphPortDirection.Output ||
                            (pair.Value.Kind == StudioAuthoringValueKind.String && pair.Value.AsString().Length > MaximumTextLength) ||
                            (pair.Value.Kind == StudioAuthoringValueKind.Reference && pair.Value.AsReference().Length > MaximumTextLength))
                            throw new InvalidOperationException();
                        if (port.SemanticTypeId == KitchenLibStudioApi.GdoReferenceSemanticTypeId)
                        {
                            GdoGraphNodeRecord target = pair.Value.Kind == StudioAuthoringValueKind.Reference
                                ? graph.FindNode(pair.Value.AsReference()) : null;
                            if (target == null || target.IsBroken) throw new InvalidOperationException();
                        }
                    }
                    if (graph.Edges.Any(edge => edge.OutputNodeId == record.Id && !result.Values.ContainsKey(edge.OutputPortPath)))
                        throw new InvalidOperationException();
                    projections.Add(record.Id, result);
                }
                catch (Exception) { Error(issues, "CustomProjectorFailed", record.Id, string.Empty,
                    "Custom node projector failed or returned invalid, unresolved or oversized output."); }
            }
            return projections;
        }

        private static StudioValidationContext Context(GdoBundleGraphAsset graph, ICollection<GdoGraphDiagnostic> issues)
        {
            var nodes = new SortedDictionary<string, StudioGraphNodeData>(StringComparer.Ordinal);
            foreach (GdoGraphNodeRecord node in graph.Nodes)
                try
                {
                    nodes.Add(node.Id, new StudioGraphNodeData(node.Id, KitchenLibStudioApi.GdoReferenceSemanticTypeId,
                        new[] { new KeyValuePair<string, StudioAuthoringValue>("name", StudioAuthoringValue.FromString(node.CachedName ?? string.Empty)) },
                        StudioCustomGraphNodeRecord.Connections(graph, node.Id)));
                }
                catch (Exception) { Error(issues, "InvalidCustomContext", node.Id, string.Empty,
                    "An authored GDO has invalid identifiers or keyed connection endpoints. Stored data was retained."); }
            foreach (StudioCustomGraphNodeRecord record in graph.CustomNodes)
                try { nodes.Add(record.Id, record.Snapshot(graph)); }
                catch (Exception) { Error(issues, "InvalidCustomNodeData", record.Id, string.Empty,
                    "Custom node contains invalid identifiers, duplicate keys or unknown value encodings. Raw data was retained."); }
            string productId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            return new StudioValidationContext(string.IsNullOrEmpty(productId) ? "unsaved-product" : productId, nodes.Values);
        }

        private static void AddFindings(ICollection<GdoGraphDiagnostic> issues, string nodeId,
            IReadOnlyList<StudioValidationFinding> findings)
        {
            if (findings == null || findings.Count > MaximumFindings) throw new InvalidOperationException();
            var pending = new List<GdoGraphDiagnostic>();
            foreach (StudioValidationFinding finding in findings)
            {
                if (finding == null || finding.Code.Length > MaximumTextLength || finding.Message.Length > MaximumTextLength ||
                    (finding.NodeId?.Length ?? 0) > MaximumTextLength || (finding.FieldKey?.Length ?? 0) > MaximumTextLength ||
                    (finding.PortKey?.Length ?? 0) > MaximumTextLength)
                    throw new InvalidOperationException();
                ValidationSeverity severity = finding.Severity == StudioValidationSeverity.Error ? ValidationSeverity.Error :
                    finding.Severity == StudioValidationSeverity.Warning ? ValidationSeverity.Warning : ValidationSeverity.Info;
                // Extension codes cannot impersonate built-in recoverable diagnostic categories.
                pending.Add(new GdoGraphDiagnostic(severity, "CustomFinding:" + finding.Code, finding.NodeId ?? nodeId, string.Empty,
                    finding.FieldKey ?? finding.PortKey ?? string.Empty, finding.Message));
            }
            foreach (GdoGraphDiagnostic issue in pending) issues.Add(issue);
        }

        private static void Error(ICollection<GdoGraphDiagnostic> issues, string category, string nodeId,
            string path, string message) => issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Error,
                category, nodeId, string.Empty, path, message));
    }
}
