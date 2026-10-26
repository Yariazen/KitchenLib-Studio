using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

namespace KitchenLib.Studio.Editor
{
    // Find can migrate and save legacy YAML, so Undo alone cannot restore companion data.
    internal sealed class GdoGraphCompanionJournal
    {
        private readonly List<Action> _restore = new List<Action>();

        public GdoGraphCompanionJournal(GdoBundleGraphAsset graph)
        {
            foreach (var node in graph.Nodes.Where(n => n.Kind == GdoGraphNodeKind.Local && !n.IsBroken))
            {
                string path = GdoOverlayAuthoringRepository.ExistingPath(node.Asset);
                var companion = AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(path);
                if (companion == null)
                {
                    _restore.Add(() =>
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
                            throw new InvalidOperationException("Could not remove new companion: " + path);
                    });
                    continue;
                }
                string absolute = Path.Combine(KitchenLibStudioGeneratedPaths.ProjectRoot, path);
                byte[] bytes = File.ReadAllBytes(absolute);
                string json = EditorJsonUtility.ToJson(companion);
                bool dirty = EditorUtility.IsDirty(companion);
                _restore.Add(() =>
                {
                    File.WriteAllBytes(absolute, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    var restored = AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(path);
                    EditorJsonUtility.FromJsonOverwrite(json, restored);
                    if (dirty) EditorUtility.SetDirty(restored);
                    else EditorUtility.ClearDirty(restored);
                });
            }
        }

        public void Restore()
        {
            var failures = new List<Exception>();
            foreach (Action restore in _restore)
                try { restore(); } catch (Exception failure) { failures.Add(failure); }
            if (failures.Count != 0) throw new AggregateException("Companion rollback failed.", failures);
        }
    }
}
