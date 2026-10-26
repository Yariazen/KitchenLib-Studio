using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenData;
using KitchenLib.Studio.Generated;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoRelationshipChoice
    {
        internal GdoRelationshipChoice(GdoGraphPortDescriptor port)
        {
            FieldPath = port.RelationshipPath;
            string name = port.FieldKey.Substring(port.FieldKey.LastIndexOf('.') + 1);
            Label = ObjectNames.NicifyVariableName(name);
            GdoValueDescriptorData root = port.RootValue;
            while (root.Kind == "Nullable") root = root.Element;
            IsRepeated = root.Kind == "List" || root.Kind == "Array" || root.Kind == "Set";
            ValueDescriptor = IsRepeated ? root.Element : root;
        }

        public string Label { get; }
        public string FieldPath { get; }
        public bool IsRepeated { get; }
        internal GdoValueDescriptorData ValueDescriptor { get; }
    }

    internal sealed class GdoGraphConnectionController
    {
        private readonly GdoBundleGraphAsset _graph;
        public GdoGraphConnectionController(GdoBundleGraphAsset graph) => _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        public IReadOnlyList<GdoRelationshipChoice> RelationshipChoices(string ownerNodeId)
        {
            GdoGraphNodeRecord owner = _graph.Nodes.FirstOrDefault(node => node.Id == ownerNodeId);
            if (owner == null || owner.Kind != GdoGraphNodeKind.Local || owner.IsBroken)
                throw new ArgumentException("Relationship owner is missing or broken.", nameof(ownerNodeId));
            return GdoGraphPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForTarget(owner.Asset))
                .Where(port => port.UsesRelationshipNode)
                .GroupBy(port => port.RelationshipPath, StringComparer.Ordinal)
                .Select(group => new GdoRelationshipChoice(group.First()))
                .OrderBy(choice => choice.FieldPath, StringComparer.Ordinal).ToArray();
        }

        public GdoGraphRelationshipRecord CreateNextRelationship(string ownerNodeId, string fieldPath, Rect position)
        {
            RelationshipChoice(ownerNodeId, fieldPath);
            int order = _graph.Relationships.Count(value => value.OwnerNodeId == ownerNodeId);
            return CreateRelationship(ownerNodeId, fieldPath, order, position);
        }

        public GdoGraphEdgeRecord Connect(string outputNodeId, string inputElementId, string inputPath, int order)
            => Connect(outputNodeId, "Value", inputElementId, inputPath, order);

        public GdoGraphEdgeRecord Connect(string outputElementId, string outputPortPath,
            string inputElementId, string inputPath, int order)
        {
            if (StudioCustomGraphPipeline.IsCustomEdge(_graph, outputElementId, inputElementId))
            {
                if (!StudioCustomGraphPipeline.CanConnect(_graph, outputElementId, outputPortPath, inputElementId, inputPath))
                    throw new ArgumentException("Custom connection is incompatible or a port is full.");
                int customGroup = Begin("Connect custom graph node");
                try
                {
                    GdoGraphEdgeRecord customEdge = GdoBundleGraphRepository.AddEdge(_graph,
                        outputElementId, outputPortPath, inputElementId, inputPath, order);
                    // Structurally valid edges may leave a multi-input node incomplete while wiring.
                    // Keep the authored edge; failed projection preserves companions and blocks builds.
                    GdoGraphProjectionApplier.TryApply(_graph, out _, true, true);
                    Undo.CollapseUndoOperations(customGroup);
                    return customEdge;
                }
                catch { Undo.RevertAllDownToGroup(customGroup); throw; }
            }
            string exactType = GdoGraphConnectionTypeResolver.ExactOutputType(
                _graph, outputElementId, outputPortPath);
            if (string.IsNullOrWhiteSpace(exactType))
                throw new ArgumentException("Output graph element is missing, broken, or untyped.", nameof(outputElementId));
            GdoGraphPortDescriptor port = ResolveInputPort(inputElementId, inputPath);
            if (port == null) throw new ArgumentException("Input port does not exist.", nameof(inputPath));
            if (!GdoGraphConnectionTypeResolver.IsAssignable(exactType, port.ExpectedTypeName))
                throw new ArgumentException("Output type '" + exactType + "' is not compatible with '" +
                    port.ExpectedTypeName + "'.", nameof(outputElementId));
            if (port.Cardinality == GdoGraphPortCardinality.Single &&
                _graph.Edges.Any(edge => edge.InputNodeId == inputElementId && edge.InputPortPath == inputPath))
                throw new InvalidOperationException("The input port accepts only one connection.");
            int group = Begin("Connect GDO graph relationship");
            GdoGraphEdgeRecord edge = GdoBundleGraphRepository.AddEdge(_graph,
                outputElementId, outputPortPath, inputElementId, inputPath, order);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
            return edge;
        }

        public bool CanConnect(string outputNodeId, string inputElementId, string inputPath)
            => CanConnect(outputNodeId, "Value", inputElementId, inputPath);

        public bool CanConnect(string outputElementId, string outputPortPath,
            string inputElementId, string inputPath)
        {
            string exactType = GdoGraphConnectionTypeResolver.ExactOutputType(
                _graph, outputElementId, outputPortPath);
            if (StudioCustomGraphPipeline.IsCustomEdge(_graph, outputElementId, inputElementId))
                return StudioCustomGraphPipeline.CanConnect(_graph, outputElementId, outputPortPath, inputElementId, inputPath);
            if (string.IsNullOrWhiteSpace(exactType)) return false;
            GdoGraphPortDescriptor port = ResolveInputPort(inputElementId, inputPath);
            if (port == null || !GdoGraphConnectionTypeResolver.IsAssignable(exactType, port.ExpectedTypeName)) return false;
            return port.Cardinality != GdoGraphPortCardinality.Single ||
                !_graph.Edges.Any(edge => edge.InputNodeId == inputElementId && edge.InputPortPath == inputPath);
        }

        public GdoGraphRelationshipRecord CreateRelationship(string ownerNodeId, string fieldPath, int order, Rect position)
        {
            GdoRelationshipChoice choice = RelationshipChoice(ownerNodeId, fieldPath);
            if (!choice.IsRepeated && _graph.Relationships.Any(value => value.OwnerNodeId == ownerNodeId && value.FieldPath == fieldPath))
                throw new InvalidOperationException("The structured field accepts only one relationship node.");
            GdoOverlayValue template = GdoOverlayValueEditor.CreateDefault(choice.ValueDescriptor);
            int group = Begin("Create GDO relationship");
            int visualOrder = _graph.Relationships.Count(value => value.OwnerNodeId == ownerNodeId);
            GdoGraphRelationshipRecord relationship = GdoBundleGraphRepository.AddRelationship(_graph,
                ownerNodeId, fieldPath, visualOrder, position, template);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
            return relationship;
        }

        public void RemoveRelationship(string relationshipId, bool removeConnectedEdges)
        {
            // The repository validates consent before recording any mutation.
            int group = Begin("Remove GDO relationship");
            GdoBundleGraphRepository.RemoveRelationship(_graph, relationshipId, removeConnectedEdges);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
        }

        public void RemoveMember(string nodeId, bool removeConnectedEdges)
        {
            int group = Begin("Remove GDO graph member");
            try
            {
                GdoBundleGraphRepository.RemoveMember(_graph, nodeId, removeConnectedEdges);
                // Removal is a recovery operation: other broken/invalid members must not prevent progress.
                // TryApply restores companions on failure; the diagnostics keep building blocked until
                // the remaining invalid data is repaired or removed. The final removal reapplies projection.
                GdoGraphProjectionApplier.TryApply(_graph, out _, true, true);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        internal void RemoveCustomNode(string nodeId, bool removeConnectedEdges)
        {
            int group = Begin("Remove custom graph node");
            try
            {
                GdoBundleGraphRepository.RemoveCustomNode(_graph, nodeId, removeConnectedEdges);
                GdoGraphProjectionApplier.TryApply(_graph, out _, true, true);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private GdoRelationshipChoice RelationshipChoice(string ownerNodeId, string fieldPath) =>
            RelationshipChoices(ownerNodeId).FirstOrDefault(choice => choice.FieldPath == fieldPath) ??
            throw new ArgumentException("Field does not use a structured relationship node.", nameof(fieldPath));

        public void Disconnect(string edgeId)
        {
            int group = Begin("Disconnect GDO graph relationship");
            GdoGraphEdgeRecord edge = _graph.FindEdge(edgeId);
            bool custom = edge != null && StudioCustomGraphPipeline.IsCustomEdge(_graph, edge.OutputNodeId, edge.InputNodeId);
            if (custom)
            {
                try
                {
                    GdoBundleGraphRepository.RemoveEdge(_graph, edgeId);
                    GdoGraphProjectionApplier.TryApply(_graph, out _, true, true);
                    Undo.CollapseUndoOperations(group);
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
                return;
            }
            GdoBundleGraphRepository.RemoveEdge(_graph, edgeId);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
        }

        public void Reorder(string edgeId, int order)
        {
            int group = Begin("Reorder GDO graph relationship");
            GdoBundleGraphRepository.SetEdgeOrder(_graph, edgeId, order);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
        }

        public bool MoveRelationship(string relationshipId, int delta)
        {
            int group = Begin("Reorder GDO relationship");
            if (!GdoBundleGraphRepository.MoveRelationship(_graph, relationshipId, delta)) return false;
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
            return true;
        }

        public void SetRelationshipValue(string relationshipId, GdoOverlayValue value)
        {
            int group = Begin("Edit GDO relationship value");
            GdoBundleGraphRepository.SetRelationshipValue(_graph, relationshipId, value);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
        }

        internal void SetCustomValue(string nodeId, string key, StudioAuthoringValue value)
        {
            int group = Begin("Edit custom graph field");
            try
            {
                GdoBundleGraphRepository.SetCustomValue(_graph, nodeId, key, value);
                // Incomplete edits remain authored and diagnosable. Successful projections join the
                // field's undo transaction; failures preserve the previous companion contents.
                GdoGraphProjectionApplier.TryApply(_graph, out _, true, true);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        public void SetRelationshipValueAfterCollectionRemoval(string relationshipId, string collectionPath,
            int removedIndex, GdoOverlayValue value)
        {
            int group = Begin("Remove GDO relationship collection element");
            GdoBundleGraphRepository.SetRelationshipValueAfterCollectionRemoval(
                _graph, relationshipId, collectionPath, removedIndex, value);
            ApplyOrRevert(group);
            Undo.CollapseUndoOperations(group);
        }

        private void ApplyOrRevert(int group)
        {
            try
            {
                if (GdoGraphProjectionApplier.TryApply(_graph, out var diagnostics, true, true)) return;
                throw new InvalidOperationException("Graph projection failed: " +
                    string.Join("; ", diagnostics.Select(issue => issue.Message)));
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        private static int Begin(string name)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return group;
        }

        private GdoGraphPortDescriptor ResolveInputPort(string elementId, string path) => InputPort(_graph, elementId, path);

        internal static GdoGraphPortDescriptor InputPort(GdoBundleGraphAsset graph, string elementId, string path)
        {
            string descriptorPath = Regex.Replace(path ?? string.Empty, @"\[\d+\]", "[]");
            GdoGraphNodeRecord owner = graph.Nodes.FirstOrDefault(node => node.Id == elementId);
            if (owner != null && !owner.IsBroken)
            {
                GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForTarget(owner.Asset);
                return GdoGraphPortCatalog.ForDescriptor(descriptor)
                    .Concat(GdoUnityObjectPortCatalog.ForDescriptor(descriptor))
                    .FirstOrDefault(port => port.Path == descriptorPath);
            }
            GdoGraphRelationshipRecord relationship = graph.Relationships.FirstOrDefault(value => value.Id == elementId);
            if (relationship == null) return null;
            GdoGraphNodeRecord relationshipOwner = graph.Nodes.First(node => node.Id == relationship.OwnerNodeId);
            return GdoGraphPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForTarget(relationshipOwner.Asset))
                .FirstOrDefault(port => port.RelationshipPath == relationship.FieldPath && port.Path == descriptorPath);
        }

    }
}
