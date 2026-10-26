using System;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public enum GdoGraphNodeKind
    {
        Local,
        External
    }

    [Serializable]
    public sealed class GdoGraphNodeRecord
    {
        [SerializeField] private string _id;
        [SerializeField] private GdoGraphNodeKind _kind;
        [SerializeField] private string _assetGuid;
        [SerializeField] private GameDataObject _asset;
        [SerializeField] private string _cachedName;
        [SerializeField] private string _runtimeTypeName;
        [SerializeField] private int _cachedRuntimeId;
        [SerializeField] private int _externalId;
        [SerializeField] private string _externalProviderKey;
        [SerializeField] private string _expectedType;
        [SerializeField] private Rect _position;
        [SerializeField] private Vector2 _groupPosition;
        [SerializeField] private bool _expanded;
        [SerializeField] private bool _propertiesExpanded;
        [SerializeField] private GdoGraphPresentationMode _presentationMode;

        private GdoGraphNodeRecord() { }

        public string Id => _id;
        public GdoGraphNodeKind Kind => _kind;
        public string AssetGuid => _assetGuid;
        public GameDataObject Asset => _asset;
        public string CachedName => _cachedName;
        public string RuntimeTypeName => _runtimeTypeName;
        public int RuntimeId => _asset == null ? _cachedRuntimeId : _asset.ID;
        public int ExternalId => _externalId;
        public string ExternalProviderKey => _externalProviderKey;
        public string ExpectedType => _expectedType;
        public Rect Position => _position;
        public Vector2 GroupPosition => _groupPosition;
        public bool Expanded => _expanded;
        public bool PropertiesExpanded => _propertiesExpanded;
        public GdoGraphPresentationMode PresentationMode => _presentationMode;
        public bool IsBroken => _kind == GdoGraphNodeKind.Local && _asset == null;

        internal static GdoGraphNodeRecord Local(string id, string assetGuid, GameDataObject asset, Rect position) =>
            new GdoGraphNodeRecord
            {
                _id = id,
                _kind = GdoGraphNodeKind.Local,
                _assetGuid = assetGuid,
                _asset = asset,
                _cachedName = asset.name,
                _runtimeTypeName = RuntimeName(asset.GetType()),
                _cachedRuntimeId = asset.ID,
                _position = position,
                _groupPosition = position.position,
                _propertiesExpanded = true,
                _presentationMode = GdoGraphPresentationMode.Nodes
            };

        internal static GdoGraphNodeRecord External(string id, string providerKey, int externalId, string expectedType, Rect position, string cachedName = null)
        {
            if (string.IsNullOrWhiteSpace(providerKey)) throw new ArgumentException("Provider key is required.", nameof(providerKey));
            return new GdoGraphNodeRecord
            {
                _id = id,
                _kind = GdoGraphNodeKind.External,
                _externalId = externalId,
                _externalProviderKey = providerKey.Trim(),
                _expectedType = expectedType,
                _cachedName = string.IsNullOrEmpty(cachedName) ? externalId.ToString() : cachedName,
                _runtimeTypeName = expectedType,
                _position = position
            };
        }

        internal void SetPosition(Rect position) => _position = position;
        internal void SetGroupPosition(Vector2 position)
            => _groupPosition = position;
        internal void SetExpanded(bool expanded) => _expanded = expanded;
        internal void SetPropertiesExpanded(bool expanded) => _propertiesExpanded = expanded;
        internal void SetPresentationMode(GdoGraphPresentationMode mode) => _presentationMode = mode;

        internal void RefreshCachedMetadata()
        {
            if (_kind != GdoGraphNodeKind.Local || _asset == null) return;
            _cachedName = _asset.name;
            _runtimeTypeName = RuntimeName(_asset.GetType());
            _cachedRuntimeId = _asset.ID;
        }

        private static string RuntimeName(Type type) =>
            type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
    }

    [Serializable]
    public sealed class GdoGraphRelationshipRecord
    {
        [SerializeField] private string _id;
        [SerializeField] private string _ownerNodeId;
        [SerializeField] private string _fieldPath;
        [SerializeField] private int _order;
        [SerializeField] private Rect _position;
        [SerializeField] private GdoOverlaySerializedGraph _value;
        [SerializeField] private bool _hasExpandedState;
        [SerializeField] private bool _expanded;

        private GdoGraphRelationshipRecord() { }

        public string Id => _id;
        public string OwnerNodeId => _ownerNodeId;
        public string FieldPath => _fieldPath;
        public int Order => _order;
        public bool Expanded => !_hasExpandedState || _expanded;
        public Rect Position => _position;
        public GdoOverlayValue Value
        {
            get
            {
                if (_value == null) return null;
                return _value.TryDecode(out GdoOverlayValue value, out _) ? value : null;
            }
        }

        internal void SetPosition(Rect position) => _position = position;
        internal void SetOrder(int order) => _order = order;
        internal void SetExpanded(bool expanded) { _expanded = expanded; _hasExpandedState = true; }
        internal void SetValue(GdoOverlayValue value) => _value = value == null ? null : GdoOverlaySerializedGraph.Encode(value);

        internal static GdoGraphRelationshipRecord Create(string id, string ownerNodeId, string fieldPath, int order,
            Rect position, GdoOverlayValue value) =>
            new GdoGraphRelationshipRecord
            {
                _id = id,
                _ownerNodeId = ownerNodeId,
                _fieldPath = fieldPath,
                _order = order,
                _position = position,
                _hasExpandedState = true,
                _expanded = true,
                _value = value == null ? null : GdoOverlaySerializedGraph.Encode(value)
            };
    }

    [Serializable]
    public sealed class GdoGraphEdgeRecord
    {
        [SerializeField] private string _id;
        [SerializeField] private string _outputNodeId;
        [SerializeField] private string _outputPortPath;
        [SerializeField] private string _inputNodeId;
        [SerializeField] private string _inputPortPath;
        [SerializeField] private int _order;

        private GdoGraphEdgeRecord() { }

        public string Id => _id;
        public string OutputNodeId => _outputNodeId;
        public string OutputPortPath => _outputPortPath;
        public string InputNodeId => _inputNodeId;
        public string InputPortPath => _inputPortPath;
        public int Order => _order;

        internal static GdoGraphEdgeRecord Create(string id, string outputNodeId, string outputPortPath,
            string inputNodeId, string inputPortPath, int order) => new GdoGraphEdgeRecord
        {
            _id = id,
            _outputNodeId = outputNodeId,
            _outputPortPath = outputPortPath,
            _inputNodeId = inputNodeId,
            _inputPortPath = inputPortPath,
            _order = order
        };

        internal void SetOrder(int order) => _order = order;
        internal void SetInputPortPath(string inputPortPath) => _inputPortPath = inputPortPath;
        internal void SetOutputPortPath(string outputPortPath) => _outputPortPath = outputPortPath;
    }
}
