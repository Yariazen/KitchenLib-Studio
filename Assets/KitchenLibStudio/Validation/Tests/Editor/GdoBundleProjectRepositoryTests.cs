using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoBundleProjectRepositoryTests
    {
        private const string Root = "Assets/Test Root";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "Test Root");
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            GdoProjectRenameMoveFailureProcessor.DeniedDestination = null;
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { Root }))
            {
                GdoOverlayAuthoringAsset companion = AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(
                    GdoOverlayAuthoringRepository.Root + "/" + guid + ".asset");
                if (companion != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(companion));
            }
            AssetDatabase.DeleteAsset(Root);
            Undo.ClearAll();
        }

        [Test]
        public void CreateEstablishesOwnedLayoutAndReadableIdentity()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");

            Assert.That(AssetDatabase.GetAssetPath(graph), Is.EqualTo(Root + "/My Bakery/My Bakery.asset"));
            Assert.That(graph.name, Is.EqualTo("My Bakery"));
            Assert.That(graph.BundleName, Is.EqualTo("My Bakery"));
            Assert.That(graph.BundleFileName, Is.EqualTo("my-bakery.assets"));
            Assert.That(GdoBundleProjectRepository.ProjectFolder(graph), Is.EqualTo(Root + "/My Bakery"));
            Assert.That(GdoBundleProjectRepository.GdoFolder(graph), Is.EqualTo(Root + "/My Bakery/GDOs"));
            Assert.That(GdoBundleProjectRepository.ContentFolder(graph), Is.EqualTo(Root + "/My Bakery/Content"));
            Assert.That(AssetDatabase.IsValidFolder(Root + "/My Bakery/GDOs"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Root + "/My Bakery/Content"), Is.True);
            Assert.That(GdoBundleProjectRepository.ValidateLayout(graph), Is.Empty);
        }

        [Test]
        public void CreateRejectsExistingFolderWithoutAddingPartialLayout()
        {
            AssetDatabase.CreateFolder(Root, "My Bakery");
            string[] before = AssetDatabase.FindAssets(string.Empty, new[] { Root });

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.Create(Root, "Test Author", "my bakery"));

            Assert.That(AssetDatabase.FindAssets(string.Empty, new[] { Root }), Is.EquivalentTo(before));
            Assert.That(AssetDatabase.IsValidFolder(Root + "/My Bakery/GDOs"), Is.False);
        }

        [TestCase("../Escape")]
        [TestCase("CON")]
        [TestCase("Bad.")]
        public void CreateRejectsInvalidProjectNameWithoutCreatingFolders(string name)
        {
            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.Create(Root, "Test Author", name));
            Assert.That(AssetDatabase.GetSubFolders(Root), Is.Empty);
        }

        [Test]
        public void CreateRejectsMissingParentWithoutCreatingIt()
        {
            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.Create(Root + "/Missing", "Test Author", "My Bakery"));
            Assert.That(AssetDatabase.GetSubFolders(Root), Is.Empty);
        }

        [Test]
        public void ValidateLayoutReportsMissingFoldersAndMisplacedGraph()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Wrong.asset", "Test Author", "My Bakery", "my-bakery.assets");

            GdoGraphDiagnostic[] issues = GdoBundleProjectRepository.ValidateLayout(graph).ToArray();

            Assert.That(issues.Any(issue => issue.AssetPath == Root + "/Wrong.asset" && issue.Severity >= ValidationSeverity.Error), Is.True);
            Assert.That(issues.Any(issue => issue.AssetPath == Root + "/GDOs" && issue.Severity >= ValidationSeverity.Error), Is.True);
            Assert.That(issues.Any(issue => issue.AssetPath == Root + "/Content" && issue.Severity >= ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void ValidateLayoutReportsUnsavedGraph()
        {
            var graph = ScriptableObject.CreateInstance<GdoBundleGraphAsset>();
            try { Assert.That(GdoBundleProjectRepository.ValidateLayout(graph).Any(issue => issue.Severity >= ValidationSeverity.Error), Is.True); }
            finally { UnityEngine.Object.DestroyImmediate(graph); }
        }

        [Test]
        public void RenamePreviewDescribesIdentityChangesWithoutMutatingAssets()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            string before = EditorJsonUtility.ToJson(graph);
            string[] paths = graph.Nodes.Where(n => n.Asset != null).Select(n => AssetDatabase.GetAssetPath(n.Asset)).ToArray();
            int[] ids = graph.Nodes.Where(n => n.Asset != null).Select(n => n.Asset.ID).ToArray();

            GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery");

            Assert.That(preview.OldNamespace, Is.EqualTo("My Bakery"));
            Assert.That(preview.NewNamespace, Is.EqualTo("New Bakery"));
            Assert.That(preview.OldProjectPath, Is.EqualTo(Root + "/My Bakery"));
            Assert.That(preview.NewProjectPath, Is.EqualTo(Root + "/New Bakery"));
            Assert.That(preview.AffectedGdoCount, Is.EqualTo(2));
            Assert.That(preview.Warning, Does.Contain("Undo/Redo history"));
            GdoProjectRenameIdChange[] changes = preview.GdoChanges.ToArray();
            Assert.That(changes.Length, Is.EqualTo(2));
            foreach (GdoProjectRenameIdChange change in changes)
            {
                GdoGraphNodeRecord node = graph.Nodes.Single(n => n.AssetGuid == change.AssetGuid);
                Assert.That(change.OldId, Is.EqualTo(node.Asset.ID));
                Assert.That(change.NewId, Is.EqualTo(change.OldId));
            }
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(graph.Nodes.Where(n => n.Asset != null).Select(n => n.Asset.ID), Is.EqualTo(ids));
            Assert.That(graph.Nodes.Where(n => n.Asset != null).Select(n => AssetDatabase.GetAssetPath(n.Asset)), Is.EqualTo(paths));
            Assert.That(AssetDatabase.IsValidFolder(Root + "/New Bakery"), Is.False);
        }

        [Test]
        public void RenameMovesProjectPreservesGuidsAndRelationshipsAndPersistsMetadata()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            string graphGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            string folderGuid = AssetDatabase.AssetPathToGUID(Root + "/My Bakery");
            string[] gdoGuids = graph.Nodes.Where(n => n.Asset != null).Select(n => n.AssetGuid).ToArray();
            int[] ids = graph.Nodes.Where(n => n.Asset != null).Select(n => n.Asset.ID).ToArray();
            string[] nodeIds = graph.Nodes.Select(n => n.Id).ToArray();
            string edgeJson = JsonUtility.ToJson(graph.Edges.Single());
            string relationshipJson = JsonUtility.ToJson(graph.Relationships.Single());
            // Built output is disposable and must never be discovered or renamed.
            string builtPath = Root + "/my-bakery.assets";
            File.WriteAllText(builtPath, "existing build bytes");
            try
            {
                graph = GdoBundleProjectRepository.RenameProject(graph, GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery"));
                AssetDatabase.SaveAssets();
                string newPath = AssetDatabase.GUIDToAssetPath(graphGuid);
                AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceUpdate);
                graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(newPath);

                Assert.That(newPath, Is.EqualTo(Root + "/New Bakery/New Bakery.asset"));
                Assert.That(AssetDatabase.GUIDToAssetPath(folderGuid), Is.EqualTo(Root + "/New Bakery"));
                Assert.That(AssetDatabase.IsValidFolder(Root + "/My Bakery"), Is.False);
                Assert.That(graph.name, Is.EqualTo("New Bakery"));
                Assert.That(graph.BundleName, Is.EqualTo("New Bakery"));
                Assert.That(graph.BundleFileName, Is.EqualTo("new-bakery.assets"));
                Assert.That(GdoBundleProjectRepository.ValidateLayout(graph), Is.Empty);
                Assert.That(graph.Nodes.Select(n => n.Id), Is.EqualTo(nodeIds));
                Assert.That(graph.Nodes.Where(n => n.Asset != null).Select(n => n.AssetGuid), Is.EqualTo(gdoGuids));
                foreach (GdoGraphNodeRecord node in graph.Nodes.Where(n => n.Asset != null))
                {
                    Assert.That(AssetDatabase.GUIDToAssetPath(node.AssetGuid), Is.EqualTo(Root + "/New Bakery/GDOs/" + node.Asset.name + ".asset"));
                    Assert.That(node.Asset.ID, Is.EqualTo(ids[graph.Nodes.Where(n => n.Asset != null).ToList().IndexOf(node)]));
                    Assert.That(node.CachedName, Is.EqualTo(node.Asset.name));
                    Assert.That(JsonUtility.ToJson(node), Does.Contain("\"_cachedRuntimeId\":" + node.Asset.ID));
                    Assert.That(node.RuntimeTypeName, Is.EqualTo(node.Asset.GetType().FullName));
                }
                Assert.That(JsonUtility.ToJson(graph.Edges.Single()), Is.EqualTo(edgeJson));
                Assert.That(JsonUtility.ToJson(graph.Relationships.Single()), Is.EqualTo(relationshipJson));
                Assert.That(graph.Nodes.Single(n => n.Kind == GdoGraphNodeKind.External).ExternalId, Is.EqualTo(123));
                Assert.That(File.ReadAllText(builtPath), Is.EqualTo("existing build bytes"));
                Assert.That(File.Exists(Root + "/new-bakery.assets"), Is.False);
            }
            finally { File.Delete(builtPath); }
        }

        [Test]
        public void RenameSupportsCaseOnlyFolderAndGraphChanges()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            int[] ids = graph.Nodes.Select(n => n.RuntimeId).ToArray();

            graph = GdoBundleProjectRepository.RenameProject(graph, GdoBundleProjectRepository.PreviewProjectRename(graph, "my bakery"));

            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(Root + "/my bakery/my bakery.asset"));
            Assert.That(graph.BundleName, Is.EqualTo("my bakery"));
            Assert.That(graph.name, Is.EqualTo("my bakery"));
            Assert.That(graph.Nodes.Select(n => n.RuntimeId), Is.EqualTo(ids));
            Assert.That(GdoBundleProjectRepository.ValidateLayout(graph), Is.Empty);
            Assert.That(AssetDatabase.GetSubFolders(Root), Is.EqualTo(new[] { Root + "/my bakery" }));
        }

        [Test]
        public void RenameRejectsConflictIntroducedAfterPreviewWithoutChangingOriginalState()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery");
            string before = EditorJsonUtility.ToJson(graph);
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { Root + "/My Bakery" });
            string[] paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            int[] ids = graph.Nodes.Select(n => n.RuntimeId).ToArray();
            AssetDatabase.CreateFolder(Root, "NEW BAKERY");
            string conflictGuid = AssetDatabase.AssetPathToGUID(Root + "/NEW BAKERY");

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.RenameProject(graph, preview));

            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(graph.Nodes.Select(n => n.RuntimeId), Is.EqualTo(ids));
            Assert.That(guids.Select(AssetDatabase.GUIDToAssetPath), Is.EqualTo(paths));
            Assert.That(AssetDatabase.AssetPathToGUID(Root + "/NEW BAKERY"), Is.EqualTo(conflictGuid));
        }

        [Test]
        public void RenameRejectsGraphFilenameConflictWithinSourceBeforeMovingFolder()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery");
            var occupied = ScriptableObject.CreateInstance<GdoBundleGraphAsset>();
            AssetDatabase.CreateAsset(occupied, Root + "/My Bakery/New Bakery.asset");
            string before = EditorJsonUtility.ToJson(graph);

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.RenameProject(graph, preview));

            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(AssetDatabase.GetAssetPath(graph), Is.EqualTo(Root + "/My Bakery/My Bakery.asset"));
            Assert.That(AssetDatabase.IsValidFolder(Root + "/New Bakery"), Is.False);
        }

        [Test]
        public void RenameRollsBackFolderMoveWhenGraphMoveIsRejected()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery");
            string before = EditorJsonUtility.ToJson(graph);
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { Root + "/My Bakery" });
            string[] paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            int[] ids = graph.Nodes.Select(n => n.RuntimeId).ToArray();
            GdoProjectRenameMoveFailureProcessor.DeniedDestination = Root + "/New Bakery/New Bakery.asset";

            Assert.Throws<InvalidOperationException>(() => GdoBundleProjectRepository.RenameProject(graph, preview));

            Assert.That(GdoProjectRenameMoveFailureProcessor.DeniedDestination, Is.Null, "Failure must occur during the graph move, after the folder move.");
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(graph.Nodes.Select(n => n.RuntimeId), Is.EqualTo(ids));
            Assert.That(guids.Select(AssetDatabase.GUIDToAssetPath), Is.EqualTo(paths));
            Assert.That(AssetDatabase.IsValidFolder(Root + "/New Bakery"), Is.False);
        }

        [Test]
        public void RenameRejectsStaleMembershipPreview()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery");
            GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Water", new Rect());
            string before = EditorJsonUtility.ToJson(graph);

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.RenameProject(graph, preview));

            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(AssetDatabase.GetAssetPath(graph), Is.EqualTo(Root + "/My Bakery/My Bakery.asset"));
        }

        [Test]
        public void RenamePreviewIgnoresHypotheticalDerivedIdCollisionBecauseIdsAreStable()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, GdoProjectIdentity.RuntimeId("New Bakery", "Flour"), "KitchenData.Item", new Rect());

            Assert.DoesNotThrow(() => GdoBundleProjectRepository.PreviewProjectRename(graph, "New Bakery"));

            Assert.That(graph.BundleName, Is.EqualTo("My Bakery"));
        }

        [Test]
        public void GdoRenamePreviewDescribesChangesAndWarningWithoutMutation()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            string before = EditorJsonUtility.ToJson(graph);
            string assetBefore = EditorJsonUtility.ToJson(node.Asset);

            GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour");

            Assert.That(preview.Graph, Is.SameAs(graph));
            Assert.That(preview.NodeId, Is.EqualTo(node.Id));
            Assert.That(preview.OldAssetPath, Is.EqualTo(Root + "/My Bakery/GDOs/Flour.asset"));
            Assert.That(preview.NewAssetPath, Is.EqualTo(Root + "/My Bakery/GDOs/Bread Flour.asset"));
            Assert.That(preview.OldName, Is.EqualTo("Flour"));
            Assert.That(preview.NewName, Is.EqualTo("Bread Flour"));
            Assert.That(preview.OldId, Is.EqualTo(node.Asset.ID));
            Assert.That(preview.NewId, Is.EqualTo(preview.OldId));
            Assert.That(preview.Warning, Does.Contain("Undo/Redo history"));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(EditorJsonUtility.ToJson(node.Asset), Is.EqualTo(assetBefore));
        }

        [Test]
        public void GdoRenamePersistsIdentityCacheAndProjectionsWhilePreservingGuidAndConnections()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Water", new Rect());
            GdoBundleGraphRepository.AddEdge(graph, node.Id, "$self", owner.Id, "KitchenData.Item.DirtiesTo", 0);
            Assert.That(GdoGraphProjectionApplier.TryApply(graph, out _), Is.True);
            AssetDatabase.SaveAssets();
            string guid = node.AssetGuid;
            string[] edges = graph.Edges.Select(JsonUtility.ToJson).ToArray();
            string[] relations = graph.Relationships.Select(JsonUtility.ToJson).ToArray();
            string[] nodeIds = graph.Nodes.Select(n => n.Id).ToArray();
            string graphPath = AssetDatabase.GetAssetPath(graph);

            int stableId = node.Asset.ID;
            KitchenData.GameDataObject renamed = GdoBundleProjectRepository.RenameGdo(GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour"));
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(graphPath);
            node = graph.Nodes.Single(n => n.AssetGuid == guid);

            Assert.That(renamed, Is.SameAs(AssetDatabase.LoadAssetAtPath<KitchenData.GameDataObject>(Root + "/My Bakery/GDOs/Bread Flour.asset")));
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(Root + "/My Bakery/GDOs/Bread Flour.asset"));
            Assert.That(renamed.name, Is.EqualTo("Bread Flour"));
            Assert.That(renamed.ID, Is.EqualTo(stableId));
            Assert.That(node.CachedName, Is.EqualTo("Bread Flour"));
            Assert.That(JsonUtility.ToJson(node), Does.Contain("\"_cachedRuntimeId\":" + renamed.ID));
            Assert.That(node.RuntimeTypeName, Is.EqualTo("KitchenData.Item"));
            Assert.That(graph.Nodes.Select(n => n.Id), Is.EqualTo(nodeIds));
            Assert.That(graph.Edges.Select(JsonUtility.ToJson), Is.EqualTo(edges));
            Assert.That(graph.Relationships.Select(JsonUtility.ToJson), Is.EqualTo(relations));
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(owner.Asset);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(overlay), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            overlay = GdoOverlayAuthoringRepository.Find(owner.Asset);
            Assert.That(overlay.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField field), Is.True);
            Assert.That(field.Value.ReferenceValue.Asset, Is.SameAs(renamed));
            Assert.That(field.Value.ReferenceValue.Id, Is.EqualTo(renamed.ID));
        }

        [Test]
        public void GdoRenameSupportsCaseOnlyFilenameChange()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            string guid = node.AssetGuid;
            int id = node.RuntimeId;

            KitchenData.GameDataObject renamed = GdoBundleProjectRepository.RenameGdo(GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "flour"));

            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(Root + "/My Bakery/GDOs/flour.asset"));
            Assert.That(renamed.name, Is.EqualTo("flour"));
            Assert.That(renamed.ID, Is.EqualTo(id));
            Assert.That(graph.Nodes.Single(n => n.Id == node.Id).CachedName, Is.EqualTo("flour"));
        }

        [Test]
        public void GdoRenameRejectsCaseInsensitiveDuplicateNameEvenWithDifferentFilename()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord flour = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoGraphNodeRecord dough = graph.Nodes.Single(n => n.CachedName == "Dough Ball");
            dough.Asset.name = "Bread Flour";

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.PreviewGdoRename(graph, flour.Id, "bread flour"));
            Assert.That(flour.Asset.name, Is.EqualTo("Flour"));
        }

        [Test]
        public void GdoRenameRejectsDestinationConflictIntroducedAfterPreviewWithoutPartialMutation()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour");
            string graphBefore = EditorJsonUtility.ToJson(graph);
            string assetBefore = EditorJsonUtility.ToJson(node.Asset);
            string pathBefore = AssetDatabase.GetAssetPath(node.Asset);
            var conflict = ScriptableObject.CreateInstance<KitchenData.Item>();
            AssetDatabase.CreateAsset(conflict, Root + "/My Bakery/GDOs/BREAD FLOUR.asset");
            string conflictGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(conflict));

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.RenameGdo(preview));

            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(graphBefore));
            Assert.That(EditorJsonUtility.ToJson(node.Asset), Is.EqualTo(assetBefore));
            Assert.That(AssetDatabase.GUIDToAssetPath(node.AssetGuid), Is.EqualTo(pathBefore));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(conflict)), Is.EqualTo(conflictGuid));
        }

        [Test]
        public void GdoRenameRollsBackTemporaryCaseOnlyRenameWhenFinalRenameFails()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "flour");
            string graphBefore = EditorJsonUtility.ToJson(graph);
            string assetBefore = EditorJsonUtility.ToJson(node.Asset);
            string pathBefore = AssetDatabase.GetAssetPath(node.Asset);
            GdoProjectRenameMoveFailureProcessor.DeniedDestination = Root + "/My Bakery/GDOs/flour.asset";

            Assert.Throws<InvalidOperationException>(() => GdoBundleProjectRepository.RenameGdo(preview));

            Assert.That(GdoProjectRenameMoveFailureProcessor.DeniedDestination, Is.Null);
            Assert.That(AssetDatabase.GUIDToAssetPath(node.AssetGuid), Is.EqualTo(pathBefore));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(graphBefore));
            Assert.That(EditorJsonUtility.ToJson(node.Asset), Is.EqualTo(assetBefore));
        }

        [Test]
        public void GdoRenameRejectsStaleIdentityPreview()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour");
            node.Asset.ID = 654321;

            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.RenameGdo(preview));

            Assert.That(node.Asset.ID, Is.EqualTo(654321));
            Assert.That(node.Asset.name, Is.EqualTo("Flour"));
        }

        [Test]
        public void GdoRenameRestoresPathIdentityAndCacheWhenProjectionFails()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoGraphNodeRecord external = graph.Nodes.Single(n => n.Kind == GdoGraphNodeKind.External);
            GdoBundleGraphRepository.AddEdge(graph, node.Id, "$self", node.Id, "KitchenData.Item.DirtiesTo", 0);
            GdoBundleGraphRepository.AddEdge(graph, external.Id, "$self", node.Id, "KitchenData.Item.DirtiesTo", 1);
            GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour");
            AssetDatabase.SaveAssets();
            string graphBefore = EditorJsonUtility.ToJson(graph);
            string assetBefore = EditorJsonUtility.ToJson(node.Asset);
            string pathBefore = AssetDatabase.GetAssetPath(node.Asset);
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(node.Asset);
            string overlayBefore = EditorJsonUtility.ToJson(overlay);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => GdoBundleProjectRepository.RenameGdo(preview));

            Assert.That(failure.Message, Does.Contain("Single-reference port"));
            Assert.That(AssetDatabase.GUIDToAssetPath(node.AssetGuid), Is.EqualTo(pathBefore));
            Assert.That(EditorJsonUtility.ToJson(node.Asset), Is.EqualTo(assetBefore));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(graphBefore));
            Assert.That(EditorJsonUtility.ToJson(overlay), Is.EqualTo(overlayBefore));
            AssetDatabase.ImportAsset(pathBefore, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(EditorJsonUtility.ToJson(node.Asset), Is.EqualTo(assetBefore));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(graphBefore));
        }

        [TestCase("../Escape")]
        [TestCase("CON")]
        public void GdoRenameRejectsInvalidName(string newName)
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, newName));
        }

        [Test]
        public void SetElementPositionsMovesNodesAndRelationshipsAsOneUndoOperation()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord owner = graph.Nodes.Single(node => node.CachedName == "Dough Ball");
            GdoGraphRelationshipRecord relationship = graph.Relationships.Single();
            Rect ownerBefore = owner.Position; Rect relationshipBefore = relationship.Position;
            Rect ownerAfter = new Rect(101, 102, 240, 160); Rect relationshipAfter = new Rect(401, 202, 260, 180);

            GdoBundleGraphRepository.SetElementPositions(graph, new Dictionary<string, Rect>
            {
                [owner.Id] = ownerAfter,
                [relationship.Id] = relationshipAfter
            });

            Assert.That(owner.Position, Is.EqualTo(ownerAfter));
            Assert.That(relationship.Position, Is.EqualTo(relationshipAfter));
            Undo.PerformUndo();
            Assert.That(owner.Position, Is.EqualTo(ownerBefore));
            Assert.That(relationship.Position, Is.EqualTo(relationshipBefore));
        }

        [Test]
        public void SetElementPositionsRejectsUnknownIdBeforeChangingAnything()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord owner = graph.Nodes.Single(node => node.CachedName == "Dough Ball");
            Rect before = owner.Position;

            Assert.Throws<ArgumentException>(() => GdoBundleGraphRepository.SetElementPositions(graph,
                new Dictionary<string, Rect> { [owner.Id] = new Rect(90, 90, 90, 90), ["missing"] = new Rect() }));

            Assert.That(owner.Position, Is.EqualTo(before));
        }

        [Test]
        public void MoveRelationshipUsesOneOwnerWideOrderAcrossFieldsAndUndoesAtomically()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord owner = graph.Nodes.Single(node => node.CachedName == "Dough Ball");
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord first = graph.Relationships.Single();
            GdoGraphRelationshipRecord second = controller.CreateNextRelationship(
                owner.Id, first.FieldPath, new Rect());
            GdoGraphRelationshipRecord otherField = controller.CreateNextRelationship(
                owner.Id, "KitchenData.ItemGroup.Rewards[]", new Rect());

            Assert.That(new[] { first.Order, second.Order, otherField.Order }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(GdoBundleGraphRepository.MoveRelationship(graph, otherField.Id, -1), Is.True);

            Assert.That(new[] { first.Order, otherField.Order, second.Order }, Is.EqualTo(new[] { 0, 1, 2 }));
            Undo.PerformUndo();
            Assert.That(first.Order, Is.EqualTo(0));
            Assert.That(second.Order, Is.EqualTo(1));
            Assert.That(otherField.Order, Is.EqualTo(2));
        }

        [Test]
        public void RelationshipExpansionPersistsAndUndoes()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphRelationshipRecord relationship = graph.Relationships.Single();

            Assert.That(relationship.Expanded, Is.True);
            GdoBundleGraphRepository.SetRelationshipExpanded(graph, relationship.Id, false);
            Assert.That(relationship.Expanded, Is.False);

            Undo.PerformUndo();
            Assert.That(relationship.Expanded, Is.True);
        }

        [Test]
        public void NewOwnerDefaultsToNodesPresentation()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Presentation");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.Item", "Presentation Item", new Rect());

            Assert.That(owner.PresentationMode, Is.EqualTo(GdoGraphPresentationMode.Nodes));
            Assert.That(graph.SchemaVersion, Is.EqualTo(GdoBundleGraphRepository.CurrentSchemaVersion));
        }

        [TestCase(true, GdoGraphPresentationMode.Full)]
        [TestCase(false, GdoGraphPresentationMode.Connected)]
        public void LegacyOwnerExpandedStateMigratesOnce(bool expanded, GdoGraphPresentationMode expected)
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Legacy Presentation");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.Item", "Legacy Item", new Rect());
            var serialized = new SerializedObject(graph);
            SerializedProperty nodes = serialized.FindProperty("_nodes");
            SerializedProperty serializedOwner = Enumerable.Range(0, nodes.arraySize)
                .Select(nodes.GetArrayElementAtIndex)
                .Single(value => value.FindPropertyRelative("_id").stringValue == owner.Id);
            serializedOwner.FindPropertyRelative("_expanded").boolValue = expanded;
            serialized.FindProperty("_schemaVersion").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(GdoBundleGraphRepository.EnsureCurrentSchema(graph), Is.True);
            Assert.That(owner.PresentationMode, Is.EqualTo(expected));
            Assert.That(graph.SchemaVersion, Is.EqualTo(GdoBundleGraphRepository.CurrentSchemaVersion));
            Assert.That(GdoBundleGraphRepository.EnsureCurrentSchema(graph), Is.False);
            Assert.That(owner.PresentationMode, Is.EqualTo(expected));
        }

        [Test]
        public void PresentationModePersistsAcrossReload()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Persist Presentation");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.Item", "Persist Item", new Rect());

            GdoBundleGraphRepository.SetPresentationMode(graph, owner.Id, GdoGraphPresentationMode.Full);
            string graphPath = AssetDatabase.GetAssetPath(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(graphPath);

            Assert.That(graph.Nodes.Single(value => value.Id == owner.Id).PresentationMode,
                Is.EqualTo(GdoGraphPresentationMode.Full));
        }

        [Test]
        public void GdoRenameRejectsUnownedNodesButIgnoresHypotheticalDerivedIdCollisions()
        {
            GdoBundleGraphAsset graph = CreatePopulatedProject();
            GdoGraphNodeRecord node = graph.Nodes.Single(n => n.CachedName == "Flour");
            GdoGraphNodeRecord external = graph.Nodes.Single(n => n.Kind == GdoGraphNodeKind.External);
            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.PreviewGdoRename(graph, external.Id, "Bread Flour"));
            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.PreviewGdoRename(graph, "missing", "Bread Flour"));
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey,
                GdoProjectIdentity.RuntimeId("Test Author", "My Bakery", "Bread Flour"),
                "KitchenData.Item", new Rect());
            Assert.DoesNotThrow(() => GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour"));
            Assert.That(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(node.Asset), Root + "/Outside.asset"), Is.Empty);
            Assert.Throws<ArgumentException>(() => GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Other Name"));
        }

        [Test]
        public void DuplicateExternalIdentityCreatesIndependentGraphRecords()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Duplicates");

            GdoGraphNodeRecord first = GdoBundleGraphRepository.AddExternal(graph,
                "workshop:2913877103", 42, "KitchenData.Item", new Rect(10, 20, 200, 100), "Pork");
            GdoGraphNodeRecord second = GdoBundleGraphRepository.AddExternal(graph,
                "workshop:2913877103", 42, "KitchenData.Item", new Rect(50, 60, 200, 100), "Pork");

            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(graph.Nodes.Count(node => node.Kind == GdoGraphNodeKind.External &&
                node.ExternalProviderKey == "workshop:2913877103" && node.ExternalId == 42), Is.EqualTo(2));
        }

        private static GdoBundleGraphAsset CreatePopulatedProject()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.ItemGroup", "Dough Ball", new Rect(1, 2, 3, 4));
            GdoGraphNodeRecord flour = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Flour", new Rect());
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 123, "KitchenData.Item", new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord relation = controller.CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            controller.SetRelationshipValue(relation.Id, GdoOverlayValueEditor.ReplaceStructMember(relation.Value, "Items",
                GdoOverlayValue.List(new[] { GdoOverlayValue.Null() })));
            controller.Connect(flour.Id, relation.Id, "KitchenData.ItemGroup.Sets[].Items[0]", 0);
            AssetDatabase.SaveAssets();
            return graph;
        }
    }

    // Exercise a real Unity move rejection without a failure-injection API in production.
    public sealed class GdoProjectRenameMoveFailureProcessor : UnityEditor.AssetModificationProcessor
    {
        internal static string DeniedDestination;

        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            if (destinationPath != DeniedDestination) return AssetMoveResult.DidNotMove;
            DeniedDestination = null;
            return AssetMoveResult.FailedMove;
        }
    }
}
