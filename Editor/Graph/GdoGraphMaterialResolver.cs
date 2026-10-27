using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using KitchenLib.Studio.Editor.Packaging;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphMaterialResolver
    {
        private const string FlatShaderName = "Unlit/Color";
        private const string LitShaderName = "Standard";

        public static string AssetPath(GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (record == null) throw new ArgumentNullException(nameof(record));
            return GdoBundleProjectRepository.MaterialFolder(graph) + "/" + record.Id + ".mat";
        }

        public static IReadOnlyList<GdoGraphDiagnostic> Validate(
            GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            var issues = new List<GdoGraphDiagnostic>();
            if (graph == null || record == null)
            {
                issues.Add(Issue(record?.Id, "Prefab material configuration is missing."));
                return issues;
            }
            if (record.MaterialMode == GdoGraphPrefabMaterialMode.None) return issues;
            if (record.MaterialMode == GdoGraphPrefabMaterialMode.ExistingMaterial)
            {
                if (record.Material == null) issues.Add(Issue(record.Id, "Existing Material is not assigned."));
                return issues;
            }
            Shader shader = ConfiguredShader(record);
            if (shader == null)
            {
                string label = record.ShaderMode == GdoGraphPrefabShaderMode.CustomShader
                    ? "Custom shader is not assigned."
                    : "Required shader '" + ShaderName(record.ShaderMode) + "' is unavailable.";
                issues.Add(Issue(record.Id, label));
                return issues;
            }
            var probe = new Material(shader);
            try
            {
                if (!probe.HasProperty(record.ColorPropertyName))
                    issues.Add(Issue(record.Id, "Shader '" + shader.name + "' does not define color property '" +
                        record.ColorPropertyName + "'."));
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            return issues;
        }

        public static Material Resolve(GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            IReadOnlyList<GdoGraphDiagnostic> issues = Validate(graph, record);
            if (issues.Count != 0) throw new InvalidOperationException(issues[0].Message);
            if (record.MaterialMode == GdoGraphPrefabMaterialMode.None) return null;
            if (record.MaterialMode == GdoGraphPrefabMaterialMode.ExistingMaterial) return record.Material;
            string path = AssetPath(graph, record);
            EnsureFolder(GdoBundleProjectRepository.MaterialFolder(graph));
            Shader shader = ConfiguredShader(record);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = record.DisplayName + " Material" };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor(record.ColorPropertyName, record.MaterialColor);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void DeleteOwned(GdoBundleGraphAsset graph, GdoGraphPrefabRecord record)
        {
            string path = AssetPath(graph, record);
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        }

        private static Shader ConfiguredShader(GdoGraphPrefabRecord record) =>
            record.ShaderMode == GdoGraphPrefabShaderMode.CustomShader
                ? record.CustomShader : Shader.Find(ShaderName(record.ShaderMode));

        private static string ShaderName(GdoGraphPrefabShaderMode mode) =>
            mode == GdoGraphPrefabShaderMode.Lit ? LitShaderName : FlatShaderName;

        private static GdoGraphDiagnostic Issue(string id, string message) =>
            new GdoGraphDiagnostic(ValidationSeverity.Error, "InvalidPrefabMaterial", id ?? string.Empty,
                string.Empty, "material", message);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
