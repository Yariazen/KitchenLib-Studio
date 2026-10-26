using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using KitchenLib.Studio.Editor.Authoring;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphConnectionTypeResolver
    {
        public static string ExactOutputType(GdoBundleGraphAsset graph, string elementId,
            string outputPortPath)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            GdoGraphNodeRecord node = graph.FindNode(elementId);
            if (node != null && !node.IsBroken)
                return node.Kind == GdoGraphNodeKind.Local ? node.RuntimeTypeName : node.ExpectedType;
            if (graph.FindPrefab(elementId) != null) return typeof(GameObject).FullName;
            return null;
        }

        public static bool IsAssignable(string exactSourceType, string expectedDestinationType)
        {
            if (string.IsNullOrWhiteSpace(exactSourceType) ||
                string.IsNullOrWhiteSpace(expectedDestinationType)) return false;
            Type source = GdoGraphAssetCommands.ResolveType(exactSourceType);
            Type destination = GdoGraphAssetCommands.ResolveType(expectedDestinationType);
            if (source != null && destination != null) return destination.IsAssignableFrom(source);
            return GdoDescriptorCatalog.IsAssignable(exactSourceType, expectedDestinationType) ||
                string.Equals(exactSourceType, expectedDestinationType, StringComparison.Ordinal);
        }

        internal static string ExpectedInputType(GdoBundleGraphAsset graph, string elementId, string inputPath)
        {
            string descriptorPath = Regex.Replace(inputPath ?? string.Empty, @"\[\d+\]", "[]");
            GdoGraphNodeRecord owner = graph.FindNode(elementId);
            if (owner != null && owner.Kind == GdoGraphNodeKind.Local && !owner.IsBroken)
            {
                GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForTarget(owner.Asset);
                return GdoGraphPortCatalog.ForDescriptor(descriptor)
                    .Concat(GdoUnityObjectPortCatalog.ForDescriptor(descriptor))
                    .FirstOrDefault(port => port.Path == descriptorPath)?.ExpectedTypeName;
            }
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(elementId);
            if (relationship == null) return null;
            GdoGraphNodeRecord relationshipOwner = graph.FindNode(relationship.OwnerNodeId);
            if (relationshipOwner == null || relationshipOwner.Kind != GdoGraphNodeKind.Local || relationshipOwner.IsBroken) return null;
            return GdoGraphPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForTarget(relationshipOwner.Asset))
                .FirstOrDefault(port => port.RelationshipPath == relationship.FieldPath &&
                    port.Path == descriptorPath)?.ExpectedTypeName;
        }
    }
}
