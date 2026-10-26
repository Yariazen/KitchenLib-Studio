using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Graph;
using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Packaging
{
    internal static class GdoPackageAudit
    {
        public const string ReportFileName = "gdo-package-audit.json";

        [Serializable]
        private sealed class ManifestShape
        {
            public int schemaVersion;
            public ManifestTarget[] targets;
        }

        [Serializable]
        private sealed class ManifestTarget { public int gdoId; }

        public static GdoPackageAuditReport Audit(
            string packageRoot,
            IEnumerable<string> bundlePaths,
            IEnumerable<string> declaredSidecarNames,
            IEnumerable<string> allowedRuntimeAssemblies)
        {
            if (string.IsNullOrWhiteSpace(packageRoot)) throw new ArgumentException("Package root is required.", nameof(packageRoot));
            var findings = new List<GdoPackageAuditFinding>();
            var declared = new HashSet<string>((declaredSidecarNames ?? Array.Empty<string>()).Select(Path.GetFileName), StringComparer.Ordinal);
            var allowedAssemblies = new HashSet<string>(allowedRuntimeAssemblies ?? Array.Empty<string>(), StringComparer.Ordinal);
            var sidecarCounts = declared.ToDictionary(name => name, name => 0, StringComparer.Ordinal);
            int binaryCount = AuditPackageFiles(packageRoot, findings);
            int scriptCount = 0;

            foreach (string bundlePath in (bundlePaths ?? Array.Empty<string>()).OrderBy(path => path, StringComparer.Ordinal))
                AuditBundle(bundlePath, declared, allowedAssemblies, sidecarCounts, findings, ref scriptCount);

            foreach (KeyValuePair<string, int> pair in sidecarCounts)
            {
                if (pair.Value == 0) Add(findings, "MissingSidecar", pair.Key, "Declared sidecar is absent from the audited bundles.");
                if (pair.Value > 1) Add(findings, "DuplicateSidecar", pair.Key, "Declared sidecar appears more than once.");
            }

            return new GdoPackageAuditReport(binaryCount, scriptCount, findings);
        }

        public static void Write(string packageRoot, GdoPackageAuditReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            Directory.CreateDirectory(packageRoot);
            File.WriteAllText(Path.Combine(packageRoot, ReportFileName), report.ToJson(), new System.Text.UTF8Encoding(false));
        }

        private static int AuditPackageFiles(string packageRoot, ICollection<GdoPackageAuditFinding> findings)
        {
            int count = 0;
            IEnumerable<string> packageFiles = Directory.Exists(packageRoot)
                ? Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal)
                : (IEnumerable<string>)Array.Empty<string>();
            foreach (string path in packageFiles)
            {
                string extension = Path.GetExtension(path);
                string fileName = Path.GetFileName(path);
                if (string.Equals(fileName, "vanilla-snapshot-catalog.json", StringComparison.OrdinalIgnoreCase))
                    Add(findings, "SnapshotMetadata", path, "Snapshot catalogue metadata cannot be included in a runtime package.");
                if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf("Snapshots", StringComparison.OrdinalIgnoreCase) >= 0)
                    Add(findings, "SnapshotCachePayload", path, "Editor snapshot cache payloads cannot be included in a runtime package.");
                if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".pdb", StringComparison.OrdinalIgnoreCase)) continue;
                count++;
                Add(findings, "ManagedBinary", path, "Data-only packages cannot contain a managed binary (DLL or PDB)." );
            }
            return count;
        }

        private static void AuditBundle(
            string bundlePath,
            ISet<string> declared,
            ISet<string> allowedAssemblies,
            IDictionary<string, int> sidecarCounts,
            ICollection<GdoPackageAuditFinding> findings,
            ref int scriptCount)
        {
            if (!File.Exists(bundlePath)) { Add(findings, "MissingBundle", bundlePath, "Declared bundle does not exist."); return; }
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null) { Add(findings, "MalformedBundle", bundlePath, "AssetBundle could not be loaded."); return; }
            try
            {
                string[] assetNames = bundle.GetAllAssetNames().OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] prefabPaths = assetNames.Where(name => bundle.LoadAsset<GameObject>(name) != null).ToArray();
                var prefabDependencies = new HashSet<string>(prefabPaths.Length == 0
                    ? Array.Empty<string>() : AssetDatabase.GetDependencies(prefabPaths, true)
                        .Select(path => path.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
                foreach (string assetName in assetNames)
                {
                    string normalized = assetName.Replace('\\', '/');
                    if (normalized.IndexOf("/editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        Add(findings, "EditorAsset", assetName, "Editor assets cannot be serialized into a runtime package.");
                    if (normalized.IndexOf("/generated/", StringComparison.OrdinalIgnoreCase) >= 0)
                        Add(findings, "GeneratedMirrorAsset", assetName, "Generated mirror assets are authoring-only and cannot be bundled.");

                    UnityEngine.Object asset = bundle.LoadAsset(assetName);
                    if (asset == null) { Add(findings, "MissingSerializedAsset", assetName, "Serialized asset could not be loaded."); continue; }
                    if (asset is GdoBundleGraphAsset)
                        Add(findings, "GraphAuthoringMetadata", assetName, "Bundle graph assets are editor metadata and cannot be packaged.");
                    Type type = asset.GetType();
                    string assembly = type.Assembly.GetName().Name;
                    if (asset is MonoScript)
                    {
                        scriptCount++;
                        Add(findings, "SerializedScript", assetName, "MonoScript assets cannot be included in a data-only bundle.");
                    }
                    if (assembly.EndsWith(".Editor", StringComparison.OrdinalIgnoreCase) ||
                        (type.Namespace ?? string.Empty).StartsWith("KitchenLib.Studio.Editor", StringComparison.Ordinal))
                        Add(findings, "EditorSerializedType", assetName, "Bundle contains an editor-only serialized type.");
                    if (!assembly.StartsWith("UnityEngine", StringComparison.Ordinal) && !allowedAssemblies.Contains(assembly))
                        Add(findings, "DisallowedRuntimeAssembly", assetName, "Serialized type references disallowed assembly '" + assembly + "'.");

                    if (asset is TextAsset text)
                    {
                        string fileName = Path.GetFileName(normalized);
                        if (!declared.Contains(fileName)) Add(findings, "UndeclaredSidecar", assetName, "Text sidecar was not declared by the build.");
                        else
                        {
                            sidecarCounts[fileName]++;
                            if (string.Equals(fileName, GdoOverlayManifest.AssetName, StringComparison.Ordinal) && !IsValidManifest(text.text))
                                Add(findings, "MalformedManifest", assetName, "GDO overlay manifest is malformed or uses an unsupported schema.");
                        }
                    }
                    else if (!(asset is GameDataObject) &&
                        !IsSupportedRuntimeAsset(asset, normalized, prefabDependencies))
                    {
                        Add(findings, "UnexpectedSerializedAsset", assetName, "Only GDO assets and declared sidecars may be top-level serialized bundle assets.");
                    }
                }
            }
            finally { bundle.Unload(true); }
        }

        private static bool IsValidManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{') return false;
            try
            {
                ManifestShape manifest = JsonUtility.FromJson<ManifestShape>(json);
                return manifest != null && (manifest.schemaVersion == 2 || manifest.schemaVersion == GdoOverlayManifest.SchemaVersion) && manifest.targets != null;
            }
            catch (ArgumentException) { return false; }
        }

        private static bool IsSupportedRuntimeAsset(UnityEngine.Object asset, string assetPath,
            ISet<string> prefabDependencies) => asset is GameObject ||
            prefabDependencies.Contains(assetPath) &&
            (asset is Material || asset is Mesh || asset is Texture || asset is Shader);

        private static void Add(ICollection<GdoPackageAuditFinding> findings, string category, string path, string message) =>
            findings.Add(new GdoPackageAuditFinding(ValidationSeverity.Fatal, category, path, message));
    }
}
