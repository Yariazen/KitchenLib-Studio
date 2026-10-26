using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoBundleGraphWindowTests
    {
        [Test]
        public void WindowHasUsableMinimumSize()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);

            Assert.That(window.minSize.x, Is.GreaterThanOrEqualTo(800f));
            Assert.That(window.minSize.y, Is.GreaterThanOrEqualTo(500f));
        }

        [Test]
        public void OpenRepairsTinyRestoredWindowBounds()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);
            window.position = new Rect(120f, 80f, 104f, 125f);

            GdoBundleGraphWindow reopened = GdoBundleGraphWindow.Open(null);

            Assert.That(reopened.position.width, Is.GreaterThanOrEqualTo(800f));
            Assert.That(reopened.position.height, Is.GreaterThanOrEqualTo(500f));
        }

        private const string Root = "Assets/GraphWindowTests";

        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.CreateFolder("Assets", "GraphWindowTests"); }
        [TearDown] public void TearDown() { foreach (GdoBundleGraphWindow window in Resources.FindObjectsOfTypeAll<GdoBundleGraphWindow>()) window.Close(); foreach (GdoExternalReferenceDialog dialog in Resources.FindObjectsOfTypeAll<GdoExternalReferenceDialog>()) dialog.Close(); foreach (GdoNameDialog dialog in Resources.FindObjectsOfTypeAll<GdoNameDialog>()) dialog.Close(); foreach (GdoProjectIdentityDialog dialog in Resources.FindObjectsOfTypeAll<GdoProjectIdentityDialog>()) dialog.Close(); foreach (GdoRenameConfirmationDialog dialog in Resources.FindObjectsOfTypeAll<GdoRenameConfirmationDialog>()) dialog.Close(); AssetDatabase.DeleteAsset(Root); Undo.ClearAll(); }

        [Test]
        public void OpenCreatesToolbarCanvasDiagnosticsAndRestoresSelectedGraph()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Graph.asset", "Test Author", "Window", "window.assets");
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Local"; item.ID = 101;
            AssetDatabase.CreateAsset(item, Root + "/Local.asset");
            GdoGraphNodeRecord node = GdoBundleGraphRepository.AddMember(graph, item, new Rect(12, 34, 250, 180));

            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(graph, node.Id);

            Assert.That(window.Graph, Is.SameAs(graph));
            Assert.That(window.rootVisualElement.Q<GdoGraphToolbar>(), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<GdoGraphToolbar>().Query<Label>().ToList()
                .Any(label => label.text.IndexOf("graph port", System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(window.rootVisualElement.Q<GdoBundleGraphView>(), Is.Not.Null);
            Assert.That(window.rootVisualElement.Q<GdoGraphDiagnosticsPanel>(), Is.Not.Null);
            Assert.That(window.GraphView.HasMiniMap, Is.True);
            Assert.That(window.GraphView.FindElement(node.Id), Is.Not.Null);
            Assert.That(window.GraphView.FocusedElementId, Is.EqualTo(node.Id));
        }

        [Test]
        public void SwitchingGraphRebuildsCanvasAndSearchFramesMatchingNode()
        {
            GdoBundleGraphAsset first = GdoBundleGraphRepository.Create(Root + "/First.asset", "Test Author", "First", "first.assets");
            GdoBundleGraphAsset second = GdoBundleGraphRepository.Create(Root + "/Second.asset", "Test Author", "Second", "second.assets");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(second, ExternalGdoCatalogRepository.VanillaProviderKey, 222, "KitchenData.Item", new Rect(4, 8, 220, 120));
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(first);

            window.SelectGraph(second);
            bool found = window.GraphView.SearchAndFrame("222");

            Assert.That(window.Graph, Is.SameAs(second));
            Assert.That(found, Is.True);
            Assert.That(window.GraphView.FocusedElementId, Is.EqualTo(external.Id));
        }

        [Test]
        public void OpeningWithoutGraphShowsFirstRunActionsAndDisablesGraphCommands()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);

            VisualElement onboarding = window.rootVisualElement.Q("gdo-graph-onboarding");
            Assert.That(onboarding, Is.Not.Null);
            Assert.That(onboarding.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(onboarding.Q<Button>("create-bundle-project").text, Is.EqualTo("Create Bundle Project"));
            Assert.That(onboarding.Q<Button>("open-bundle-project").text, Is.EqualTo("Open Bundle Project"));
            Assert.That(window.rootVisualElement.Q("gdo-graph-authoring-toolbar"), Is.Null);
            Assert.That(window.rootVisualElement.Q("build-gdo-bundle").enabledSelf, Is.False);
            Assert.That(window.rootVisualElement.Q("external-id"), Is.Null);
            Assert.That(window.rootVisualElement.Q("external-type"), Is.Null);
        }

        [Test]
        public void CreatingBundleGraphSelectsItAndShowsEmptyBundleGuidance()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);

            GdoBundleGraphAsset graph = window.CreateProject(Root, "Test Author", "Breakfast Menu");

            Assert.That(window.Graph, Is.SameAs(graph));
            Assert.That(AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(Root + "/Breakfast Menu/Breakfast Menu.asset"), Is.SameAs(graph));
            Assert.That(AssetDatabase.IsValidFolder(Root + "/Breakfast Menu/GDOs"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Root + "/Breakfast Menu/Content"), Is.True);
            Assert.That(graph.BundleName, Is.EqualTo("Breakfast Menu"));
            Assert.That(graph.BundleFileName, Is.EqualTo("breakfast-menu.assets"));
            VisualElement empty = window.rootVisualElement.Q("gdo-empty-graph");
            Assert.That(empty, Is.Not.Null);
            Assert.That(empty.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(window.rootVisualElement.Q("set-project-author"), Is.Null);
            Assert.That(window.rootVisualElement.Q("build-gdo-bundle").enabledSelf, Is.False);
            Assert.That(window.rootVisualElement.Q("gdo-graph-authoring-toolbar"), Is.Null);
        }

        [Test]
        public void EmptyCanvasMenuContainsAuthoringActionsButNodeMenuDoesNot()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Canvas Menu.asset", "Test Author", "Canvas Menu", "canvas-menu.assets");
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Local"; item.ID = 101;
            AssetDatabase.CreateAsset(item, Root + "/Canvas Menu Item.asset");
            GdoGraphNodeRecord node = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(graph);

            DropdownMenu canvas = CanvasMenu(window.GraphView, window.GraphView);
            string[] names = canvas.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name).ToArray();

            Assert.That(names, Does.Contain("Add Selected GDO"));
            Assert.That(names, Does.Contain("External Reference…"));
            Assert.That(names.Any(name => name.IndexOf("Junction",
                System.StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(names, Does.Contain("Rename Project…"));
            Assert.That(names, Does.Contain("Collect Project Overlays"));
            Assert.That(names.Count(name => name.StartsWith("Create GDO/")),
                Is.EqualTo(GdoGraphAssetCommands.SupportedRuntimeTypes.Count));

            DropdownMenu nodeMenu = CanvasMenu(window.GraphView, window.GraphView.FindElement(node.Id));
            Assert.That(nodeMenu.MenuItems().OfType<DropdownMenuAction>()
                .Any(action => action.name == "Add Selected GDO"), Is.False);
        }

        private static DropdownMenu CanvasMenu(GdoBundleGraphView view, VisualElement target)
        {
            var menu = new DropdownMenu();
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, target, null))
                view.BuildCanvasContextMenu(evt);
            menu.PrepareForDisplay(null);
            return menu;
        }

        [Test]
        public void NameOnlyProjectCreationAlwaysUsesModContent()
        {
            const string projectName = "Default Root Window Test";
            string projectPath = "Assets/ModContent/" + projectName;
            AssetDatabase.DeleteAsset(projectPath);
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);
            try
            {
                GdoBundleGraphAsset graph = window.CreateProject(
                    GdoBundleProjectRepository.DefaultRoot, "Test Author", projectName);

                Assert.That(AssetDatabase.GetAssetPath(graph),
                    Is.EqualTo(projectPath + "/" + projectName + ".asset"));
                Assert.That(window.Graph, Is.SameAs(graph));
            }
            finally
            {
                window.Close();
                AssetDatabase.DeleteAsset(projectPath);
            }
        }

        [Test]
        public void NameOnlyCreationShowsDerivedDestinationAndKeepsOriginalProjectWhenSelectionChanges()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);
            GdoBundleGraphAsset first = window.CreateProject(Root, "Test Author", "Bakery");
            GdoNameDialog dialog = window.OpenCreateGdoDialog("KitchenData.Item");
            dialog.CreateGUI();
            dialog.rootVisualElement.Q<TextField>("gdo-name").value = "Flour";
            Assert.That(dialog.rootVisualElement.Query<TextField>().ToList().Count, Is.EqualTo(1));
            Assert.That(dialog.rootVisualElement.Q<Label>("derived-destination").text,
                Is.EqualTo(Root + "/Bakery/GDOs/Flour.asset"));
            window.CreateProject(Root, "Test Author", "Cafe");

            Assert.That(dialog.Accept("Flour"), Is.True);

            Assert.That(window.Graph, Is.SameAs(first));
            Assert.That(first.Nodes.Single().Asset.name, Is.EqualTo("Flour"));
            Assert.That(AssetDatabase.GetAssetPath(first.Nodes.Single().Asset), Is.EqualTo(Root + "/Bakery/GDOs/Flour.asset"));
            Assert.That(window.GraphView.FocusedElementId, Is.EqualTo(first.Nodes.Single().Id));
        }

        [Test]
        public void RenameRequiresCompatibilityConsentThenReloadsAndReselectsGdoByGuid()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);
            GdoBundleGraphAsset graph = window.CreateProject(Root, "Test Author", "Bakery");
            GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Flour", new Rect());
            string guid = node.AssetGuid;
            window.SelectGraph(graph);
            var menu = new DropdownMenu();
            var view = (GdoGraphNodeView)window.GraphView.FindElement(node.Id);
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, view, null))
                view.BuildContextualMenu(evt);
            menu.PrepareForDisplay(null);
            DropdownMenuAction rename = menu.MenuItems().OfType<DropdownMenuAction>().Single(action => action.name == "Rename GDO");
            Assert.That(rename.status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            rename.Execute();
            GdoNameDialog name = Resources.FindObjectsOfTypeAll<GdoNameDialog>().Single();
            Assert.That(name.Accept("Bread Flour"), Is.True);
            GdoRenameConfirmationDialog confirmation = Resources.FindObjectsOfTypeAll<GdoRenameConfirmationDialog>().Single();

            Assert.That(confirmation.Confirm(false), Is.False);
            Assert.That(node.Asset.name, Is.EqualTo("Flour"));
            Assert.That(confirmation.Confirm(true), Is.True);

            GdoGraphNodeRecord renamed = window.Graph.Nodes.Single(value => value.AssetGuid == guid);
            Assert.That(renamed.Asset.name, Is.EqualTo("Bread Flour"));
            Assert.That(Selection.activeObject, Is.SameAs(renamed.Asset));
            Assert.That(window.GraphView.FocusedElementId, Is.EqualTo(renamed.Id));
                Assert.That(((GdoGraphNodeView)window.GraphView.FindElement(renamed.Id)).title, Is.EqualTo("Properties"));
                Assert.That(window.GraphView.FindOwnerGroup(renamed.Id).title, Is.EqualTo("Bread Flour"));
        }

        [Test]
        public void ProjectRenameConfirmationReloadsMovedGraphByGuidAndPreservesFocusedGdo()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);
            GdoBundleGraphAsset graph = window.CreateProject(Root, "Test Author", "Bakery");
            GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Flour", new Rect());
            string graphGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph));
            window.SelectGraph(graph);
            window.GraphView.AddToSelection(window.GraphView.FindElement(node.Id));
            Assert.That(window.OpenRenameProjectDialog().Accept("New Bakery"), Is.True);
            GdoRenameConfirmationDialog confirmation = Resources.FindObjectsOfTypeAll<GdoRenameConfirmationDialog>().Single();
            confirmation.Close();
            Assert.That(graph.BundleName, Is.EqualTo("Bakery"));
            window.OpenRenameProjectDialog().Accept("New Bakery");

            Assert.That(Resources.FindObjectsOfTypeAll<GdoRenameConfirmationDialog>().Single().Confirm(true), Is.True);

            Assert.That(AssetDatabase.GetAssetPath(window.Graph), Is.EqualTo(Root + "/New Bakery/New Bakery.asset"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(window.Graph)), Is.EqualTo(graphGuid));
            Assert.That(window.Graph.BundleName, Is.EqualTo("New Bakery"));
            Assert.That(window.GraphView.FocusedElementId, Is.EqualTo(node.Id));
            Assert.That(Selection.activeObject, Is.SameAs(window.Graph.Nodes.Single().Asset));
        }

        [Test]
        public void InvalidGraphShowsDiagnosticsAndExplainsDisabledBuild()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Invalid");
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Needs ID"; item.ID = 0;
            AssetDatabase.CreateAsset(item, GdoBundleProjectRepository.GdoFolder(graph) + "/Needs ID.asset");
            GdoBundleGraphRepository.AddMember(graph, item, new Rect());

            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(graph);

            VisualElement build = window.rootVisualElement.Q("build-gdo-bundle");
            Assert.That(build.enabledSelf, Is.False);
            Assert.That(build.tooltip, Does.Contain("Resolve"));
            GdoGraphDiagnosticsPanel diagnostics = window.rootVisualElement.Q<GdoGraphDiagnosticsPanel>();
            Assert.That(diagnostics.IsExpanded, Is.True);
            Assert.That(diagnostics.StatusText, Does.Contain("1 issue"));
        }

        [Test]
        public void GraphConnectionsRefreshVisibleValidationStateImmediately()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Live");
            Item owner = ScriptableObject.CreateInstance<Item>(); owner.name = "Owner"; owner.ID = GdoProjectIdentity.RuntimeId("Live", "Owner");
            Item first = ScriptableObject.CreateInstance<Item>(); first.name = "First"; first.ID = GdoProjectIdentity.RuntimeId("Live", "First");
            Item second = ScriptableObject.CreateInstance<Item>(); second.name = "Second"; second.ID = GdoProjectIdentity.RuntimeId("Live", "Second");
            AssetDatabase.CreateAsset(owner, GdoBundleProjectRepository.GdoFolder(graph) + "/Owner.asset");
            AssetDatabase.CreateAsset(first, GdoBundleProjectRepository.GdoFolder(graph) + "/First.asset");
            AssetDatabase.CreateAsset(second, GdoBundleProjectRepository.GdoFolder(graph) + "/Second.asset");
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord firstNode = GdoBundleGraphRepository.AddMember(graph, first, new Rect());
            GdoGraphNodeRecord secondNode = GdoBundleGraphRepository.AddMember(graph, second, new Rect());
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(graph);
            GdoGraphDiagnosticsPanel diagnostics = window.rootVisualElement.Q<GdoGraphDiagnosticsPanel>();
            Assert.That(diagnostics.StatusText, Does.Contain("0 connection(s)"));

            GdoGraphEdgeRecord edge = window.GraphView.Connect(firstNode.Id, ownerNode.Id,
                "KitchenData.Item.DirtiesTo", 0);

            Assert.That(diagnostics.StatusText, Does.Contain("1 connection(s)"));
            Assert.That(window.rootVisualElement.Q("build-gdo-bundle").enabledSelf, Is.True);

            window.GraphView.Disconnect(edge.Id);

            Assert.That(diagnostics.StatusText, Does.Contain("0 connection(s)"));
            Assert.That(window.rootVisualElement.Q("build-gdo-bundle").enabledSelf, Is.True);
        }

        [Test]
        public void ExternalReferenceDialogKeepsGraphItWasOpenedFor()
        {
            GdoBundleGraphAsset first = GdoBundleGraphRepository.Create(Root + "/FirstExternal.asset", "Test Author", "First", "first.assets");
            GdoBundleGraphAsset second = GdoBundleGraphRepository.Create(Root + "/SecondExternal.asset", "Test Author", "Second", "second.assets");
            GdoBundleGraphAsset acceptedGraph = null;
            GdoExternalReferenceDialog dialog = GdoExternalReferenceDialog.Open(first,
                (graph, _, __, ___) => acceptedGraph = graph);

            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(first);
            window.SelectGraph(second);
            dialog.Accept(ExternalGdoCatalogRepository.VanillaProviderKey, 9001, "KitchenData.Item");

            Assert.That(acceptedGraph, Is.SameAs(first));
        }

        [Test]
        public void ExternalReferenceDialogSelectsVanillaCatalogEntryWithoutManualIdEntry()
        {
            string catalogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "graph-picker-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ExternalGdoCatalogRepository repository = ExternalGdoPickerTests.ReadyRepository(catalogPath);
                GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/CatalogExternal.asset", "Test Author", "Catalog", "catalog.assets");
                int acceptedId = 0;
                string acceptedType = null;
                GdoExternalReferenceDialog dialog = GdoExternalReferenceDialog.Open(graph,
                    (_, __, id, type) => { acceptedId = id; acceptedType = type; }, "KitchenData.Item", repository);

                Button manualAccept = dialog.rootVisualElement.Q<Button>("accept-manual-reference");
                Assert.That(dialog.rootVisualElement.Q<Button>("cancel-external-reference"), Is.Not.Null);
                Assert.That(manualAccept, Is.Not.Null);
                Assert.That(manualAccept.text, Is.EqualTo("Add Reference"));
                Assert.That(manualAccept.style.display.value, Is.EqualTo(DisplayStyle.None));
                dialog.Picker.SetManualExpanded(true);
                Assert.That(manualAccept.style.display.value, Is.EqualTo(DisplayStyle.Flex));

                dialog.Picker.Select(repository.Find(ExternalGdoCatalogRepository.VanillaProviderKey,
                    1657174953, "KitchenData.Item"));

                Assert.That(acceptedId, Is.EqualTo(1657174953));
                Assert.That(acceptedType, Is.EqualTo("KitchenData.Item"));
            }
            finally
            {
                if (System.IO.File.Exists(catalogPath)) System.IO.File.Delete(catalogPath);
            }
        }

        [Test]
        public void ExistingExternalReferenceUsesSaveActionOnlyInsideManualMode()
        {
            string catalogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "graph-picker-edit-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ExternalGdoCatalogRepository repository = ExternalGdoPickerTests.ReadyRepository(catalogPath);
                GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                    Root + "/EditCatalogExternal.asset", "Test Author", "Edit Catalog", "edit-catalog.assets");
                GdoExternalReferenceDialog dialog = GdoExternalReferenceDialog.Open(graph, (_, __, ___, ____) => { },
                    "KitchenData.Item", repository, 1657174953, "KitchenData.Item");
                Button manualAccept = dialog.rootVisualElement.Q<Button>("accept-manual-reference");

                Assert.That(manualAccept.text, Is.EqualTo("Save Reference"));
                Assert.That(manualAccept.style.display.value, Is.EqualTo(DisplayStyle.None));
                dialog.Picker.SetManualExpanded(true);
                Assert.That(manualAccept.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }
            finally
            {
                if (System.IO.File.Exists(catalogPath)) System.IO.File.Delete(catalogPath);
            }
        }

        [Test]
        public void UndoingConnectionReloadsCanvasAndValidationStatus()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Undo");
            Item owner = ScriptableObject.CreateInstance<Item>(); owner.name = "Undo Owner"; owner.ID = GdoProjectIdentity.RuntimeId("Undo", "Undo Owner");
            Item target = ScriptableObject.CreateInstance<Item>(); target.name = "Undo Target"; target.ID = GdoProjectIdentity.RuntimeId("Undo", "Undo Target");
            AssetDatabase.CreateAsset(owner, GdoBundleProjectRepository.GdoFolder(graph) + "/Undo Owner.asset");
            AssetDatabase.CreateAsset(target, GdoBundleProjectRepository.GdoFolder(graph) + "/Undo Target.asset");
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(graph);

            Undo.IncrementCurrentGroup();
            window.GraphView.Connect(targetNode.Id, ownerNode.Id, "KitchenData.Item.DirtiesTo", 0);
            Assert.That(window.rootVisualElement.Q<GdoGraphDiagnosticsPanel>().StatusText, Does.Contain("1 connection(s)"));

            Undo.PerformUndo();

            Assert.That(window.GraphView.edges.ToList(), Is.Empty);
            Assert.That(window.rootVisualElement.Q<GdoGraphDiagnosticsPanel>().StatusText, Does.Contain("0 connection(s)"));
        }

        [Test]
        public void UndoingRelationshipStructureReloadsRelationshipNodes()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Relationship Undo");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.ItemGroup", "Group", new Rect());
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(graph);

            Undo.IncrementCurrentGroup();
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph).CreateRelationship(
                owner.Id, "KitchenData.ItemGroup.Sets[]", 0, new Rect());
            window.SelectGraph(graph);
            Assert.That(window.GraphView.FindElement(relationship.Id), Is.Not.Null);

            Undo.PerformUndo();

            Assert.That(window.GraphView.FindElement(relationship.Id), Is.Null);
        }

        [Test]
        public void WindowDisableDisposesSnapshotService()
        {
            GdoBundleGraphWindow window = GdoBundleGraphWindow.Open(null);
            var service = new WindowSnapshotService();
            var serviceField = typeof(GdoBundleGraphView).GetField("_snapshotService",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var disable = typeof(GdoBundleGraphWindow).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(serviceField, Is.Not.Null);
            Assert.That(disable, Is.Not.Null);
            serviceField.SetValue(window.GraphView, service);

            disable.Invoke(window, null);

            Assert.That(service.Disposed, Is.True);
        }

        private sealed class WindowSnapshotService : IExternalGdoSnapshotService
        {
            public bool Disposed;

            public System.IDisposable Request(string providerKey, int gdoId, string runtimeType,
                System.Action<ExternalGdoSnapshotResult> completed) => null;

            public void Dispose() => Disposed = true;
        }
    }
}
