using System;
using System.IO;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class DeploymentReadinessTests
    {
        private const string Root = "Assets/DeploymentReadinessTests";
        private string _output;

        [SetUp] public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "DeploymentReadinessTests");
            _output = Path.Combine(Path.GetTempPath(), "KLDeployment", Guid.NewGuid().ToString("N"), "content");
            Undo.ClearAll();
        }

        [TearDown] public void TearDown()
        {
            Undo.ClearAll();
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { Root }))
                AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root + "/" + guid + ".asset");
            AssetDatabase.DeleteAsset(Root);
            string package = Path.GetDirectoryName(_output);
            if (Directory.Exists(package)) Directory.Delete(package, true);
        }

        private static GdoBundleGraphAsset Project(string name = "First") => GdoBundleProjectRepository.Create(Root, "Test Author", name);
        private static GdoGraphNodeRecord Item(GdoBundleGraphAsset graph, string name) =>
            GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", name, new Rect());

        private static void Link(GdoBundleGraphAsset graph, GdoGraphNodeRecord owner, GdoGraphNodeRecord target) =>
            new GdoGraphConnectionController(graph).Connect(target.Id, owner.Id, "KitchenData.Item.DirtiesTo", 0);

        private static GdoBundleGraphAsset Reload(GdoBundleGraphAsset graph)
        {
            string path = AssetDatabase.GetAssetPath(graph);
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(path);
        }

        [Test] public void ProjectRenameRefreshesCanonicalLocalReferencesAfterReloadAndBuilds()
        {
            var graph = Project(); var owner = Item(graph, "Owner"); var target = Item(graph, "Target");
            Link(graph, owner, target);
            graph = GdoBundleProjectRepository.RenameProject(graph, GdoBundleProjectRepository.PreviewProjectRename(graph, "Renamed"));
            graph = Reload(graph);
            var reloadedOwner = graph.Nodes.Single(n => n.Id == owner.Id);
            var reloadedTarget = graph.Nodes.Single(n => n.Id == target.Id);
            Assert.That(GdoOverlayAuthoringRepository.Find(reloadedOwner.Asset).TryGetField("KitchenData.Item", "DirtiesTo", out var field), Is.True);
            Assert.That(field.Value.ReferenceValue.Id, Is.EqualTo(reloadedTarget.Asset.ID));
            Assert.That(GdoBundleGraphValidator.Validate(graph).Where(i => i.Severity >= ValidationSeverity.Error), Is.Empty);
            var result = GdoBundleGraphBuilder.Build(graph, _output);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
        }

        [TestCase(false)] [TestCase(true)]
        public void SuccessfulRenameDiscardsAffectedUndoHistoryAndSurvivesUndoRedoReload(bool projectRename)
        {
            var graph = Project(); var owner = Item(graph, "Owner"); var target = Item(graph, "Target");
            Link(graph, owner, target);
            // Actual pre-rename snapshots of every affected object, including a companion-only edit.
            Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(target.Asset, "old identity");
            target.Asset.ID = -7; Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Undo.PerformRedo();
            target.Asset.ID = GdoProjectIdentity.RuntimeId("First", "Target");
            LinkSnapshot(graph, owner);
            if (projectRename) graph = GdoBundleProjectRepository.RenameProject(graph, GdoBundleProjectRepository.PreviewProjectRename(graph, "Renamed"));
            else GdoBundleProjectRepository.RenameGdo(GdoBundleProjectRepository.PreviewGdoRename(graph, target.Id, "NewTarget"));
            string after = EditorJsonUtility.ToJson(graph);
            string[] assets = graph.Nodes.Select(n => EditorJsonUtility.ToJson(n.Asset)).ToArray();
            string companion = EditorJsonUtility.ToJson(GdoOverlayAuthoringRepository.Find(owner.Asset));
            for (int i = 0; i < 12; i++) Undo.PerformUndo();
            for (int i = 0; i < 12; i++) Undo.PerformRedo();
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(after));
            Assert.That(graph.Nodes.Select(n => EditorJsonUtility.ToJson(n.Asset)), Is.EqualTo(assets));
            Assert.That(EditorJsonUtility.ToJson(GdoOverlayAuthoringRepository.Find(owner.Asset)), Is.EqualTo(companion));
            graph = Reload(graph);
            Assert.That(GdoBundleGraphValidator.Validate(graph).Where(i => i.Severity >= ValidationSeverity.Error), Is.Empty);
        }

        private static void LinkSnapshot(GdoBundleGraphAsset graph, GdoGraphNodeRecord owner)
        {
            Undo.IncrementCurrentGroup();
            GdoBundleGraphRepository.SetPosition(graph, owner.Id, new Rect(40, 30, 250, 100));
            var companion = GdoOverlayAuthoringRepository.Find(owner.Asset);
            Undo.RegisterCompleteObjectUndo(companion, "old companion projection");
            companion.UpsertField("KitchenData.Item", "DirtiesTo", GdoOverlayValue.Null());
            GdoGraphProjectionApplier.TryApply(graph, out _, true, true);
        }

        [Test] public void MovedMemberHasMemberDiagnosticAndBuildStopsBeforeCreatingOutput()
        {
            var graph = Project(); var node = Item(graph, "Moved");
            Assert.That(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(node.Asset), Root + "/Moved.asset"), Is.Empty);
            var issues = GdoBundleGraphValidator.Validate(graph);
            Assert.That(issues.Any(i => i.ElementId == node.Id && i.AssetPath == Root + "/Moved.asset" &&
                i.Category == "MemberOutsideProject" && i.Severity >= ValidationSeverity.Error), Is.True);
            Assert.That(GdoBundleGraphBuilder.Build(graph, _output).Succeeded, Is.False);
            Assert.That(Directory.Exists(_output), Is.False);
        }

        [Test]
        public void CreationPreflightsCalculatedExternalCollisionWithoutMutation()
        {
            var graph = Project();
            int collision = GdoProjectIdentity.RuntimeId("Test Author", "First", "Collision");
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey,
                collision, "KitchenData.Item", new Rect());
            string before = EditorJsonUtility.ToJson(graph);
            string[] paths = AssetDatabase.GetAllAssetPaths();
            Assert.Throws<ArgumentException>(() => Item(graph, "Collision"));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            Assert.That(AssetDatabase.GetAllAssetPaths(), Is.EquivalentTo(paths));
        }

        [Test] public void OwnedProjectContainsLocalizationAndRebuildsAfterFolderTransfer()
        {
            var graph = Project();
            var node = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Dish", "Dish", new Rect());
            GdoGraphAssetCommands.SetIntrinsicValue(node.Asset, "KitchenData.LocalisedGameDataObject`1", "Info",
                GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "Portable dish", "Description", "Flavour") }));
            string companionPath = AssetDatabase.GetAssetPath(GdoOverlayAuthoringRepository.Find(node.Asset));
            Assert.That(companionPath, Does.StartWith(Root + "/First/Editor/Overlays/"));
            AssetDatabase.SaveAssets(); Undo.ClearAll();
            Assert.That(AssetDatabase.MoveAsset(Root + "/First", Root + "/Transit"), Is.Empty);
            // Keep the convention folder name while transferring its parent.
            AssetDatabase.CreateFolder(Root, "Destination");
            Assert.That(AssetDatabase.MoveAsset(Root + "/Transit", Root + "/Destination/First"), Is.Empty);
            graph = Reload(graph);
            var companion = GdoOverlayAuthoringRepository.Find(graph.Nodes.Single().Asset);
            Assert.That(companion.TryGetField("KitchenData.LocalisedGameDataObject`1", "Info", out var field), Is.True);
            Assert.That(field.Value.Locales.Single().DisplayName, Is.EqualTo("Portable dish"));
            var result = GdoBundleGraphBuilder.Build(graph, _output);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            var bundle = AssetBundle.LoadFromFile(result.BundlePath);
            try
            {
                string manifest = bundle.LoadAllAssets<TextAsset>().Single(a => a.name ==
                    Path.GetFileNameWithoutExtension(GdoOverlayManifest.AssetName)).text;
                Assert.That(manifest, Does.Contain("Portable dish").And.Contain("Description").And.Contain("Flavour"));
            }
            finally { bundle.Unload(true); }
        }

        [TestCase("local")] [TestCase("external")] [TestCase("broken")]
        public void GraphOffersRemovalForEveryMemberKindAndPreservesSourceAssets(string kind)
        {
            var graph = Project(); var node = kind == "external"
                ? GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 123, "KitchenData.Item", new Rect()) : Item(graph, "Member");
            string path = node.Asset == null ? null : AssetDatabase.GetAssetPath(node.Asset);
            if (kind == "broken") AssetDatabase.DeleteAsset(path);
            var window = GdoBundleGraphWindow.Open(graph);
            try
            {
                var view = window.GraphView;
                Node nodeView = (Node)view.FindElement(node.Id);
                Assert.That(nodeView.Q<Button>("remove-member"), Is.Null);
                ContextActions(nodeView).MenuItems().OfType<DropdownMenuAction>()
                    .Single(action => action.name == "Remove from graph").Execute();
                Assert.That(graph.Nodes, Is.Empty);
                Assert.That(view.FindElement(node.Id), Is.Null);
            }
            finally { window.Close(); }
            if (kind == "local") Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null);
        }

        [TestCase("local")] [TestCase("external")] [TestCase("broken")]
        public void ConnectedMemberRemovalRequiresConsentUpdatesProjectionAndCanUndo(string kind)
        {
            var graph = Project(); var owner = Item(graph, "Owner");
            var member = kind == "external" ? GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 123, "KitchenData.Item", new Rect()) : Item(graph, "Member");
            Link(graph, owner, member);
            string path = member.Asset == null ? null : AssetDatabase.GetAssetPath(member.Asset);
            if (kind == "broken") AssetDatabase.DeleteAsset(path);
            string before = EditorJsonUtility.ToJson(graph);
            var controller = new GdoGraphConnectionController(graph);
            Assert.Throws<InvalidOperationException>(() => controller.RemoveMember(member.Id, false));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            controller.RemoveMember(member.Id, true);
            Assert.That(graph.Nodes.Single().Id, Is.EqualTo(owner.Id));
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(GdoOverlayAuthoringRepository.Find(owner.Asset).TryGetField("KitchenData.Item", "DirtiesTo", out var field), Is.True);
            Assert.That(field.Value.Kind, Is.EqualTo(GdoOverlayValueKind.Null));
            Undo.PerformUndo();
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            if (kind == "local") Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null);
        }

        [Test] public void ViewCancelsConnectedMemberRemovalWithoutChangingGraphOrProjection()
        {
            var graph = Project(); var owner = Item(graph, "Owner"); var target = Item(graph, "Target");
            Link(graph, owner, target);
            int confirmations = 0;
            var view = new GdoBundleGraphView(confirmMemberRemoval: _ => { confirmations++; return false; });
            view.Load(graph);
            var window = GdoBundleGraphWindow.Open(graph);
            window.rootVisualElement.Add(view);
            string before = EditorJsonUtility.ToJson(graph);
            string companion = EditorJsonUtility.ToJson(GdoOverlayAuthoringRepository.Find(owner.Asset));
            try
            {
                Node nodeView = (Node)view.FindElement(target.Id);
                Assert.That(nodeView.Q<Button>("remove-member"), Is.Null);
                ContextActions(nodeView).MenuItems().OfType<DropdownMenuAction>()
                    .Single(action => action.name == "Remove from graph").Execute();
                Assert.That(confirmations, Is.EqualTo(1));
                Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
                Assert.That(EditorJsonUtility.ToJson(GdoOverlayAuthoringRepository.Find(owner.Asset)), Is.EqualTo(companion));
            }
            finally { window.Close(); }
        }

        [Test] public void MultipleBrokenMembersCanBeRemovedUntilProjectRecovers()
        {
            var graph = Project(); var first = Item(graph, "MissingOne"); var second = Item(graph, "MissingTwo");
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(first.Asset));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(second.Asset));
            var controller = new GdoGraphConnectionController(graph);
            Assert.DoesNotThrow(() => controller.RemoveMember(first.Id, false));
            Assert.That(graph.Nodes.Single().Id, Is.EqualTo(second.Id));
            controller.RemoveMember(second.Id, false);
            Assert.That(graph.Nodes, Is.Empty);
            Assert.That(GdoBundleGraphValidator.Validate(graph).Where(i => i.Severity >= ValidationSeverity.Error), Is.Empty);
        }

        [Test] public void OwnerRemovalRequiresConsentForOwnedRelationshipsAndPreservesAssetAndCompanion()
        {
            var graph = Project();
            var owner = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Dish", "Owner", new Rect());
            var external = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 123, "KitchenData.Item", new Rect());
            var controller = new GdoGraphConnectionController(graph);
            var relation = controller.CreateNextRelationship(owner.Id, "KitchenData.Dish.ResultingMenuItems[]", new Rect());
            controller.Connect(external.Id, relation.Id, "KitchenData.Dish.ResultingMenuItems[].Item", 0);
            string assetPath = AssetDatabase.GetAssetPath(owner.Asset);
            string companionPath = AssetDatabase.GetAssetPath(GdoOverlayAuthoringRepository.Find(owner.Asset));
            string before = EditorJsonUtility.ToJson(graph);
            Assert.Throws<InvalidOperationException>(() =>
                new GdoGraphConnectionController(graph).RemoveMember(owner.Id, false));
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
            new GdoGraphConnectionController(graph).RemoveMember( owner.Id, true);
            Assert.That(graph.Nodes.Single().Id, Is.EqualTo(external.Id));
            Assert.That(graph.Relationships, Is.Empty);
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(assetPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(companionPath), Is.Not.Null);
            Undo.PerformUndo();
            Assert.That(EditorJsonUtility.ToJson(graph), Is.EqualTo(before));
        }

        [Test] public void SharedCompanionsRemainReadableAndCollectIntoOwnedProjectWithoutLosingDataOrGuid()
        {
            var graph = Project(); var node = Item(graph, "Shared");
            var companion = GdoOverlayAuthoringRepository.Find(node.Asset);
            companion.UpsertField("KitchenData.Item", "IsSplitDepleted", GdoOverlayValue.Scalar("True"));
            EditorUtility.SetDirty(companion); AssetDatabase.SaveAssets();
            string owned = AssetDatabase.GetAssetPath(companion);
            string guid = AssetDatabase.AssetPathToGUID(owned);
            if (!AssetDatabase.IsValidFolder(GdoOverlayAuthoringRepository.Root))
            {
                if (!AssetDatabase.IsValidFolder("Assets/ModContent")) AssetDatabase.CreateFolder("Assets", "ModContent");
                if (!AssetDatabase.IsValidFolder("Assets/ModContent/Editor")) AssetDatabase.CreateFolder("Assets/ModContent", "Editor");
                AssetDatabase.CreateFolder("Assets/ModContent/Editor", "KitchenLibStudioOverlays");
            }
            string shared = GdoOverlayAuthoringRepository.Root + "/" + node.AssetGuid + ".asset";
            Assert.That(AssetDatabase.MoveAsset(owned, shared), Is.Empty);
            Assert.That(GdoOverlayAuthoringRepository.Find(node.Asset), Is.SameAs(companion));
            Assert.That(GdoOverlayAuthoringRepository.GetOrCreate(node.Asset), Is.SameAs(companion));
            GdoOverlayAuthoringRepository.MoveCompanionsIntoProject(graph);
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(owned));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(shared), Is.Null);
            Assert.That(GdoOverlayAuthoringRepository.Find(node.Asset).TryGetField("KitchenData.Item", "IsSplitDepleted", out var field), Is.True);
            Assert.That(field.Value.ScalarValue, Is.EqualTo("True"));
        }

        private static DropdownMenu ContextActions(Node node)
        {
            var menu = new DropdownMenu();
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, node, null))
                node.BuildContextualMenu(evt);
            return menu;
        }
    }
}
