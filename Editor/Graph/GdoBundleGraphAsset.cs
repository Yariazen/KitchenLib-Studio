using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Graph
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoBundleGraphAsset")]
    internal sealed class GdoBundleGraphAsset : ScriptableObject
    {
        [SerializeField] private string _bundleName;
        [SerializeField] private string _bundleFileName;
        [SerializeField] private string _authorNamespace;
        [SerializeField] private int _identitySchemeVersion;
        [SerializeField] private int _schemaVersion;
        [SerializeField] private List<GdoGraphNodeRecord> _nodes = new List<GdoGraphNodeRecord>();
        [SerializeField] private List<GdoGraphRelationshipRecord> _relationships = new List<GdoGraphRelationshipRecord>();
        [SerializeField] private List<GdoGraphEdgeRecord> _edges = new List<GdoGraphEdgeRecord>();
        [SerializeField] private List<StudioCustomGraphNodeRecord> _customNodes = new List<StudioCustomGraphNodeRecord>();
        [SerializeField] private List<GdoGraphPrefabRecord> _prefabs = new List<GdoGraphPrefabRecord>();
        [SerializeField] private List<GdoGraphPrefabProjectionState> _prefabProjectionStates =
            new List<GdoGraphPrefabProjectionState>();

        public IReadOnlyList<StudioCustomGraphNodeRecord> CustomNodes => _customNodes.OrderBy(value => value.Id, System.StringComparer.Ordinal).ToArray();
        internal StudioCustomGraphNodeRecord FindCustomNode(string id) => _customNodes.SingleOrDefault(value => value.Id == id);
        internal void AddCustomNode(StudioCustomGraphNodeRecord node) => _customNodes.Add(node);
        internal bool RemoveCustomNode(string id) => _customNodes.RemoveAll(value => value.Id == id) != 0;

        public string BundleName => _bundleName;
        public string BundleFileName => _bundleFileName;
        public string AuthorNamespace => _authorNamespace ?? string.Empty;
        public int IdentitySchemeVersion => _identitySchemeVersion;
        public int SchemaVersion => _schemaVersion;
        public bool HasAuthorIdentity => _identitySchemeVersion >= 1 && !string.IsNullOrWhiteSpace(_authorNamespace);
        public IReadOnlyList<GdoGraphNodeRecord> Nodes => _nodes.OrderBy(value => value.Id, System.StringComparer.Ordinal).ToArray();
        public IReadOnlyList<GdoGraphRelationshipRecord> Relationships => _relationships
            .OrderBy(value => value.Order).ThenBy(value => value.Id, System.StringComparer.Ordinal).ToArray();
        public IReadOnlyList<GdoGraphEdgeRecord> Edges => _edges
            .OrderBy(value => value.Order).ThenBy(value => value.Id, System.StringComparer.Ordinal).ToArray();
        public IReadOnlyList<GdoGraphPrefabRecord> Prefabs => _prefabs
            .OrderBy(value => value.Id, System.StringComparer.Ordinal).ToArray();
        public IReadOnlyList<GdoGraphPrefabProjectionState> PrefabProjectionStates =>
            _prefabProjectionStates.ToArray();

        internal void Initialise(string bundleName, string bundleFileName)
            => SetIdentity(bundleName, bundleFileName);

        internal void SetIdentity(string bundleName, string bundleFileName)
        {
            _bundleName = bundleName;
            _bundleFileName = bundleFileName;
        }

        internal void SetAuthorIdentity(string authorNamespace, int version)
        {
            _authorNamespace = authorNamespace;
            _identitySchemeVersion = version;
        }

        internal void SetSchemaVersion(int version) => _schemaVersion = version;

        internal void AddNode(GdoGraphNodeRecord node) => _nodes.Add(node);
        internal bool RemoveNode(string id) => _nodes.RemoveAll(value => value.Id == id) != 0;
        internal void AddRelationship(GdoGraphRelationshipRecord relationship) => _relationships.Add(relationship);
        internal bool RemoveRelationship(string id) => _relationships.RemoveAll(value => value.Id == id) != 0;
        internal void AddEdge(GdoGraphEdgeRecord edge) => _edges.Add(edge);
        internal bool RemoveEdge(string id) => _edges.RemoveAll(value => value.Id == id) != 0;
        internal void AddPrefab(GdoGraphPrefabRecord prefab) => _prefabs.Add(prefab);
        internal bool RemovePrefab(string id) => _prefabs.RemoveAll(value => value.Id == id) != 0;
        internal void AddPrefabProjectionState(GdoGraphPrefabProjectionState state) => _prefabProjectionStates.Add(state);
        internal bool RemovePrefabProjectionState(string ownerNodeId, string fieldPath) =>
            _prefabProjectionStates.RemoveAll(value => value.OwnerNodeId == ownerNodeId && value.FieldPath == fieldPath) != 0;

        internal GdoGraphNodeRecord FindNode(string id) => _nodes.SingleOrDefault(value => value.Id == id);
        internal GdoGraphRelationshipRecord FindRelationship(string id) => _relationships.SingleOrDefault(value => value.Id == id);
        internal GdoGraphEdgeRecord FindEdge(string id) => _edges.SingleOrDefault(value => value.Id == id);
        internal GdoGraphPrefabRecord FindPrefab(string id) => _prefabs.SingleOrDefault(value => value.Id == id);
        internal bool ContainsElement(string id) => _nodes.Any(value => value.Id == id) ||
            _relationships.Any(value => value.Id == id) || _prefabs.Any(value => value.Id == id) ||
            _customNodes.Any(value => value.Id == id);
        internal bool HasConnections(string id) => _relationships.Any(value => value.OwnerNodeId == id) ||
            _edges.Any(value => value.OutputNodeId == id || value.InputNodeId == id);
    }
}
