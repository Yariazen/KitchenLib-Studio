using System;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphNodeViewTests
    {
        private const string Root = "Assets/GraphNodeViewTests";
        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.CreateFolder("Assets", "GraphNodeViewTests"); }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(Root); Undo.ClearAll(); }

        [Test]
        public void LocalActionsCreateSelectAndFrameSchemaRelationshipBesideOwner()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Bakery");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.ItemGroup", "Mix", new Rect(10, 20, 240, 160));
            string renameId = null;
            int changes = 0;
            var view = new GdoBundleGraphView(() => changes++, id => renameId = id);
            view.Load(graph);
            var ownerView = (GdoGraphNodeView)view.FindElement(owner.Id);
            DropdownMenu menu = Actions(ownerView);
            DropdownMenuAction rename = menu.MenuItems().OfType<DropdownMenuAction>().Single(action => action.name == "Rename GDO");
            rename.Execute();
            Assert.That(renameId, Is.EqualTo(owner.Id));
            DropdownMenu groupMenu = Actions(view.FindOwnerGroup(owner.Id));
            DropdownMenuAction sets = groupMenu.MenuItems().OfType<DropdownMenuAction>().Single(action => action.name == "Add Relationship/Sets");
            Assert.That(groupMenu.MenuItems().OfType<DropdownMenuAction>().Any(action => action.name == "Add Relationship/Rewards"), Is.True);

            try
            {
                sets.Execute();
                GdoGraphRelationshipRecord relationship = graph.Relationships.Single();
                Assert.That(relationship.FieldPath, Is.EqualTo("KitchenData.ItemGroup.Sets[]"));
                Assert.That(relationship.Position.x, Is.GreaterThan(owner.Position.xMax));
                Assert.That(relationship.Position.y, Is.EqualTo(owner.Position.y));
                Assert.That(view.FocusedElementId, Is.EqualTo(relationship.Id));
                Assert.That(view.selection.Single(), Is.SameAs(view.FindElement(relationship.Id)));
                Assert.That(view.FindElement(owner.Id), Is.Not.SameAs(ownerView));
                Assert.That(changes, Is.EqualTo(1));
                var relationshipView = (GdoGraphRelationshipNodeView)view.FindElement(relationship.Id);
                GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
                Assert.That(group.containedElements.Contains(relationshipView), Is.True);
                Assert.That(view.edges.ToList(), Is.Empty);
                Assert.That(graph.Edges, Is.Empty, "The ownership line is editor structure, not a runtime GDO reference.");
                Assert.That(relationshipView.tooltip, Does.Contain("Mix").And.Contain("KitchenData.ItemGroup.Sets[]"));
                Actions(relationshipView).MenuItems().OfType<DropdownMenuAction>()
                    .Single(action => action.name == "Remove relationship").Execute();
                Assert.That(graph.Relationships, Is.Empty);
                Assert.That(view.FindElement(relationship.Id), Is.Null);
                Assert.That(view.FocusedElementId, Is.EqualTo(owner.Id));
                Assert.That(changes, Is.EqualTo(2));
            }
            finally
            {
                GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(owner.Asset);
                if (overlay != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(overlay));
            }
        }

        [Test]
        public void SingletonRelationshipActionDisablesAfterFirstNode()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Singleton");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Owner", new Rect());
            var view = new GdoBundleGraphView(); view.Load(graph);
            try
            {
                Actions(view.FindOwnerGroup(owner.Id)).MenuItems().OfType<DropdownMenuAction>()
                    .Single(action => action.name == "Add Relationship/Automatic Item Process").Execute();
                DropdownMenu menu = Actions(view.FindOwnerGroup(owner.Id));
                menu.PrepareForDisplay(null);
                Assert.That(menu.MenuItems().OfType<DropdownMenuAction>()
                    .Single(action => action.name == "Add Relationship/Automatic Item Process").status, Is.EqualTo(DropdownMenuAction.Status.Disabled));
            }
            finally
            {
                GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(owner.Asset);
                if (overlay != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(overlay));
            }
        }

        [Test]
        public void PortsUseExactOutputAndExpectedInputTypeColors()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Palette");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Colored Group", new Rect());
            var node = new GdoGraphNodeView(owner);

            Port output = node.outputContainer.Query<Port>().ToList().Single();
            Port itemInput = node.inputContainer.Query<Port>().ToList().First(port =>
                (port.userData as string) == "KitchenData.Item.DirtiesTo");

            Assert.That(output.portColor, Is.EqualTo(GdoGraphTypePalette.For("KitchenData.ItemGroup")));
            Assert.That(itemInput.portColor, Is.EqualTo(GdoGraphTypePalette.For("KitchenData.Item")));
            Assert.That(output.portColor, Is.Not.EqualTo(itemInput.portColor));
        }

        private static DropdownMenu Actions(Node node)
        {
            var menu = new DropdownMenu();
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, node, null))
                node.BuildContextualMenu(evt);
            return menu;
        }

        [Test]
        public void ExternalNodeMenuAddsRelatedIncomingAndOutgoingEntries()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Related Menu");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddExternal(graph,
                "workshop:2913877103", 43, "KitchenData.Item", new Rect(), "Chopped Pork");
            ExternalGdoCatalogEntry pork = CreateCatalogEntry("workshop:2913877103", 42, "Pork");
            ExternalGdoCatalogEntry cook = CreateCatalogEntry("vanilla:plateup", 100, "Cook");
            ExternalGdoCatalogEntry selected = null;
            var view = new GdoGraphExternalNodeView(record, related: () => new[]
            {
                CreateRelated(ExternalGdoRelationshipDirection.Incoming, "Result", "Processes[0].Result", pork),
                CreateRelated(ExternalGdoRelationshipDirection.Outgoing, "Process", "Processes[0].Process", cook)
            }, addRelated: entry => selected = entry);

            DropdownMenu menu = Actions(view);
            DropdownMenuAction incoming = menu.MenuItems().OfType<DropdownMenuAction>().Single(action =>
                action.name == "Related GDOs/Referenced By/Result/Pork");
            Assert.That(menu.MenuItems().OfType<DropdownMenuAction>().Any(action =>
                action.name == "Related GDOs/Process/Cook"), Is.True);
            incoming.Execute();

            Assert.That(selected, Is.SameAs(pork));
        }

        private static ExternalGdoCatalogEntry CreateCatalogEntry(string provider, int id, string name) =>
            (ExternalGdoCatalogEntry)Activator.CreateInstance(typeof(ExternalGdoCatalogEntry),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { provider, provider, id, "KitchenData.Item", name, null, null }, null);

        private static ExternalGdoRelatedEntry CreateRelated(ExternalGdoRelationshipDirection direction,
            string label, string path, ExternalGdoCatalogEntry entry) =>
            (ExternalGdoRelatedEntry)Activator.CreateInstance(typeof(ExternalGdoRelatedEntry),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { direction, label, path, entry }, null);

        private static DropdownMenu Actions(GdoGraphOwnerGroup group)
        {
            var menu = new DropdownMenu();
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu,
                group, null))
                group.BuildContextualMenu(evt);
            return menu;
        }

        [Test]
        public void LocalNodeIsCompactButKeepsTypedRelationshipPortsVisible()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Ingredient"; item.ID = 301;
            AssetDatabase.CreateAsset(item, Root + "/Item.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Graph.asset", "Test Author", "Graph", "graph.assets");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddMember(graph, item, new Rect(10, 20, 240, 160));

            var view = new GdoGraphNodeView(record);

            Assert.That(view.RecordId, Is.EqualTo(record.Id));
            Assert.That(view.ClassListContains("kls-gdo-node--local"), Is.True);
            Assert.That(view.IsExpanded, Is.False);
            Assert.That(view.outputContainer.Query<Port>().ToList().Any(port => port.portName == "Item"), Is.True);
            Assert.That(view.inputContainer.Query<Port>().ToList().Any(port => port.portName.Contains("Dirties To")), Is.True);
            Assert.That(view.InitialPosition, Is.EqualTo(record.Position));
        }

        [Test]
        public void GroupedNodeSuppressesItsOwnDisclosureButStandaloneNodeKeepsIt()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Disclosure"; item.ID = 1301;
            AssetDatabase.CreateAsset(item, Root + "/Disclosure.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Disclosure Graph.asset", "Test Author", "Disclosure", "disclosure.assets");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddMember(graph, item, new Rect());

            var grouped = new GdoGraphNodeView(record, groupedDisplay: true);
            var standalone = new GdoGraphNodeView(record);

            Assert.That(GdoGraphHeaderButton.IsNativeCollapseVisible(grouped), Is.False);
            Assert.That(GdoGraphHeaderButton.IsNativeCollapseVisible(standalone), Is.True);
        }

        [Test]
        public void BrokenAndExternalNodesRemainRenderableAndMovementPersists()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Broken Soon"; item.ID = 302;
            AssetDatabase.CreateAsset(item, Root + "/Broken.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Graph.asset", "Test Author", "Graph", "graph.assets");
            GdoGraphNodeRecord broken = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 999, "KitchenData.Item", new Rect(50, 60, 200, 100));
            AssetDatabase.DeleteAsset(Root + "/Broken.asset");
            var graphView = new GdoBundleGraphView();
            graphView.Load(graph);

            var brokenView = (GdoGraphNodeView)graphView.FindElement(broken.Id);
            Assert.That(brokenView.IsBroken, Is.True);
            Assert.That(brokenView.ClassListContains("kls-gdo-node--broken"), Is.True);
            GraphElement externalView = graphView.FindElement(external.Id);
            Assert.That(externalView, Is.TypeOf<GdoGraphExternalNodeView>());
            Assert.That(externalView.ClassListContains("kls-gdo-node--external"), Is.True);

            Rect moved = new Rect(90, 100, 250, 140);
            graphView.PersistPosition(external.Id, moved);
            Assert.That(graph.Nodes.Single(node => node.Id == external.Id).Position, Is.EqualTo(moved));
        }

        [Test]
        public void ExpandedStatePersistsAcrossViewReconstruction()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Expanded"; item.ID = 303;
            AssetDatabase.CreateAsset(item, Root + "/Expanded.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Graph.asset", "Test Author", "Graph", "graph.assets");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            var view = new GdoGraphNodeView(record, expanded => GdoBundleGraphRepository.SetExpanded(graph, record.Id, expanded));
            view.expanded = true;
            view.SyncExpandedState();

            var reopened = new GdoGraphNodeView(graph.Nodes.Single(node => node.Id == record.Id));
            Assert.That(reopened.IsExpanded, Is.True);
            Assert.That(reopened.EditorBody, Is.Not.Null);
        }

        [Test]
        public void RelationshipPortsUseReadableLabelsAndPreserveTechnicalPathInTooltip()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Readable"; item.ID = 405;
            AssetDatabase.CreateAsset(item, Root + "/Readable.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Readable Graph.asset", "Test Author", "Readable", "readable.assets");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddMember(graph, item, new Rect());

            var view = new GdoGraphNodeView(record);
            Port dirtiesTo = view.inputContainer.Query<Port>().ToList()
                .First(port => (string)port.userData == "KitchenData.Item.DirtiesTo");

            Assert.That(dirtiesTo.portName, Is.EqualTo("Dirties To · Item"));
            Assert.That(dirtiesTo.tooltip, Does.Contain("KitchenData.Item.DirtiesTo"));
            Assert.That(dirtiesTo.tooltip, Does.Contain("KitchenData.Item"));
        }

        [Test]
        public void RelationshipHeaderMoveControlsHonorEnabledProviders()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Ordering");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            int moved = 0;
            var view = new GdoGraphRelationshipNodeView(relationship, graph, null, null,
                delta => moved = delta, delta => delta > 0);
            Button up = view.Q<Button>("relationship-move-up");
            Button down = view.Q<Button>("relationship-move-down");

            Assert.That(view.titleContainer[0], Is.SameAs(view.Q<Button>("relationship-collapse-button")));
            Assert.That(up.text, Is.EqualTo("↑"));
            Assert.That(down.text, Is.EqualTo("↓"));
            Assert.That(up.ClassListContains("kls-graph-reorder-button"), Is.True);
            Assert.That(view.Q<Label>("relationship-owner"), Is.Null);
            Assert.That(view.Q<VisualElement>("relationship-reorder-controls"), Is.Not.Null);
            Assert.That(GdoGraphHeaderButton.IsNativeCollapseVisible(view), Is.False);
            Assert.That(view.titleContainer.Query<Button>().ToList().Select(button => button.name),
                Is.EqualTo(new[] { "relationship-collapse-button", "relationship-move-up", "relationship-move-down" }));
            Assert.That(up.enabledSelf, Is.False);
            Assert.That(down.enabledSelf, Is.True);
            typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Invoke(down.clickable, new object[] { null });
            Assert.That(moved, Is.EqualTo(1));
        }

        [Test]
        public void SharedHeaderButtonHasAccessibleRoleAndHitTarget()
        {
            Button button = GdoGraphHeaderButton.Create(
                "test-disclosure", "▸", "Show details", "kls-graph-disclosure-button", () => { });

            Assert.That(button.name, Is.EqualTo("test-disclosure"));
            Assert.That(button.text, Is.EqualTo("▸"));
            Assert.That(button.tooltip, Is.EqualTo("Show details"));
            Assert.That(button.ClassListContains("kls-graph-icon-button"), Is.True);
            Assert.That(button.ClassListContains("kls-graph-disclosure-button"), Is.True);
            Assert.That(button.style.width.value.value, Is.EqualTo(24f));
            Assert.That(button.style.height.value.value, Is.EqualTo(24f));
        }

        [Test]
        public void NodesDoNotOfferDeletionUntilItCanBePersisted()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Protected"; item.ID = 406;
            AssetDatabase.CreateAsset(item, Root + "/Protected.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Protected Graph.asset", "Test Author", "Protected", "protected.assets");
            GdoGraphNodeRecord local = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 407, "KitchenData.Item", new Rect());

            var localView = new GdoGraphNodeView(local);
            var externalView = new GdoGraphExternalNodeView(external);

            Assert.That((localView.capabilities & Capabilities.Deletable) == 0, Is.True);
            Assert.That((externalView.capabilities & Capabilities.Deletable) == 0, Is.True);
        }

        [Test]
        public void ExternalNodeStartsWithSnapshotPlaceholder()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Snapshot Placeholder.asset", "Test Author", "Snapshot", "snapshot.assets");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 701, "KitchenData.Item", new Rect());

            var view = new GdoGraphExternalNodeView(external);
            Image image = view.Q<Image>("external-snapshot");
            Label status = view.Q<Label>("external-snapshot-status");

            Assert.That(image, Is.Not.Null);
            Assert.That(status, Is.Not.Null);
            Assert.That(image.image, Is.Null);
            Assert.That(status.text, Does.Contain("Snapshot"));
            Assert.That(view.extensionContainer.IndexOf(image),
                Is.LessThan(view.extensionContainer.IndexOf(view.Q<Label>("external-provenance"))));
        }

        [Test]
        public void ExternalNodeDisplaysGeneratedTextureAt128Pixels()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Snapshot Ready.asset", "Test Author", "Snapshot", "snapshot.assets");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 702, "KitchenData.Item", new Rect());
            var texture = new Texture2D(1, 1);
            try
            {
                var view = new GdoGraphExternalNodeView(external);
                view.SetSnapshotResult(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Ready, texture));
                Image image = view.Q<Image>("external-snapshot");

                Assert.That(image.image, Is.SameAs(texture));
                Assert.That(image.scaleMode, Is.EqualTo(ScaleMode.ScaleToFit));
                Assert.That(image.style.width.value.value, Is.EqualTo(128f));
                Assert.That(image.style.height.value.value, Is.EqualTo(128f));
                Assert.That(view.Q<Label>("external-snapshot-status").style.display.value,
                    Is.EqualTo(DisplayStyle.None));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ExternalNodeShowsUnavailableDiagnosticWithoutChangingPort()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Snapshot Missing.asset", "Test Author", "Snapshot", "snapshot.assets");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 703, "KitchenData.Item", new Rect());
            var view = new GdoGraphExternalNodeView(external);
            int ports = view.outputContainer.childCount;

            view.SetSnapshotResult(new ExternalGdoSnapshotResult(
                ExternalGdoSnapshotState.Unavailable, diagnostic: "No supported prefab"));

            Label status = view.Q<Label>("external-snapshot-status");
            Assert.That(status.text, Is.EqualTo("Snapshot unavailable"));
            Assert.That(status.tooltip, Is.EqualTo("No supported prefab"));
            Assert.That(view.outputContainer.childCount, Is.EqualTo(ports));
        }

        [Test]
        public void ExternalNodeShowsFailedSnapshotDiagnostic()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Snapshot Failed.asset", "Test Author", "Snapshot", "snapshot.assets");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 704, "KitchenData.Item", new Rect());
            var view = new GdoGraphExternalNodeView(external);

            view.SetSnapshotResult(new ExternalGdoSnapshotResult(
                ExternalGdoSnapshotState.Failed, diagnostic: "Decoder failed"));

            Label status = view.Q<Label>("external-snapshot-status");
            Assert.That(status.text, Is.EqualTo("Snapshot failed"));
            Assert.That(status.tooltip, Is.EqualTo("Decoder failed"));
        }

        [Test]
        public void UnsupportedExternalNodeOmitsSnapshotControls()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Snapshot Unsupported.asset", "Test Author", "Snapshot", "snapshot.assets");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 705,
                "KitchenData.Process", new Rect());
            var view = new GdoGraphExternalNodeView(external);

            view.SetSnapshotResult(new ExternalGdoSnapshotResult(
                ExternalGdoSnapshotState.Unsupported, diagnostic: "No snapshot for this GDO type."));

            Assert.That(view.Q<Image>("external-snapshot"), Is.Null);
            Assert.That(view.Q<Label>("external-snapshot-status"), Is.Null);
        }

        [Test]
        public void ReconstructedNodeTitleUsesPersistedGdoRename()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Bakery");
            GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Flour", new Rect());
            try
            {
                GdoBundleProjectRepository.RenameGdo(GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour"));
                string graphPath = AssetDatabase.GetAssetPath(graph);
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(graphPath);
                var view = new GdoGraphNodeView(graph.Nodes.Single(n => n.Id == node.Id));

                Assert.That(view.title, Is.EqualTo("Bread Flour"));
                Assert.That(view.RecordId, Is.EqualTo(node.Id));
            }
            finally
            {
                GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(node.Asset);
                if (overlay != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(overlay));
            }
        }
    }
}
