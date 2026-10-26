using System;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public enum GdoGraphPrefabSourceMode { Primitive, WorkspacePrefab }

    [Serializable]
    public sealed class GdoGraphPrefabRecord
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private GdoGraphPrefabSourceMode _sourceMode;
        [SerializeField] private PrimitiveType _primitiveType;
        [SerializeField] private Vector3 _scale;
        [SerializeField] private Material _material;
        [SerializeField] private bool _includeCollider;
        [SerializeField] private string _workspacePrefabGuid;
        [SerializeField] private GameObject _workspacePrefab;
        [SerializeField] private Rect _position;
        [SerializeField] private bool _expanded;

        private GdoGraphPrefabRecord() { }

        public string Id => _id;
        public string DisplayName => _displayName;
        public GdoGraphPrefabSourceMode SourceMode => _sourceMode;
        public PrimitiveType PrimitiveType => _primitiveType;
        public Vector3 Scale => _scale;
        public Material Material => _material;
        public bool IncludeCollider => _includeCollider;
        public string WorkspacePrefabGuid => _workspacePrefabGuid;
        public GameObject WorkspacePrefab => _workspacePrefab;
        public Rect Position => _position;
        public bool Expanded => _expanded;

        internal static GdoGraphPrefabRecord Create(string id, string displayName, Rect position) =>
            new GdoGraphPrefabRecord
            {
                _id = id,
                _displayName = displayName,
                _sourceMode = GdoGraphPrefabSourceMode.Primitive,
                _primitiveType = PrimitiveType.Cube,
                _scale = Vector3.one,
                _includeCollider = true,
                _position = position,
                _expanded = true
            };

        internal void ConfigurePrimitive(PrimitiveType primitiveType, Vector3 scale,
            Material material, bool includeCollider)
        {
            _sourceMode = GdoGraphPrefabSourceMode.Primitive;
            _primitiveType = primitiveType;
            _scale = scale;
            _material = material;
            _includeCollider = includeCollider;
        }

        internal void ConfigureWorkspace(GameObject prefab, string guid)
        {
            _sourceMode = GdoGraphPrefabSourceMode.WorkspacePrefab;
            _workspacePrefab = prefab;
            _workspacePrefabGuid = guid;
        }

        internal void SetSourceMode(GdoGraphPrefabSourceMode mode) => _sourceMode = mode;
        internal void SetPosition(Rect position) => _position = position;
        internal void SetExpanded(bool expanded) => _expanded = expanded;
        internal void SetDisplayName(string displayName) => _displayName = displayName;
    }
}
