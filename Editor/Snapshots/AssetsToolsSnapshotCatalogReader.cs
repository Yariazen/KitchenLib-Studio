using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class AssetsToolsSnapshotCatalogReader : IPlateUpSnapshotCatalogReader
    {
        private static readonly string[] Containers = { "globalgamemanagers.assets", "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "level0", "level1" };
        public SnapshotCatalogScanResult Scan(string gameRoot, IProgress<SnapshotProgress> progress, CancellationToken cancellationToken)
        {
            PlateUpPlatformLayout layout = PlateUpPlatformLayout.ResolveLayout(gameRoot);
            string dataRoot = layout.DataRoot;
            string classData = KitchenLibStudioGeneratedPaths.PackageFilePath(
                "Editor/ThirdParty/AssetsTools.NET/classdata.tpk");
            var drafts = new List<RecordDraft>(); var edges = new List<EdgeDraft>(); var diagnostics = new List<VanillaAssetDiagnosticData>();
            var sources = new List<VanillaAssetSourceData>();
            var manager = new AssetsManager();
            var mono = new MonoCecilTempGenerator(layout.ManagedDirectory);
            try
            {
                manager.LoadClassPackage(classData); manager.MonoTempGenerator = mono;
                for (int fileIndex = 0; fileIndex < Containers.Length; fileIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested(); string relative = Containers[fileIndex]; string path = Path.Combine(dataRoot, relative);
                    if (!File.Exists(path)) continue;
                    AssetsFileInstance instance = manager.LoadAssetsFile(path, false);
                    manager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);
                    sources.Add(new VanillaAssetSourceData { relativePath = relative, sha256 = Hash(path),
                        containerKind = relative.StartsWith("level", StringComparison.Ordinal) ? "level" : "assets", unityVersion = instance.file.Metadata.UnityVersion });
                    IList<AssetFileInfo> infos = instance.file.AssetInfos;
                    for (int index = 0; index < infos.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if ((index & 255) == 0) progress?.Report(new SnapshotProgress(relative, index, infos.Count));
                        AssetFileInfo info = infos[index]; VanillaAssetKind? kind = Kind(info.TypeId);
                        if (!kind.HasValue && info.TypeId != (int)AssetClassID.MonoBehaviour) continue;
                        try { ReadObject(manager, instance, relative, info, kind, drafts, edges); }
                        catch (Exception exception) { diagnostics.Add(new VanillaAssetDiagnosticData { severity = "warning", code = "object-read-failed", message = exception.Message, key = relative + ":" + info.PathId }); }
                    }
                    manager.UnloadAssetsFile(instance);
                }
            }
            finally { manager.UnloadAll(true); mono.Dispose(); }

            AssignKeys(drafts);
            var byIdentity = drafts.ToDictionary(item => Identity(item.RelativePath, item.PathId), StringComparer.OrdinalIgnoreCase);
            VanillaAssetEdgeData[] resolved = edges.Where(edge => byIdentity.ContainsKey(Identity(edge.TargetFile, edge.TargetPathId)))
                .Select(edge => new VanillaAssetEdgeData { sourceKey = byIdentity[Identity(edge.SourceFile, edge.SourcePathId)].Data.key,
                    relation = edge.Relation, slot = edge.Slot, targetKey = byIdentity[Identity(edge.TargetFile, edge.TargetPathId)].Data.key })
                .Distinct(new EdgeComparer()).ToArray();
            foreach (RecordDraft transform in drafts.Where(item => item.Data.kind == "transform" && item.ParentTransformId == 0))
            {
                EdgeDraft goEdge = edges.FirstOrDefault(edge => edge.TargetPathId == transform.PathId && edge.Relation == "component");
                if (goEdge != null && byIdentity.TryGetValue(Identity(goEdge.SourceFile, goEdge.SourcePathId), out RecordDraft go)) go.Data.hierarchyRoot = true;
            }
            var data = new VanillaAssetCatalogData { schemaVersion = 2, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceFingerprint = HashStrings(sources.Select(item => item.sha256)),
                sources = sources.ToArray(), records = drafts.Select(item => item.Data).ToArray(), edges = resolved, diagnostics = diagnostics.ToArray() };
            VanillaAssetCatalogFile.Validate(data); return new SnapshotCatalogScanResult(data);
        }

        private static void ReadObject(AssetsManager manager, AssetsFileInstance instance, string file, AssetFileInfo info,
            VanillaAssetKind? knownKind, ICollection<RecordDraft> records, ICollection<EdgeDraft> edges)
        {
            AssetTypeValueField fields = manager.GetBaseField(instance, info, AssetReadFlags.None);
            if (fields == null || fields.IsDummy) return;
            VanillaAssetKind kind; int gdoId = 0;
            if (knownKind.HasValue) kind = knownKind.Value;
            else
            {
                AssetTypeValueField id = fields["ID"];
                if (id == null || id.IsDummy || id.AsInt == 0) return;
                kind = VanillaAssetKind.Gdo; gdoId = id.AsInt;
            }
            string name = ReadName(fields, kind, info.PathId);
            var data = new VanillaAssetRecordData { displayName = name, kind = KindName(kind), structural = IsStructural(kind), gdoId = gdoId,
                source = new VanillaObjectIdentityData { relativePath = file, pathId = info.PathId, classId = info.TypeId, className = kind.ToString() }, metadata = Array.Empty<VanillaAssetMetadataData>() };
            var draft = new RecordDraft(file, info.PathId, kind, data); records.Add(draft);
            if (kind == VanillaAssetKind.GameObject)
            {
                int slot = 0; foreach (AssetTypeValueField item in ArrayChildren(fields["m_Component"])) AddPointer(edges, file, info.PathId, "component", (slot++).ToString(), item["component"] ?? item);
            }
            else if (kind == VanillaAssetKind.Transform)
            {
                AddPointer(edges, file, info.PathId, "gameObject", "", fields["m_GameObject"]);
                AssetTypeValueField father = fields["m_Father"]; draft.ParentTransformId = PointerPath(father);
                AddPointer(edges, file, info.PathId, "parent", "", father);
                int slot = 0; foreach (AssetTypeValueField child in ArrayChildren(fields["m_Children"])) AddPointer(edges, file, info.PathId, "child", (slot++).ToString(), child);
            }
            else if (kind == VanillaAssetKind.MeshFilter) AddPointer(edges, file, info.PathId, "mesh", "", fields["m_Mesh"]);
            else if (kind == VanillaAssetKind.Renderer)
            { int slot = 0; foreach (AssetTypeValueField item in ArrayChildren(fields["m_Materials"])) AddPointer(edges, file, info.PathId, "material", (slot++).ToString(), item); }
            else if (kind == VanillaAssetKind.Material) AddPointer(edges, file, info.PathId, "shader", "", fields["m_Shader"]);
            else if (kind == VanillaAssetKind.Gdo)
            {
                foreach (string field in new[] { "Prefab", "IconPrefab", "DisplayPrefab", "Visual", "_IconPrefab" })
                { AssetTypeValueField value = fields[field]; if (value != null && !value.IsDummy) AddPointer(edges, file, info.PathId, "gameObject", field, value); }
            }
        }

        private static void AddPointer(ICollection<EdgeDraft> edges, string file, long source, string relation, string slot, AssetTypeValueField pointer)
        { long target = PointerPath(pointer); if (target != 0 && PointerFile(pointer) == 0) edges.Add(new EdgeDraft(file, source, file, target, relation, slot)); }
        private static long PointerPath(AssetTypeValueField pointer) { AssetTypeValueField field = pointer?["m_PathID"]; return field == null ? 0 : field.AsLong; }
        private static int PointerFile(AssetTypeValueField pointer) { AssetTypeValueField field = pointer?["m_FileID"]; return field == null ? 0 : field.AsInt; }
        private static IEnumerable<AssetTypeValueField> ArrayChildren(AssetTypeValueField field)
        { if (field?.Children == null) return Enumerable.Empty<AssetTypeValueField>(); AssetTypeValueField array = field.Children.FirstOrDefault(child => child.FieldName == "Array"); return array?.Children ?? field.Children; }
        private static string ReadName(AssetTypeValueField fields, VanillaAssetKind kind, long pathId)
        { AssetTypeValueField name = fields["m_Name"]; return name == null || name.IsDummy || string.IsNullOrEmpty(name.AsString) ? kind + " " + pathId : name.AsString; }
        private static bool IsStructural(VanillaAssetKind kind) => kind == VanillaAssetKind.Transform || kind == VanillaAssetKind.MeshFilter || kind == VanillaAssetKind.Renderer;
        private static VanillaAssetKind? Kind(int id)
        {
            if (id == (int)AssetClassID.GameObject) return VanillaAssetKind.GameObject;
            if (id == (int)AssetClassID.Transform || id == (int)AssetClassID.RectTransform) return VanillaAssetKind.Transform;
            if (id == (int)AssetClassID.MeshFilter) return VanillaAssetKind.MeshFilter;
            if (id == (int)AssetClassID.MeshRenderer || id == (int)AssetClassID.SkinnedMeshRenderer || id == (int)AssetClassID.SpriteRenderer) return VanillaAssetKind.Renderer;
            if (id == (int)AssetClassID.Material) return VanillaAssetKind.Material;
            if (id == (int)AssetClassID.Shader) return VanillaAssetKind.Shader;
            if (id == (int)AssetClassID.Texture2D) return VanillaAssetKind.Texture2D;
            if (id == (int)AssetClassID.Cubemap) return VanillaAssetKind.Cubemap;
            if (id == (int)AssetClassID.Sprite) return VanillaAssetKind.Sprite;
            if (id == (int)AssetClassID.Mesh) return VanillaAssetKind.Mesh;
            return null;
        }
        private static string KindName(VanillaAssetKind kind) => kind == VanillaAssetKind.GameObject ? "gameObject" : char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString().Substring(1);
        private static void AssignKeys(IList<RecordDraft> records)
        {
            foreach (RecordDraft item in records)
                item.Data.key = VanillaAssetKey.Generate("plateup", item.Kind, item.Data.displayName, Array.Empty<string>(), item.PathId);

            // Distinct Unity names can collapse to the same slug (for example punctuation-only
            // differences). Resolve collisions after canonicalization rather than before it.
            foreach (IGrouping<string, RecordDraft> group in records.GroupBy(item => item.Data.key, StringComparer.Ordinal))
            {
                if (group.Count() == 1) continue;
                foreach (RecordDraft item in group.OrderBy(value => value.RelativePath, StringComparer.Ordinal).ThenBy(value => value.PathId))
                    item.Data.key = VanillaAssetKey.Generate("plateup", item.Kind, item.Data.displayName,
                        new[] { Path.GetFileNameWithoutExtension(item.RelativePath) + "-" + item.PathId.ToString("x") }, item.PathId);
            }
        }
        private static string Identity(string file, long pathId) => file.Replace('\\', '/') + "\n" + pathId;
        private static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        private static string HashStrings(IEnumerable<string> values) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", values)))).Replace("-", ""); }
        private sealed class RecordDraft { public RecordDraft(string file,long id,VanillaAssetKind kind,VanillaAssetRecordData data){RelativePath=file;PathId=id;Kind=kind;Data=data;} public string RelativePath; public long PathId; public VanillaAssetKind Kind; public VanillaAssetRecordData Data; public long ParentTransformId; }
        private sealed class EdgeDraft { public EdgeDraft(string sf,long sid,string tf,long tid,string r,string s){SourceFile=sf;SourcePathId=sid;TargetFile=tf;TargetPathId=tid;Relation=r;Slot=s;} public string SourceFile,TargetFile,Relation,Slot; public long SourcePathId,TargetPathId; }
        private sealed class EdgeComparer : IEqualityComparer<VanillaAssetEdgeData> { public bool Equals(VanillaAssetEdgeData x,VanillaAssetEdgeData y)=>x.sourceKey==y.sourceKey&&x.relation==y.relation&&x.slot==y.slot&&x.targetKey==y.targetKey; public int GetHashCode(VanillaAssetEdgeData o)=>(o.sourceKey+"\n"+o.relation+"\n"+o.slot+"\n"+o.targetKey).GetHashCode(); }
    }
}
