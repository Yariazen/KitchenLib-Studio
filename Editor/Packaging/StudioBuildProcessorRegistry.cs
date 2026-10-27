using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenData;
using KitchenLib.Studio.Editor.Api;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Build;
using KitchenLib.Studio.Editor.Api.Graph;
using KitchenLib.Studio.Editor.Api.Validation;
using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Graph;
using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Packaging
{
    [InitializeOnLoad]
    internal static class StudioBuildProcessorRegistry
    {
        private sealed class Entry
        {
            internal string Id;
            internal int Order;
            internal IStudioBuildProcessor Processor;
        }
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static bool registering;
        private static bool processing;
        private static int buildDepth;
        private sealed class BuildScope : IDisposable
        {
            internal BuildScope() { buildDepth++; }
            public void Dispose() { buildDepth--; }
        }
        static StudioBuildProcessorRegistry() => AssemblyReloadEvents.beforeAssemblyReload += Reset;
        internal static bool HasProcessors => Entries.Count != 0;
        internal static IDisposable BeginBuild() => new BuildScope();

        internal static void Register(IStudioBuildProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            if (registering || processing || buildDepth != 0)
                throw new InvalidOperationException("Processor registration cannot be reentrant or occur during a build.");
            registering = true;
            try
            {
                string id = processor.Id;
                int order = processor.Order;
                if (id == null || id.Length > 256 || !Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9._-]*:[A-Za-z0-9][A-Za-z0-9._/-]*\z"))
                    throw new ArgumentException("Processor IDs must be namespaced identifiers of at most 256 characters.", nameof(processor));
                if (Entries.ContainsKey(id)) throw new ArgumentException("Duplicate build processor ID: " + id, nameof(processor));
                if (Entries.Count >= 128) throw new ArgumentException("At most 128 build processors are supported.", nameof(processor));
                Entries.Add(id, new Entry { Id = id, Order = order, Processor = processor });
            }
            finally { registering = false; }
        }

        internal static void Process(StudioBuildPreparation preparation, string productId, ICollection<ValidationIssue> issues)
        {
            if (processing) { issues.Add(Error("BuildProcessors", "Reentrant processor execution is not supported.")); return; }
            processing = true;
            try
            {
                foreach (Entry entry in Entries.Values.OrderBy(value => value.Order).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray())
                {
                    try { preparation.Apply(entry.Processor.Process(preparation.Context(productId)), entry.Id, issues); }
                    catch (Exception) { issues.Add(Error("BuildProcessor:" + entry.Id,
                        "Build processor failed or returned invalid, unresolved or oversized output.")); }
                }
            }
            finally { processing = false; }
        }

        internal static ValidationIssue Error(string category, string message) =>
            new ValidationIssue(ValidationSeverity.Error, string.Empty, category, message);
        private static void Reset() { Entries.Clear(); registering = false; processing = false; buildDepth = 0; }
    }

    // Unity's bundle builder also requests a global save. Restrict that request to the product's
    // already journaled build inputs; explicit authoring saves use SaveAssetIfDirty instead.
    internal sealed class StudioBuildSaveGuard : UnityEditor.AssetModificationProcessor
    {
        private static HashSet<string> allowedPaths;
        private sealed class Scope : IDisposable
        {
            private readonly HashSet<string> previous;
            internal Scope(IEnumerable<string> paths)
            {
                previous = allowedPaths;
                allowedPaths = new HashSet<string>(paths, StringComparer.Ordinal);
            }
            public void Dispose() { allowedPaths = previous; }
        }
        internal static IDisposable RestrictTo(IEnumerable<string> paths) => new Scope(paths);
        private static string[] OnWillSaveAssets(string[] paths) => allowedPaths == null ? paths :
            paths.Where(path => allowedPaths.Contains(path)).ToArray();
    }

    // No authored Unity object is handed to an extension. Only these disposable copies receive changes.
    internal sealed class StudioBuildPreparation : IDisposable
    {
        private readonly Dictionary<string, GameDataObject> assets;
        private GdoOverlayAuthoringAsset[] companions;
        private Dictionary<string, StudioAuthoringValue> typedValues;
        internal IReadOnlyList<GdoOverlayAuthoringAsset> Companions => companions;

        internal StudioBuildPreparation(IEnumerable<GameDataObject> selected, IEnumerable<GdoOverlayAuthoringAsset> authored)
        {
            assets = selected.ToDictionary(asset => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)), StringComparer.Ordinal);
            companions = Copy(authored);
        }

        internal StudioBuildContext Context(string productId)
        {
            if (assets.Count > 1024) throw new InvalidOperationException();
            var nodes = new List<StudioGraphNodeData>();
            var snapshotValues = typedValues ?? new Dictionary<string, StudioAuthoringValue>(StringComparer.Ordinal);
            int budget = 1048576;
            int fields = 0;
            Text(productId, ref budget);
            foreach (var asset in assets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Text(asset.Key, ref budget);
                var values = new List<KeyValuePair<string, StudioAuthoringValue>>();
                GdoOverlayAuthoringAsset companion = companions.SingleOrDefault(item => item.Target == asset.Value);
                if (companion != null)
                    foreach (GdoOverlayField field in companion.Fields)
                    {
                        string identity = asset.Key + "\0" + Key(field);
                        StudioAuthoringValue value;
                        bool available = typedValues == null ? TryValue(field.Value, out value) : typedValues.TryGetValue(identity, out value);
                        if (available)
                        {
                            if (typedValues == null) snapshotValues.Add(identity, value);
                            string key = Key(field);
                            Text(key, ref budget);
                            Value(value, ref budget);
                            values.Add(new KeyValuePair<string, StudioAuthoringValue>(key, value));
                            if (values.Count > 256 || ++fields > 4096) throw new InvalidOperationException();
                        }
                    }
                nodes.Add(new StudioGraphNodeData(asset.Key, KitchenLibStudioApi.GdoReferenceSemanticTypeId,
                    values, Array.Empty<StudioGraphConnection>()));
            }
            typedValues = snapshotValues;
            return new StudioBuildContext(productId, nodes);
        }

        internal void Apply(StudioBuildProcessorResult result, string id, ICollection<ValidationIssue> issues)
        {
            if (result == null) throw new InvalidOperationException();
            int budget = 1048576;
            var findings = new List<ValidationIssue>();
            foreach (StudioValidationFinding finding in result.Findings)
            {
                Text(finding.Code, ref budget); Text(finding.Message, ref budget);
                Text(finding.NodeId, ref budget); Text(finding.FieldKey, ref budget); Text(finding.PortKey, ref budget);
                findings.Add(new ValidationIssue(finding.Severity == StudioValidationSeverity.Error ? ValidationSeverity.Error :
                    finding.Severity == StudioValidationSeverity.Warning ? ValidationSeverity.Warning : ValidationSeverity.Info,
                    string.Empty, "BuildFinding:" + id + ":" + finding.Code, finding.Message));
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pendingValues = new Dictionary<string, StudioAuthoringValue>(typedValues, StringComparer.Ordinal);
            var pending = Copy(companions);
            try
            {
                foreach (StudioBuildFieldChange change in result.Changes)
                {
                    Text(change.NodeId, ref budget); Text(change.FieldKey, ref budget); Value(change.Value, ref budget);
                    if (!assets.TryGetValue(change.NodeId, out GameDataObject target) ||
                        !seen.Add(change.NodeId + "\0" + change.FieldKey)) throw new InvalidOperationException();
                    GdoOverlayAuthoringAsset companion = pending.SingleOrDefault(item => item.Target == target);
                    GdoOverlayField field = companion?.Fields.SingleOrDefault(item => Key(item) == change.FieldKey);
                    string identity = change.NodeId + "\0" + change.FieldKey;
                    if (field == null || !typedValues.TryGetValue(identity, out StudioAuthoringValue previous) || previous.Kind != change.Value.Kind)
                        throw new InvalidOperationException();
                    companion.UpsertField(field.DeclaringType, field.FieldName, Replace(field.Value, change.Value));
                    pendingValues[identity] = change.Value;
                }
                // Schema/required-field checks remain mandatory after all bounded replacements.
                foreach (GdoOverlayAuthoringAsset companion in pending)
                    findings.AddRange(GdoDescriptorValueValidator.Validate(companion.Target, companion,
                        GdoDescriptorCatalog.ForTarget(companion.Target)));
                foreach (ValidationIssue finding in findings) issues.Add(finding);
                if (findings.Any(finding => finding.Severity == ValidationSeverity.Error || finding.Severity == ValidationSeverity.Fatal)) return;
                Destroy(companions);
                companions = pending;
                // Keep the accepted semantic value, rather than reparsing its serialized scalar text.
                // In particular, Number(2.0) formats as "2" but must never become Integer(2).
                typedValues = pendingValues;
                pending = null;
            }
            finally { Destroy(pending); }
        }

        private bool TryValue(GdoOverlayValue value, out StudioAuthoringValue result)
        {
            result = null;
            switch (value.Kind)
            {
                case GdoOverlayValueKind.String: result = StudioAuthoringValue.FromString(value.ScalarValue); break;
                case GdoOverlayValueKind.Enum:
                    if (long.TryParse(value.ScalarValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ordinal))
                        result = StudioAuthoringValue.FromInteger(ordinal);
                    break;
                case GdoOverlayValueKind.Scalar:
                    if (bool.TryParse(value.ScalarValue, out bool flag)) result = StudioAuthoringValue.FromBoolean(flag);
                    else if (long.TryParse(value.ScalarValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                        result = StudioAuthoringValue.FromInteger(integer);
                    else if (double.TryParse(value.ScalarValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
                        !double.IsInfinity(number) && !double.IsNaN(number)) result = StudioAuthoringValue.FromNumber(number);
                    break;
                case GdoOverlayValueKind.GdoReference:
                    if (value.ReferenceValue.Asset != null && assets.ContainsValue(value.ReferenceValue.Asset))
                        result = StudioAuthoringValue.FromReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value.ReferenceValue.Asset)));
                    break;
            }
            return result != null;
        }

        private GdoOverlayValue Replace(GdoOverlayValue original, StudioAuthoringValue value)
        {
            if (original.Kind == GdoOverlayValueKind.GdoReference)
            {
                if (!assets.TryGetValue(value.AsReference(), out GameDataObject target)) throw new InvalidOperationException();
                return GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(target, original.ReferenceValue.ExpectedType));
            }
            if (original.Kind == GdoOverlayValueKind.String) return GdoOverlayValue.String(value.AsString());
            if (original.Kind == GdoOverlayValueKind.Enum) return GdoOverlayValue.Enum(value.AsInteger());
            string scalar = value.Kind == StudioAuthoringValueKind.Boolean ? (value.AsBoolean() ? "true" : "false") :
                value.Kind == StudioAuthoringValueKind.Integer ? value.AsInteger().ToString(CultureInfo.InvariantCulture) :
                value.AsNumber().ToString("R", CultureInfo.InvariantCulture);
            return GdoOverlayValue.Scalar(scalar);
        }

        private static string Key(GdoOverlayField field) => field.DeclaringType + "|" + field.FieldName;
        private static void Value(StudioAuthoringValue value, ref int budget)
        {
            if (value.Kind == StudioAuthoringValueKind.String) Text(value.AsString(), ref budget);
            if (value.Kind == StudioAuthoringValueKind.Reference) Text(value.AsReference(), ref budget);
        }
        private static void Text(string value, ref int budget)
        {
            int length = value?.Length ?? 0;
            budget -= length;
            if (length > 65536 || budget < 0) throw new InvalidOperationException();
        }
        private static GdoOverlayAuthoringAsset[] Copy(IEnumerable<GdoOverlayAuthoringAsset> source)
        {
            var copies = new List<GdoOverlayAuthoringAsset>();
            try
            {
                foreach (GdoOverlayAuthoringAsset item in source) copies.Add(UnityEngine.Object.Instantiate(item));
                return copies.ToArray();
            }
            catch { Destroy(copies); throw; }
        }
        private static void Destroy(IEnumerable<GdoOverlayAuthoringAsset> copies)
        {
            if (copies != null) foreach (GdoOverlayAuthoringAsset copy in copies) UnityEngine.Object.DestroyImmediate(copy);
        }
        public void Dispose() { Destroy(companions); companions = Array.Empty<GdoOverlayAuthoringAsset>(); }
    }

    // The existing graph apply stage precedes packaging. Restore its authored state on success and failure
    // when processors are active, including original on-disk bytes and unsaved editor values.
    internal sealed class StudioBuildAuthoringJournal : IDisposable
    {
        private readonly GdoGraphCompanionJournal companions;
        private readonly List<Action> restore = new List<Action>();
        private readonly HashSet<string> missingFolders = new HashSet<string>(StringComparer.Ordinal);
        internal StudioBuildAuthoringJournal(GdoBundleGraphAsset graph)
        {
            companions = new GdoGraphCompanionJournal(graph);
            foreach (GameDataObject asset in graph.Nodes.Where(node => !node.IsBroken && node.Kind == GdoGraphNodeKind.Local)
                .Select(node => node.Asset).Distinct())
            {
                Capture(asset);
                TrackFolders(GdoOverlayAuthoringRepository.ExistingPath(asset));
            }
            Capture(graph);
            foreach (GdoGraphPrefabRecord prefab in graph.Prefabs.Where(item => item.SourceMode == GdoGraphPrefabSourceMode.Primitive))
            {
                CaptureGeneratedFile(GdoGraphPrefabResolver.PrimitiveAssetPath(graph, prefab));
                if (prefab.MaterialMode == GdoGraphPrefabMaterialMode.GeneratedMaterial)
                    CaptureGeneratedFile(GdoGraphMaterialResolver.AssetPath(graph, prefab));
            }
        }
        private void CaptureGeneratedFile(string path)
        {
            TrackFolders(path);
            byte[] bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            restore.Insert(0, () =>
            {
                if (bytes == null) { if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path); }
                else if (!File.ReadAllBytes(path).SequenceEqual(bytes))
                {
                    File.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
            });
        }
        private void TrackFolders(string path)
        {
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/", StringComparison.Ordinal) && !Directory.Exists(folder))
            {
                missingFolders.Add(folder);
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }
        }
        private void Capture(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            byte[] bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            string json = EditorJsonUtility.ToJson(asset);
            bool dirty = EditorUtility.IsDirty(asset);
            restore.Add(() =>
            {
                if (bytes != null && !File.ReadAllBytes(path).SequenceEqual(bytes))
                {
                    File.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
                // BuildPipeline may unload unused persistent objects even while managed references exist.
                UnityEngine.Object current = bytes == null ? asset : AssetDatabase.LoadMainAssetAtPath(path);
                EditorJsonUtility.FromJsonOverwrite(json, current);
                if (dirty) EditorUtility.SetDirty(current); else EditorUtility.ClearDirty(current);
            });
        }
        public void Dispose()
        {
            var failures = new List<Exception>();
            try { companions.Restore(); } catch (Exception failure) { failures.Add(failure); }
            foreach (Action action in restore) try { action(); } catch (Exception failure) { failures.Add(failure); }
            foreach (string folder in missingFolders.OrderByDescending(value => value.Length))
                try
                {
                    if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
                }
                catch (Exception failure) { failures.Add(failure); }
            if (failures.Count != 0) throw new AggregateException("Build authoring rollback failed.", failures);
        }
    }
}
