using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

using KitchenLib.Studio.Editor.Graph;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Packaging
{
    internal sealed class GdoRenamePreview
    {
        public GdoBundleGraphAsset Graph { get; }
        public string NodeId { get; }
        public string OldAssetPath { get; }
        public string NewAssetPath { get; }
        public string OldName { get; }
        public string NewName { get; }
        public int OldId { get; }
        public int NewId { get; }
        public string Warning => "Renaming changes the display name and asset path and clears the Unity editor Undo/Redo history.";
        internal string GraphGuid { get; }
        internal string AssetGuid { get; }
        internal string Namespace { get; }

        internal GdoRenamePreview(GdoBundleGraphAsset graph, GdoGraphNodeRecord node, string newName, string newPath)
        {
            Graph = graph;
            NodeId = node.Id;
            GraphGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            AssetGuid = node.AssetGuid;
            Namespace = graph.BundleName;
            OldAssetPath = AssetDatabase.GetAssetPath(node.Asset);
            NewAssetPath = newPath;
            OldName = node.Asset.name;
            NewName = newName;
            OldId = node.Asset.ID;
            NewId = OldId;
        }
    }

    internal sealed class GdoProjectRenameIdChange
    {
        public string AssetGuid { get; }
        public string AssetName { get; }
        public int OldId { get; }
        public int NewId { get; }

        internal GdoProjectRenameIdChange(GdoGraphNodeRecord node, int newId)
        {
            AssetGuid = node.AssetGuid;
            AssetName = node.Asset.name;
            OldId = node.Asset.ID;
            NewId = newId;
        }
    }

    internal sealed class GdoProjectRenamePreview
    {
        public string OldNamespace { get; }
        public string NewNamespace { get; }
        public string OldProjectPath { get; }
        public string NewProjectPath { get; }
        public int AffectedGdoCount => GdoChanges.Count;
        public IReadOnlyList<GdoProjectRenameIdChange> GdoChanges { get; }
        public string Warning => "Renaming changes project and bundle metadata and clears the Unity editor Undo/Redo history.";
        internal string GraphGuid { get; }

        internal GdoProjectRenamePreview(string graphGuid, string oldNamespace, string newNamespace,
            string oldPath, string newPath, IEnumerable<GdoProjectRenameIdChange> changes)
        {
            GraphGuid = graphGuid;
            OldNamespace = oldNamespace;
            NewNamespace = newNamespace;
            OldProjectPath = oldPath;
            NewProjectPath = newPath;
            GdoChanges = Array.AsReadOnly(changes.ToArray());
        }
    }

    internal static class GdoBundleProjectRepository
    {
        public const string DefaultRoot = "Assets/ModContent";

        public static void EnsureDefaultRoot()
        {
            if (AssetDatabase.IsValidFolder(DefaultRoot)) return;
            if (AssetDatabase.LoadMainAssetAtPath(DefaultRoot) != null)
                throw new InvalidOperationException("Cannot create the ModContent folder because an asset already exists at '" + DefaultRoot + "'.");

            string guid = AssetDatabase.CreateFolder("Assets", "ModContent");
            string createdPath = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(createdPath, DefaultRoot, StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(DefaultRoot))
                throw new InvalidOperationException("Could not create the required project folder '" + DefaultRoot + "'.");
        }

        public static GdoRenamePreview PreviewGdoRename(GdoBundleGraphAsset graph, string nodeId, string newName)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            GdoProjectIdentity.ValidateName(newName, nameof(newName));
            if (ValidateLayout(graph).Count != 0)
                throw new ArgumentException("Graph must have a valid bundle project layout.", nameof(graph));
            GdoGraphNodeRecord node = graph.Nodes.SingleOrDefault(value => value.Id == nodeId);
            if (node == null || node.Kind != GdoGraphNodeKind.Local || node.IsBroken)
                throw new ArgumentException("Only a resolved local GDO can be renamed.", nameof(nodeId));
            string oldPath = AssetDatabase.GetAssetPath(node.Asset);
            if (!oldPath.StartsWith(GdoFolder(graph) + "/", StringComparison.Ordinal) ||
                AssetDatabase.AssetPathToGUID(oldPath) != node.AssetGuid ||
                AssetDatabase.LoadMainAssetAtPath(oldPath) != node.Asset)
                throw new ArgumentException("GDO must be a main asset inside this project's GDOs folder.", nameof(nodeId));

            if (graph.Nodes.Any(other => other.Id != nodeId &&
                other.Kind == GdoGraphNodeKind.Local && !other.IsBroken &&
                string.Equals(other.Asset.name.Trim(), newName.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Renamed GDO would duplicate a local name.", nameof(newName));
            string newPath = Path.GetDirectoryName(oldPath).Replace('\\', '/') + "/" + newName + ".asset";
            RequireAvailablePath(newPath, oldPath);
            return new GdoRenamePreview(graph, node, newName, newPath);
        }

        public static KitchenData.GameDataObject RenameGdo(GdoRenamePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            GdoRenamePreview current = PreviewGdoRename(preview.Graph, preview.NodeId, preview.NewName);
            if (current.GraphGuid != preview.GraphGuid || current.AssetGuid != preview.AssetGuid ||
                current.Namespace != preview.Namespace || current.OldAssetPath != preview.OldAssetPath ||
                current.NewAssetPath != preview.NewAssetPath || current.OldName != preview.OldName ||
                current.OldId != preview.OldId || current.NewId != preview.NewId)
                throw new ArgumentException("GDO changed since preview; preview the rename again.", nameof(preview));

            GdoBundleGraphAsset graph = current.Graph;
            GdoGraphNodeRecord node = graph.Nodes.Single(value => value.Id == current.NodeId);
            KitchenData.GameDataObject asset = node.Asset;
            var rollback = new Stack<Action>();
            var companions = new GdoGraphCompanionJournal(graph);
            JournalSnapshot(graph, rollback);
            JournalSnapshot(asset, rollback);
            try
            {
                RenameWithJournal(current.OldAssetPath, current.NewAssetPath, rollback);
                asset.name = current.NewName;
                node.RefreshCachedMetadata();
                EditorUtility.SetDirty(asset);
                EditorUtility.SetDirty(graph);

                if (!GdoGraphProjectionApplier.TryApply(graph, out IReadOnlyList<GdoGraphDiagnostic> diagnostics, true, true, false))
                    throw new InvalidOperationException("Could not apply renamed GDO relationships: " +
                        string.Join("; ", diagnostics.Select(issue => issue.Message)));
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(current.NewAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                asset = AssetDatabase.LoadAssetAtPath<KitchenData.GameDataObject>(current.NewAssetPath);
                if (asset == null) throw new InvalidOperationException("Could not reload renamed GDO.");
                EstablishRenameUndoBoundary();
                return asset;
            }
            catch (Exception failure)
            {
                RollbackOrRethrow(rollback, companions, failure,
                    "GDO rename failed and rollback encountered errors.");
                throw;
            }
        }

        private static void EstablishRenameUndoBoundary()
        {
            // ClearUndo(object) does not remove created-object records in Unity 2020.3.
            // Those can destroy a companion after a rename even after clearing its snapshots.
            // The confirmation explicitly announces this editor-wide filesystem Undo boundary.
            Undo.ClearAll();
        }

        private static void JournalSnapshot(UnityEngine.Object asset, Stack<Action> rollback)
        {
            string snapshot = EditorJsonUtility.ToJson(asset);
            rollback.Push(() =>
            {
                EditorJsonUtility.FromJsonOverwrite(snapshot, asset);
                EditorUtility.SetDirty(asset);
            });
        }

        private static void RenameWithJournal(string source, string destination, Stack<Action> rollback)
        {
            if (source == destination) return;
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                string temporary = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(source).Replace('\\', '/') +
                    "/__gdo_rename_" + Guid.NewGuid().ToString("N") + ".asset");
                RenameWithJournal(source, temporary, rollback);
                RenameWithJournal(temporary, destination, rollback);
                return;
            }
            RenameAsset(source, destination);
            rollback.Push(() => RenameAsset(destination, source));
        }

        private static void RenameAsset(string source, string destination)
        {
            string error = AssetDatabase.RenameAsset(source, Path.GetFileNameWithoutExtension(destination));
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not rename '" + source + "' to '" + destination + "': " + error);
        }

        public static GdoProjectRenamePreview PreviewProjectRename(GdoBundleGraphAsset graph, string newName)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            GdoProjectIdentity.ValidateName(newName, nameof(newName));
            if (ValidateLayout(graph).Count != 0)
                throw new ArgumentException("Graph must have a valid bundle project layout.", nameof(graph));

            string oldPath = ProjectFolder(graph);
            string newPath = Path.GetDirectoryName(oldPath).Replace('\\', '/') + "/" + newName;
            RequireAvailablePath(newPath, oldPath);
            RequireAvailablePath(oldPath + "/" + newName + ".asset", AssetDatabase.GetAssetPath(graph));

            var changes = new List<GdoProjectRenameIdChange>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<int>(graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.External).Select(node => node.ExternalId));
            foreach (GdoGraphNodeRecord node in graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local))
            {
                if (node.IsBroken || !AssetDatabase.GetAssetPath(node.Asset).StartsWith(GdoFolder(graph) + "/", StringComparison.Ordinal) ||
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(node.Asset)) != node.AssetGuid)
                    throw new ArgumentException("Every local GDO must resolve inside this project's GDOs folder.", nameof(graph));
                string canonical = node.Asset.name.Trim().ToLowerInvariant();
                if (!names.Add(canonical) || !ids.Add(node.Asset.ID))
                    throw new ArgumentException("Project contains duplicate GDO names or colliding runtime IDs.", nameof(newName));
                changes.Add(new GdoProjectRenameIdChange(node, node.Asset.ID));
            }
            return new GdoProjectRenamePreview(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph)),
                graph.BundleName, newName, oldPath, newPath, changes);
        }

        public static GdoBundleGraphAsset RenameProject(GdoBundleGraphAsset graph, GdoProjectRenamePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            // Recheck both filesystem conflicts and identity inputs after confirmation.
            GdoProjectRenamePreview current = PreviewProjectRename(graph, preview.NewNamespace);
            if (current.GraphGuid != preview.GraphGuid || current.OldNamespace != preview.OldNamespace ||
                current.OldProjectPath != preview.OldProjectPath || current.NewProjectPath != preview.NewProjectPath ||
                !current.GdoChanges.Select(ChangeKey).SequenceEqual(preview.GdoChanges.Select(ChangeKey)))
                throw new ArgumentException("Project changed since preview; preview the rename again.", nameof(preview));
            if (current.OldNamespace == current.NewNamespace) return graph;

            var rollback = new Stack<Action>();
            var companions = new GdoGraphCompanionJournal(graph);
            GdoBundleGraphAsset originalGraph = graph;
            string graphSnapshot = EditorJsonUtility.ToJson(graph);
            rollback.Push(() =>
            {
                EditorJsonUtility.FromJsonOverwrite(graphSnapshot, originalGraph);
                EditorUtility.SetDirty(originalGraph);
            });
            try
            {
                MoveWithJournal(current.OldProjectPath, current.NewProjectPath, rollback);
                graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(AssetDatabase.GUIDToAssetPath(current.GraphGuid));
                if (graph == null) throw new InvalidOperationException("Could not reload moved project graph.");
                MoveWithJournal(AssetDatabase.GetAssetPath(graph), current.NewProjectPath + "/" + current.NewNamespace + ".asset", rollback);
                graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(AssetDatabase.GUIDToAssetPath(current.GraphGuid));
                if (graph == null) throw new InvalidOperationException("Could not reload renamed project graph.");

                graph.name = current.NewNamespace;
                graph.SetIdentity(current.NewNamespace, GdoProjectIdentity.BundleFileName(current.NewNamespace));
                EditorUtility.SetDirty(graph);
                if (!GdoGraphProjectionApplier.TryApply(graph, out var diagnostics, true, true, false))
                    throw new InvalidOperationException("Could not apply renamed project relationships: " +
                        string.Join("; ", diagnostics.Select(issue => issue.Message)));
                AssetDatabase.SaveAssets();
                EstablishRenameUndoBoundary();
                return graph;
            }
            catch (Exception failure)
            {
                RollbackOrRethrow(rollback, companions, failure,
                    "Project rename failed and rollback encountered errors.");
                throw;
            }
        }

        private static void RollbackOrRethrow(Stack<Action> rollback, GdoGraphCompanionJournal companions,
            Exception failure, string aggregateMessage)
        {
            var failures = new List<Exception> { failure };
            while (rollback.Count != 0)
            {
                try { rollback.Pop()(); }
                catch (Exception rollbackFailure) { failures.Add(rollbackFailure); }
            }
            try { AssetDatabase.SaveAssets(); }
            catch (Exception saveFailure) { failures.Add(saveFailure); }
            try { companions.Restore(); }
            catch (Exception fileFailure) { failures.Add(fileFailure); }
            if (failures.Count > 1) throw new AggregateException(aggregateMessage, failures);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static string ChangeKey(GdoProjectRenameIdChange change) =>
            change.AssetGuid + ":" + change.AssetName + ":" + change.OldId + ":" + change.NewId;

        private static void RequireAvailablePath(string destination, string source)
        {
            string parent = Path.GetDirectoryName(destination).Replace('\\', '/');
            string absoluteParent = Path.Combine(KitchenLibStudioGeneratedPaths.ProjectRoot, parent);
            string name = Path.GetFileName(destination);
            string sourceName = Path.GetFileName(source);
            if (Directory.EnumerateFileSystemEntries(absoluteParent).Any(path =>
                (string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetFileName(path), name + ".meta", StringComparison.OrdinalIgnoreCase)) &&
                !string.Equals(Path.GetFileName(path), sourceName, StringComparison.Ordinal) &&
                !string.Equals(Path.GetFileName(path), sourceName + ".meta", StringComparison.Ordinal)))
                throw new ArgumentException("The rename destination is already occupied: " + destination);
        }

        private static void MoveWithJournal(string source, string destination, Stack<Action> rollback)
        {
            if (source == destination) return;
            // Unity/Windows may treat a case-only move as the same path; use an intermediate sibling.
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                string temporary = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(source).Replace('\\', '/') +
                    "/__project_rename_" + Guid.NewGuid().ToString("N") + Path.GetExtension(source));
                MoveWithJournal(source, temporary, rollback);
                MoveWithJournal(temporary, destination, rollback);
                return;
            }
            Move(source, destination);
            rollback.Push(() => Move(destination, source));
        }

        private static void Move(string source, string destination)
        {
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not move '" + source + "' to '" + destination + "': " + error);
        }

        public static GdoBundleGraphAsset Create(string parentFolder, string authorNamespace, string projectName)
        {
            GdoProjectIdentity.ValidateName(authorNamespace, nameof(authorNamespace));
            GdoProjectIdentity.ValidateName(projectName, nameof(projectName));
            string parent = parentFolder?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(parent) || (parent != "Assets" && !parent.StartsWith("Assets/", StringComparison.Ordinal)) ||
                !AssetDatabase.IsValidFolder(parent))
                throw new ArgumentException("Parent must be an existing folder under Assets.", nameof(parentFolder));

            string project = parent + "/" + projectName;
            string absoluteParent = Path.Combine(KitchenLibStudioGeneratedPaths.ProjectRoot, parent);
            if (Directory.EnumerateFileSystemEntries(absoluteParent).Any(path =>
                string.Equals(Path.GetFileName(path), projectName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A project path with this name already exists.", nameof(projectName));

            var created = new List<string>();
            try
            {
                CreateFolder(parent, projectName, created);
                CreateFolder(project, "GDOs", created);
                CreateFolder(project, "Content", created);
                string graphPath = project + "/" + projectName + ".asset";
                created.Add(graphPath);
                return GdoBundleGraphRepository.Create(graphPath, authorNamespace.Trim(), projectName,
                    GdoProjectIdentity.BundleFileName(projectName));
            }
            catch
            {
                for (int index = created.Count - 1; index >= 0; index--)
                    AssetDatabase.DeleteAsset(created[index]);
                throw;
            }
        }

        public static string ProjectFolder(GdoBundleGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            string path = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Project graph must be saved as a Unity asset.", nameof(graph));
            return Path.GetDirectoryName(path).Replace('\\', '/');
        }

        public static string GdoFolder(GdoBundleGraphAsset graph) => ProjectFolder(graph) + "/GDOs";
        public static string ContentFolder(GdoBundleGraphAsset graph) => ProjectFolder(graph) + "/Content";
        public static string PrefabFolder(GdoBundleGraphAsset graph) => ProjectFolder(graph) + "/Prefabs";

        public static IReadOnlyList<GdoGraphDiagnostic> ValidateLayout(GdoBundleGraphAsset graph)
        {
            var issues = new List<GdoGraphDiagnostic>();
            string path = graph == null ? string.Empty : AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(path))
            {
                AddIssue(issues, path, "Project graph must be saved as a Unity asset under Assets.");
                return issues;
            }

            string folder = ProjectFolder(graph);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetFileName(folder), graph.BundleName, StringComparison.Ordinal) ||
                !string.Equals(Path.GetFileName(path), graph.BundleName + ".asset", StringComparison.Ordinal))
                AddIssue(issues, path, "Project folder and graph asset must match the project namespace.");
            try
            {
                GdoProjectIdentity.ValidateName(graph.BundleName, nameof(graph.BundleName));
                if (!graph.HasAuthorIdentity)
                    AddIssue(issues, path, "Project author identity is required.");
                if (graph.BundleFileName != GdoProjectIdentity.BundleFileName(graph.BundleName))
                    AddIssue(issues, path, "Bundle file name must be derived from the project namespace.");
            }
            catch (ArgumentException exception) { AddIssue(issues, path, exception.Message); }

            foreach (string required in new[] { GdoFolder(graph), ContentFolder(graph) })
                if (!AssetDatabase.IsValidFolder(required)) AddIssue(issues, required, "Required project folder is missing.");
            return issues;
        }

        private static void CreateFolder(string parent, string name, ICollection<string> created)
        {
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Could not create project folder '" + parent + "/" + name + "'.");
            string path = AssetDatabase.GUIDToAssetPath(guid);
            created.Add(path);
            if (!string.Equals(path, parent + "/" + name, StringComparison.Ordinal))
                throw new InvalidOperationException("Unity created an unexpected project folder path.");
        }

        private static void AddIssue(ICollection<GdoGraphDiagnostic> issues, string path, string message) =>
            issues.Add(new GdoGraphDiagnostic(ValidationSeverity.Error, "ProjectLayout", string.Empty, path, string.Empty, message));
    }
}
