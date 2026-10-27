using System;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Graph
{
    internal enum GdoGraphPrefabSourceMode { Primitive, WorkspacePrefab }
    internal enum GdoGraphPrefabMaterialMode { None, ExistingMaterial, GeneratedMaterial }
    internal enum GdoGraphPrefabShaderMode { FlatUnlit, Lit, CustomShader }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoGraphPrefabRecord")]
    internal sealed class GdoGraphPrefabRecord
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private GdoGraphPrefabSourceMode _sourceMode;
        [SerializeField] private PrimitiveType _primitiveType;
        [SerializeField] private Vector3 _scale;
        [SerializeField] private Material _material;
        [SerializeField] private int _materialSettingsVersion;
        [SerializeField] private GdoGraphPrefabMaterialMode _materialMode;
        [SerializeField] private GdoGraphPrefabShaderMode _shaderMode;
        [SerializeField] private Shader _customShader;
        [SerializeField] private Color _materialColor;
        [SerializeField] private string _colorPropertyName;
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
        public GdoGraphPrefabMaterialMode MaterialMode => _materialSettingsVersion == 0
            ? (_material == null ? GdoGraphPrefabMaterialMode.None : GdoGraphPrefabMaterialMode.ExistingMaterial)
            : _materialMode;
        public GdoGraphPrefabShaderMode ShaderMode => _shaderMode;
        public Shader CustomShader => _customShader;
        public Color MaterialColor => _materialSettingsVersion == 0 ? Color.white : _materialColor;
        public string ColorPropertyName => _materialSettingsVersion == 0 || string.IsNullOrEmpty(_colorPropertyName)
            ? "_Color" : _colorPropertyName;
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
                _materialSettingsVersion = 1,
                _materialMode = GdoGraphPrefabMaterialMode.GeneratedMaterial,
                _shaderMode = GdoGraphPrefabShaderMode.FlatUnlit,
                _materialColor = Color.white,
                _colorPropertyName = "_Color",
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

        internal void ConfigureMaterial(GdoGraphPrefabMaterialMode materialMode, Material material,
            GdoGraphPrefabShaderMode shaderMode, Shader customShader, Color color, string colorPropertyName)
        {
            _materialSettingsVersion = 1;
            _materialMode = materialMode;
            _material = materialMode == GdoGraphPrefabMaterialMode.ExistingMaterial ? material : null;
            _shaderMode = shaderMode;
            _customShader = shaderMode == GdoGraphPrefabShaderMode.CustomShader ? customShader : null;
            _materialColor = color;
            _colorPropertyName = colorPropertyName;
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
