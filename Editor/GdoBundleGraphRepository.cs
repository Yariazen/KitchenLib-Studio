using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class GdoBundleGraphRepository
    {
        public const int CurrentSchemaVersion = 4;

        public static GdoBundleGraphAsset Create(string assetPath, string authorNamespace, string bundleName,
            string bundleFileName)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("Graph asset path is required.", nameof(assetPath));
            GdoProjectIdentity.ValidateName(authorNamespace, nameof(authorNamespace));
            if (string.IsNullOrWhiteSpace(bundleName)) throw new ArgumentException("Bundle name is required.", nameof(bundleName));
            ValidateBundleFileName(bundleFileName);
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new ArgumentException("An asset already exists at the graph path.", nameof(assetPath));
            GdoBundleGraphAsset graph = ScriptableObject.CreateInstance<GdoBundleGraphAsset>();
            graph.name = Path.GetFileNameWithoutExtension(assetPath);
            graph.Initialise(bundleName, bundleFileName);
            graph.SetSchemaVersion(CurrentSchemaVersion);
            graph.SetAuthorIdentity(authorNamespace.Trim(), 1);
            AssetDatabase.CreateAsset(graph, assetPath);
            Undo.RegisterCreatedObjectUndo(graph, "Create GDO bundle graph");
            Undo.IncrementCurrentGroup();
            return graph;
        }

        public static bool EnsureCurrentSchema(GdoBundleGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (graph.SchemaVersion >= CurrentSchemaVersion) return false;
            Undo.RecordObject(graph, "Migrate GDO bundle graph");
            if (graph.SchemaVersion < 1)
                foreach (GdoGraphNodeRecord node in graph.Nodes.Where(value => value.Kind == GdoGraphNodeKind.Local))
                    node.SetPresentationMode(node.Expanded
                        ? GdoGraphPresentationMode.Full
                        : GdoGraphPresentationMode.Connected);
            if (graph.SchemaVersion < 4)
                foreach (GdoGraphEdgeRecord edge in graph.Edges.Where(value =>
                    !graph.ContainsElement(value.OutputNodeId) || !graph.ContainsElement(value.InputNodeId)).ToArray())
                    graph.RemoveEdge(edge.Id);
            graph.SetSchemaVersion(CurrentSchemaVersion);
            Dirty(graph);
            return true;
        }

        public static GdoGraphNodeRecord AddMember(GdoBundleGraphAsset graph, GameDataObject asset, Rect position)
        {
            RequireGraph(graph);
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("GDO must be saved as a Unity asset.", nameof(asset));
            if (graph.Nodes.Any(value => value.Kind == GdoGraphNodeKind.Local && value.AssetGuid == guid))
                throw new ArgumentException("The GDO is already a member of this graph.", nameof(asset));
            Record(graph, "Add GDO graph member");
            GdoGraphNodeRecord node = GdoGraphNodeRecord.Local(NewId(), guid, asset, position);
            graph.AddNode(node);
            Dirty(graph);
            return node;
        }

        public static GdoGraphNodeRecord AddExternal(GdoBundleGraphAsset graph, string providerKey, int id, string expectedType, Rect position, string cachedName = null)
        {
            RequireGraph(graph);
            if (id == 0) throw new ArgumentException("External GDO ID must be non-zero.", nameof(id));
            if (string.IsNullOrWhiteSpace(expectedType)) throw new ArgumentException("Expected type is required.", nameof(expectedType));
            if (string.IsNullOrWhiteSpace(providerKey)) throw new ArgumentException("Provider key is required.", nameof(providerKey));
            providerKey = providerKey.Trim();
            Record(graph, "Add external GDO reference");
            GdoGraphNodeRecord node = GdoGraphNodeRecord.External(NewId(), providerKey, id, expectedType, position, cachedName);
            graph.AddNode(node);
            Dirty(graph);
            return node;
        }

        public static GdoGraphPrefabRecord AddPrefab(GdoBundleGraphAsset graph, string displayName, Rect position)
        {
            RequireGraph(graph);
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Prefab name is required.", nameof(displayName));
            Record(graph, "Add prefab graph node");
            GdoGraphPrefabRecord prefab = GdoGraphPrefabRecord.Create(NewId(), displayName.Trim(), position);
            graph.AddPrefab(prefab);
            Dirty(graph);
            return prefab;
        }

        public static void ConfigurePrimitive(GdoBundleGraphAsset graph, string prefabId,
            PrimitiveType primitiveType, Vector3 scale, Material material, bool includeCollider)
        {
            GdoGraphPrefabRecord prefab = RequirePrefab(graph, prefabId);
            Record(graph, "Configure primitive prefab");
            prefab.ConfigurePrimitive(primitiveType, scale, material, includeCollider);
            Dirty(graph);
        }

        public static void ConfigureWorkspacePrefab(GdoBundleGraphAsset graph, string prefabId,
            GameObject source, string guid)
        {
            GdoGraphPrefabRecord prefab = RequirePrefab(graph, prefabId);
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(guid)) throw new ArgumentException("Prefab GUID is required.", nameof(guid));
            Record(graph, "Configure workspace prefab");
            prefab.ConfigureWorkspace(source, guid);
            Dirty(graph);
        }

        public static void SetPrefabSourceMode(GdoBundleGraphAsset graph, string prefabId,
            GdoGraphPrefabSourceMode mode)
        {
            GdoGraphPrefabRecord prefab = RequirePrefab(graph, prefabId);
            Record(graph, "Change prefab source");
            prefab.SetSourceMode(mode);
            Dirty(graph);
        }

        public static void RenamePrefab(GdoBundleGraphAsset graph, string prefabId, string displayName)
        {
            GdoGraphPrefabRecord prefab = RequirePrefab(graph, prefabId);
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Prefab name is required.", nameof(displayName));
            Record(graph, "Rename prefab graph node");
            prefab.SetDisplayName(displayName.Trim());
            Dirty(graph);
        }

        public static void RemovePrefab(GdoBundleGraphAsset graph, string prefabId, bool removeConnectedEdges = false)
        {
            GdoGraphPrefabRecord prefab = RequirePrefab(graph, prefabId);
            if (graph.HasConnections(prefab.Id) && !removeConnectedEdges)
                throw new InvalidOperationException("Explicitly allow removing connected edges before removing this prefab.");
            Record(graph, "Remove prefab graph node");
            foreach (GdoGraphEdgeRecord edge in graph.Edges
                .Where(value => value.InputNodeId == prefabId || value.OutputNodeId == prefabId).ToArray())
                graph.RemoveEdge(edge.Id);
            graph.RemovePrefab(prefabId);
            Dirty(graph);
        }

        public static void RemoveMember(GdoBundleGraphAsset graph, string nodeId, bool removeConnectedEdges = false)
        {
            RequireGraph(graph);
            GdoGraphNodeRecord node = graph.FindNode(nodeId);
            if (node == null) throw new ArgumentException("Graph node does not exist.", nameof(nodeId));
            if (graph.HasConnections(nodeId) && !removeConnectedEdges)
                throw new InvalidOperationException("Explicitly allow removing connected edges and owned relationships before removing this member.");
            Record(graph, "Remove GDO graph member");
            string[] owned = graph.Relationships.Where(r => r.OwnerNodeId == nodeId).Select(r => r.Id).ToArray();
            foreach (var edge in graph.Edges.Where(e => e.InputNodeId == nodeId || e.OutputNodeId == nodeId ||
                owned.Contains(e.InputNodeId) || owned.Contains(e.OutputNodeId)).ToArray()) graph.RemoveEdge(edge.Id);
            foreach (string relationship in owned) graph.RemoveRelationship(relationship);
            graph.RemoveNode(nodeId);
            Dirty(graph);
        }

        public static void SetPosition(GdoBundleGraphAsset graph, string nodeId, Rect position)
        {
            RequireGraph(graph);
            GdoGraphNodeRecord node = graph.FindNode(nodeId);
            if (node == null) throw new ArgumentException("Graph node does not exist.", nameof(nodeId));
            Record(graph, "Move GDO graph node");
            node.SetPosition(position);
            Dirty(graph);
        }

        public static void SetElementPositions(GdoBundleGraphAsset graph, IReadOnlyDictionary<string, Rect> positions)
        {
            RequireGraph(graph);
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            foreach (string elementId in positions.Keys)
                if (!graph.ContainsElement(elementId))
                    throw new ArgumentException("Graph element does not exist: " + elementId, nameof(positions));
            if (positions.Count == 0) return;
            Record(graph, "Move grouped GDO graph elements");
            foreach (KeyValuePair<string, Rect> value in positions)
            {
                GdoGraphNodeRecord node = graph.FindNode(value.Key);
                if (node != null) node.SetPosition(value.Value);
                else
                {
                    GdoGraphRelationshipRecord relationship = graph.FindRelationship(value.Key);
                    if (relationship != null) relationship.SetPosition(value.Value);
                    else
                    {
                        GdoGraphPrefabRecord prefab = graph.FindPrefab(value.Key);
                        prefab.SetPosition(value.Value);
                    }
                }
            }
            Dirty(graph);
        }

        public static void SetGroupPosition(GdoBundleGraphAsset graph, string ownerNodeId, Vector2 position)
        {
            RequireGraph(graph);
            GdoGraphNodeRecord owner = graph.FindNode(ownerNodeId);
            if (owner == null || owner.Kind != GdoGraphNodeKind.Local)
                throw new ArgumentException("Local GDO owner does not exist.", nameof(ownerNodeId));
            Record(graph, "Move GDO graph group");
            owner.SetGroupPosition(position);
            Dirty(graph);
        }

        public static void SetExpanded(GdoBundleGraphAsset graph, string nodeId, bool expanded)
        {
            RequireGraph(graph);
            GdoGraphNodeRecord node = graph.FindNode(nodeId);
            if (node == null) throw new ArgumentException("Graph node does not exist.", nameof(nodeId));
            Record(graph, expanded ? "Expand GDO graph node" : "Collapse GDO graph node");
            node.SetExpanded(expanded);
            Dirty(graph);
        }

        public static void SetPropertiesExpanded(GdoBundleGraphAsset graph, string nodeId, bool expanded)
        {
            RequireGraph(graph);
            GdoGraphNodeRecord node = graph.FindNode(nodeId);
            if (node == null || node.Kind != GdoGraphNodeKind.Local)
                throw new ArgumentException("Local GDO graph node does not exist.", nameof(nodeId));
            if (node.PropertiesExpanded == expanded) return;
            Record(graph, expanded ? "Expand GDO properties" : "Collapse GDO properties");
            node.SetPropertiesExpanded(expanded);
            Dirty(graph);
        }

        public static void SetPresentationMode(GdoBundleGraphAsset graph, string nodeId,
            GdoGraphPresentationMode mode)
        {
            RequireGraph(graph);
            GdoGraphNodeRecord node = graph.FindNode(nodeId);
            if (node == null || node.Kind != GdoGraphNodeKind.Local)
                throw new ArgumentException("Local GDO graph node does not exist.", nameof(nodeId));
            if (node.PresentationMode == mode) return;
            Record(graph, "Change GDO group presentation");
            node.SetPresentationMode(mode);
            Dirty(graph);
        }

        public static GdoGraphRelationshipRecord AddRelationship(GdoBundleGraphAsset graph, string ownerNodeId,
            string fieldPath, int order, Rect position)
            => AddRelationship(graph, ownerNodeId, fieldPath, order, position, null);

        public static GdoGraphRelationshipRecord AddRelationship(GdoBundleGraphAsset graph, string ownerNodeId,
            string fieldPath, int order, Rect position, GdoOverlayValue value)
        {
            RequireGraph(graph);
            if (graph.FindNode(ownerNodeId) == null) throw new ArgumentException("Relationship owner does not exist.", nameof(ownerNodeId));
            if (string.IsNullOrWhiteSpace(fieldPath)) throw new ArgumentException("Relationship field path is required.", nameof(fieldPath));
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            Record(graph, "Add GDO relationship");
            var ordered = OrderedRelationships(graph, ownerNodeId).ToList();
            int insertion = Math.Min(order, ordered.Count);
            GdoGraphRelationshipRecord relationship = GdoGraphRelationshipRecord.Create(
                NewId(), ownerNodeId, fieldPath, insertion, position, value);
            graph.AddRelationship(relationship);
            ordered.Insert(insertion, relationship);
            Renumber(ordered);
            Dirty(graph);
            return relationship;
        }

        public static void RemoveRelationship(GdoBundleGraphAsset graph, string relationshipId, bool removeConnectedEdges)
        {
            RequireGraph(graph);
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(relationshipId);
            if (relationship == null)
                throw new ArgumentException("Graph relationship does not exist.", nameof(relationshipId));
            GdoGraphEdgeRecord[] connected = graph.Edges
                .Where(edge => edge.InputNodeId == relationshipId || edge.OutputNodeId == relationshipId).ToArray();
            if (connected.Length > 0 && !removeConnectedEdges)
                throw new InvalidOperationException("Explicitly allow removing connected edges before removing this relationship.");
            Record(graph, "Remove GDO relationship");
            foreach (GdoGraphEdgeRecord edge in connected) graph.RemoveEdge(edge.Id);
            graph.RemoveRelationship(relationshipId);
            Renumber(OrderedRelationships(graph, relationship.OwnerNodeId));
            Dirty(graph);
        }

        public static void SetRelationshipPosition(GdoBundleGraphAsset graph, string relationshipId, Rect position)
        {
            RequireGraph(graph);
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(relationshipId);
            if (relationship == null) throw new ArgumentException("Graph relationship does not exist.", nameof(relationshipId));
            Record(graph, "Move GDO relationship node");
            relationship.SetPosition(position);
            Dirty(graph);
        }

        public static bool MoveRelationship(GdoBundleGraphAsset graph, string relationshipId, int delta)
        {
            RequireGraph(graph);
            if (delta != -1 && delta != 1) throw new ArgumentOutOfRangeException(nameof(delta));
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(relationshipId);
            if (relationship == null) throw new ArgumentException("Graph relationship does not exist.", nameof(relationshipId));
            GdoGraphRelationshipRecord[] siblings = OrderedRelationships(graph, relationship.OwnerNodeId).ToArray();
            int index = Array.IndexOf(siblings, relationship);
            int destination = index + delta;
            if (index < 0 || destination < 0 || destination >= siblings.Length) return false;
            Record(graph, "Reorder GDO relationship");
            GdoGraphRelationshipRecord moved = siblings[index];
            siblings[index] = siblings[destination];
            siblings[destination] = moved;
            Renumber(siblings);
            Dirty(graph);
            return true;
        }

        public static void SetRelationshipExpanded(GdoBundleGraphAsset graph, string relationshipId, bool expanded)
        {
            RequireGraph(graph);
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(relationshipId);
            if (relationship == null) throw new ArgumentException("Graph relationship does not exist.", nameof(relationshipId));
            if (relationship.Expanded == expanded) return;
            Record(graph, expanded ? "Expand GDO relationship" : "Collapse GDO relationship");
            relationship.SetExpanded(expanded);
            Dirty(graph);
        }

        public static IReadOnlyList<GdoGraphRelationshipRecord> OrderedRelationships(
            GdoBundleGraphAsset graph, string ownerNodeId)
        {
            RequireGraph(graph);
            return graph.Relationships.Where(value => value.OwnerNodeId == ownerNodeId)
                .OrderBy(value => value.Order).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
        }

        private static void Renumber(IEnumerable<GdoGraphRelationshipRecord> relationships)
        {
            int order = 0;
            foreach (GdoGraphRelationshipRecord relationship in relationships) relationship.SetOrder(order++);
        }

        public static void SetRelationshipValue(GdoBundleGraphAsset graph, string relationshipId, GdoOverlayValue value)
        {
            RequireGraph(graph);
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(relationshipId);
            if (relationship == null) throw new ArgumentException("Graph relationship does not exist.", nameof(relationshipId));
            Record(graph, "Edit GDO relationship value");
            relationship.SetValue(value);
            Dirty(graph);
        }

        public static void SetRelationshipValueAfterCollectionRemoval(GdoBundleGraphAsset graph,
            string relationshipId, string collectionPath, int removedIndex, GdoOverlayValue value)
        {
            RequireGraph(graph);
            GdoGraphRelationshipRecord relationship = graph.FindRelationship(relationshipId);
            if (relationship == null) throw new ArgumentException("Graph relationship does not exist.", nameof(relationshipId));
            if (string.IsNullOrWhiteSpace(collectionPath)) throw new ArgumentException("Collection path is required.", nameof(collectionPath));
            if (removedIndex < 0) throw new ArgumentOutOfRangeException(nameof(removedIndex));
            Record(graph, "Remove GDO relationship collection element");
            relationship.SetValue(value);
            string prefix = collectionPath + "[";
            foreach (GdoGraphEdgeRecord edge in graph.Edges
                .Where(candidate => candidate.InputNodeId == relationshipId && candidate.InputPortPath.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray())
            {
                int closingBracket = edge.InputPortPath.IndexOf(']', prefix.Length);
                if (closingBracket < 0 || !int.TryParse(edge.InputPortPath.Substring(prefix.Length,
                    closingBracket - prefix.Length), out int index)) continue;
                if (index == removedIndex)
                    graph.RemoveEdge(edge.Id);
                else if (index > removedIndex)
                    edge.SetInputPortPath(prefix + (index - 1) + edge.InputPortPath.Substring(closingBracket));
            }
            Dirty(graph);
        }

        public static GdoGraphEdgeRecord AddEdge(GdoBundleGraphAsset graph, string outputNodeId, string outputPortPath,
            string inputNodeId, string inputPortPath, int order)
        {
            RequireGraph(graph);
            if (!graph.ContainsElement(outputNodeId)) throw new ArgumentException("Output element does not exist.", nameof(outputNodeId));
            if (!graph.ContainsElement(inputNodeId)) throw new ArgumentException("Input element does not exist.", nameof(inputNodeId));
            if (string.IsNullOrWhiteSpace(outputPortPath)) throw new ArgumentException("Output port path is required.", nameof(outputPortPath));
            if (string.IsNullOrWhiteSpace(inputPortPath)) throw new ArgumentException("Input port path is required.", nameof(inputPortPath));
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            Record(graph, "Connect GDO graph elements");
            GdoGraphEdgeRecord edge = GdoGraphEdgeRecord.Create(NewId(), outputNodeId, outputPortPath, inputNodeId, inputPortPath, order);
            graph.AddEdge(edge);
            Dirty(graph);
            return edge;
        }

        public static void RemoveEdge(GdoBundleGraphAsset graph, string edgeId)
        {
            RequireGraph(graph);
            if (graph.FindEdge(edgeId) == null) throw new ArgumentException("Graph edge does not exist.", nameof(edgeId));
            Record(graph, "Disconnect GDO graph elements");
            graph.RemoveEdge(edgeId);
            Dirty(graph);
        }

        public static void SetEdgeOrder(GdoBundleGraphAsset graph, string edgeId, int order)
        {
            RequireGraph(graph);
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            GdoGraphEdgeRecord edge = graph.FindEdge(edgeId);
            if (edge == null) throw new ArgumentException("Graph edge does not exist.", nameof(edgeId));
            Record(graph, "Reorder GDO graph connection");
            edge.SetOrder(order);
            Dirty(graph);
        }

        public static IReadOnlyList<GdoBundleGraphAsset> FindGraphsContaining(GameDataObject asset)
        {
            if (asset == null) return Array.Empty<GdoBundleGraphAsset>();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrEmpty(guid)) return Array.Empty<GdoBundleGraphAsset>();
            return AssetDatabase.FindAssets("t:GdoBundleGraphAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>)
                .Where(graph => graph != null && graph.Nodes.Any(node => node.Kind == GdoGraphNodeKind.Local && node.AssetGuid == guid))
                .ToArray();
        }

        private static void ValidateBundleFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(fileName), ".assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Bundle file name must be a single file name with the .assets extension.", nameof(fileName));
        }

        private static void RequireGraph(GdoBundleGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            EnsureCurrentSchema(graph);
        }

        private static GdoGraphPrefabRecord RequirePrefab(GdoBundleGraphAsset graph, string prefabId)
        {
            RequireGraph(graph);
            GdoGraphPrefabRecord prefab = graph.FindPrefab(prefabId);
            if (prefab == null) throw new ArgumentException("Prefab graph node does not exist.", nameof(prefabId));
            return prefab;
        }

        private static string NewId() => Guid.NewGuid().ToString("N");
        private static void Record(GdoBundleGraphAsset graph, string action)
        {
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(graph, action);
        }
        private static void Dirty(GdoBundleGraphAsset graph)
        {
            EditorUtility.SetDirty(graph);
        }
    }
}
