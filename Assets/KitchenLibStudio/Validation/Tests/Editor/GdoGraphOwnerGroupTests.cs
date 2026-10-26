using System;
using System.Collections;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphOwnerGroupTests
    {
        private const string Root = "Assets/GraphOwnerGroupTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "GraphOwnerGroupTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
            Undo.ClearAll();
        }

        [Test]
        public void LocalOwnerAndRelationshipsLoadIntoOneDerivedGroupWithoutOwnershipEdges()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Grouped");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Dough", new Rect(20, 30, 240, 160));
            GdoGraphRelationshipRecord sets = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(300, 30, 260, 180));
            GdoGraphRelationshipRecord rewards = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Rewards[]", new Rect(300, 230, 260, 180));
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 12345, "KitchenData.Item", new Rect(10, 300, 200, 100));

            var view = new GdoBundleGraphView();
            view.Load(graph);

            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
            Assert.That(group, Is.Not.Null);
            Assert.That(group.title, Is.EqualTo("Dough"));
            Assert.That(group.DerivedElementIds, Is.EquivalentTo(new[] { owner.Id, sets.Id, rewards.Id }));
            Assert.That(group.containedElements, Does.Contain(view.FindElement(owner.Id)));
            Assert.That(group.containedElements, Does.Contain(view.FindElement(sets.Id)));
            Assert.That(group.containedElements, Does.Contain(view.FindElement(rewards.Id)));
            Assert.That(group.containedElements.Contains(view.FindElement(external.Id)), Is.False);
            Assert.That(view.edges.ToList(), Is.Empty, "Ownership is represented by containment, not an edge.");
            Assert.That(((Node)view.FindElement(owner.Id)).title, Is.EqualTo("Properties"));
        }

        [Test]
        public void OwnerGroupUsesExplicitRemovalWhileAllGroupedElementsRemainProtectedFromNativeDeletion()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Deletion Target");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            var view = new GdoBundleGraphView();
            view.Load(graph);

            Assert.That((view.FindOwnerGroup(owner.Id).capabilities & Capabilities.Deletable) == 0, Is.True);
            Assert.That((view.FindElement(owner.Id).capabilities & Capabilities.Deletable) == 0, Is.True);
            Assert.That((view.FindElement(relationship.Id).capabilities & Capabilities.Deletable) == 0, Is.True);
        }

        [Test]
        public void OwnerGroupRemovalCommandRemovesGraphMemberButPreservesSourceAsset()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Context Removal");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            string sourcePath = AssetDatabase.GetAssetPath(owner.Asset);
            int changes = 0;
            int confirmations = 0;
            var view = new GdoBundleGraphView(() => changes++, confirmMemberRemoval: _ =>
            {
                confirmations++;
                return true;
            });
            view.Load(graph);

            view.FindOwnerGroup(owner.Id).RequestRemoval();

            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(graph.Nodes.Any(node => node.Id == owner.Id), Is.False);
            Assert.That(graph.Relationships.Any(relationship => relationship.OwnerNodeId == owner.Id), Is.False);
            Assert.That(view.FindOwnerGroup(owner.Id), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(sourcePath), Is.Not.Null);
        }

        [Test]
        public void RemoveGdoContextActionInvokesTheGroupRemovalCallback()
        {
            int removals = 0;
            var group = new GdoGraphOwnerGroup("owner", "Owner", removeGdo: () => removals++);
            DropdownMenu actions = Actions(group);
            actions.PrepareForDisplay(null);

            DropdownMenuAction remove = actions.MenuItems().OfType<DropdownMenuAction>()
                .Single(action => action.name == "Remove GDO");
            Assert.That(remove.status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            remove.Execute();

            Assert.That(removals, Is.EqualTo(1));
        }

        [Test]
        public void GroupBodyOffersTheRemoveGdoMenu()
        {
            var group = new GdoGraphOwnerGroup("owner", "Owner", removeGdo: () => { });
            var menu = new DropdownMenu();

            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, group, null))
                group.BuildContextualMenu(evt);

            Assert.That(menu.MenuItems().OfType<DropdownMenuAction>()
                .Any(action => action.name == "Remove GDO"), Is.True);
        }

        [Test]
        public void GroupProvidesDedicatedBackgroundContextSurface()
        {
            var group = new GdoGraphOwnerGroup("owner", "Owner", removeGdo: () => { });

            VisualElement surface = group.Q<VisualElement>("owner-group-context-surface");

            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.pickingMode, Is.EqualTo(PickingMode.Position));
            var menu = new DropdownMenu();
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, surface, null))
                group.BuildContextualMenu(evt);
            Assert.That(menu.MenuItems().OfType<DropdownMenuAction>()
                .Any(action => action.name == "Remove GDO"), Is.True);
        }

        [Test]
        public void GroupContextMenuDoesNotDuplicateRemovalForAContainedNode()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Child Context");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            var view = new GdoBundleGraphView();
            view.Load(graph);
            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
            GraphElement ownerView = view.FindElement(owner.Id);
            var menu = new DropdownMenu();

            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, ownerView, null))
                group.BuildContextualMenu(evt);

            Assert.That(menu.MenuItems().OfType<DropdownMenuAction>()
                .Any(action => action.name == "Remove GDO"), Is.False);
        }

        [UnityTest]
        public IEnumerator EdgeContextInsideOwnerGroupUsesTheGroupMenu()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(
                Root, "Test Author", "Edge Over Group Context");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect(120, 80, 240, 160));
            var view = new GdoBundleGraphView();
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(100, 100, 1000, 800);
                window.rootVisualElement.Add(view);
                window.Show();
                view.Load(graph);
                for (int frame = 0; frame < 8; frame++) yield return null;

                GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
                Rect groupPosition = group.GetPosition();
                Assert.That(float.IsNaN(groupPosition.width) || float.IsNaN(groupPosition.height), Is.False);
                var edge = new Edge();
                var menu = new DropdownMenu();
                using (var trigger = MouseDownEvent.GetPooled())
                using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, edge, null))
                {
                    var method = typeof(GdoBundleGraphView).GetMethod("TryBuildOwnerGroupContextMenu",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Assert.That(method, Is.Not.Null);
                    Assert.That(method.Invoke(view, new object[] { evt, groupPosition.center }), Is.True);
                }

                Assert.That(menu.MenuItems().OfType<DropdownMenuAction>()
                    .Any(action => action.name == "Remove GDO"), Is.True);
            }
            finally { window.Close(); }
        }

        [Test]
        public void OwnerGroupMenuOwnsRelationshipCreationAndPresentationActions()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Group Actions");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoRelationshipChoice repeated = controller.RelationshipChoices(owner.Id).First(choice => choice.IsRepeated);
            GdoRelationshipChoice singleton = controller.RelationshipChoices(owner.Id).First(choice => !choice.IsRepeated);
            var view = new GdoBundleGraphView();
            view.Load(graph);

            DropdownMenu actions = Actions(view.FindOwnerGroup(owner.Id));
            actions.PrepareForDisplay(null);
            Assert.That(actions.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Does.Contain("Add Relationship/" + repeated.Label));
            Assert.That(actions.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Does.Contain("Presentation/Full"));
            Assert.That(actions.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Does.Contain("Presentation/Nodes"));
            Assert.That(actions.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Does.Contain("Presentation/Connected"));

            DropdownMenuAction addSingleton = actions.MenuItems().OfType<DropdownMenuAction>()
                .Single(action => action.name == "Add Relationship/" + singleton.Label);
            Assert.That(addSingleton.status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            addSingleton.Execute();

            DropdownMenu after = Actions(view.FindOwnerGroup(owner.Id));
            after.PrepareForDisplay(null);
            Assert.That(after.MenuItems().OfType<DropdownMenuAction>()
                .Single(action => action.name == "Add Relationship/" + singleton.Label).status,
                Is.EqualTo(DropdownMenuAction.Status.Disabled));
            Assert.That(after.MenuItems().OfType<DropdownMenuAction>()
                .Single(action => action.name == "Add Relationship/" + repeated.Label).status,
                Is.EqualTo(DropdownMenuAction.Status.Normal));

            after.MenuItems().OfType<DropdownMenuAction>()
                .Single(action => action.name == "Presentation/Connected").Execute();
            Assert.That(graph.Nodes.Single(node => node.Id == owner.Id).PresentationMode,
                Is.EqualTo(GdoGraphPresentationMode.Connected));
            Assert.That(((GdoGraphNodeView)view.FindElement(owner.Id))
                .Q<Button>("properties-presentation-button").text, Is.EqualTo("−"));
        }

        [Test]
        public void DeleteOwnerGroupRemovesConnectedMemberWhenConfirmedAndPreservesSourceAsset()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Confirmed Delete");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 7234, "KitchenData.Item", new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord relationship = controller.CreateNextRelationship(
                owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            controller.Connect(external.Id, relationship.Id, "KitchenData.ItemGroup.Sets[].Items[0]", 0);
            string sourcePath = AssetDatabase.GetAssetPath(owner.Asset);
            int confirmations = 0;
            var view = new GdoBundleGraphView(confirmMemberRemoval: _ => { confirmations++; return true; });
            view.Load(graph);

            RemoveSelectedOwnerGroups(view, view.FindOwnerGroup(owner.Id));

            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(graph.Nodes.Any(node => node.Id == owner.Id), Is.False);
            Assert.That(graph.Relationships.Any(item => item.OwnerNodeId == owner.Id), Is.False);
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(view.FindOwnerGroup(owner.Id), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(sourcePath), Is.Not.Null);
        }

        [Test]
        public void DeleteOwnerGroupCancellationPreservesGraphAndVisibleGroup()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Cancelled Delete");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 7235, "KitchenData.Item", new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord relationship = controller.CreateNextRelationship(
                owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            GdoGraphEdgeRecord edge = controller.Connect(
                external.Id, relationship.Id, "KitchenData.ItemGroup.Sets[].Items[0]", 0);
            int confirmations = 0;
            var view = new GdoBundleGraphView(confirmMemberRemoval: _ => { confirmations++; return false; });
            view.Load(graph);
            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);

            RemoveSelectedOwnerGroups(view, group);

            Assert.That(confirmations, Is.EqualTo(1));
            Assert.That(graph.Nodes.Any(node => node.Id == owner.Id), Is.True);
            Assert.That(graph.Relationships.Any(item => item.Id == relationship.Id), Is.True);
            Assert.That(graph.Edges.Any(item => item.Id == edge.Id), Is.True);
            Assert.That(view.FindOwnerGroup(owner.Id), Is.SameAs(group));
            Assert.That(view.graphElements.ToList().Contains(group), Is.True);
        }

        [Test]
        public void BrokenOwnerKeepsItsGroupAndRemainsSearchableByCachedName()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Broken Group");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Missing Dough", new Rect());
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(280, 0, 260, 180));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(owner.Asset));

            var view = new GdoBundleGraphView();
            view.Load(graph);

            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
            Assert.That(group, Is.Not.Null);
            Assert.That(group.title, Is.EqualTo("Missing Dough"));
            Assert.That(group.DerivedElementIds, Is.EquivalentTo(new[] { owner.Id, relationship.Id }));
            Assert.That(view.SearchAndFrame("Missing Dough"), Is.True);
            Assert.That(view.selection.Single(), Is.SameAs(group));
        }

        [Test]
        public void StandaloneLocalNodeRetainsAssetNameOutsideGroupedGraphDisplay()
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            item.name = "Standalone Tomato"; item.ID = 7001;
            AssetDatabase.CreateAsset(item, Root + "/Standalone Tomato.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Graph.asset", "Test Author", "Graph", "graph.assets");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddMember(graph, item, new Rect());

            Assert.That(new GdoGraphNodeView(record).title, Is.EqualTo("Standalone Tomato"));
        }

        [Test]
        public void DerivedMembershipRejectsExternalAdditionsAndRestoresOwnedRelationships()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Membership");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(280, 0, 260, 180));
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 9876, "KitchenData.Item", new Rect());
            var view = new GdoBundleGraphView(); view.Load(graph);
            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);

            group.RemoveElement(view.FindElement(relationship.Id));
            group.AddElement(view.FindElement(external.Id));
            view.ReconcileOwnerGroups();

            Assert.That(group.containedElements.Contains(view.FindElement(relationship.Id)), Is.True);
            Assert.That(group.containedElements.Contains(view.FindElement(external.Id)), Is.False);
            Assert.That(group.DerivedElementIds, Is.EquivalentTo(new[] { owner.Id, relationship.Id }));
        }

        [Test]
        public void PersistMovedGroupUpdatesOnlyExplicitGroupAnchor()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Movement");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect(20, 30, 240, 160));
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(300, 30, 260, 180));
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 4321, "KitchenData.Item", new Rect(280, 0, 200, 100));
            var view = new GdoBundleGraphView(); view.Load(graph);
            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);

            var refreshPending = typeof(GdoBundleGraphView).GetProperty("IsEdgeGeometryRefreshPending",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(refreshPending, Is.Not.Null);
            Assert.That((bool)refreshPending.GetValue(view), Is.True);

            view.PersistMovedElements(new GraphElement[] { group }, new Vector2(400, 200));

            var pending = typeof(GdoGraphOwnerGroup).GetProperty("IsPositionRestorePending",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(pending, Is.Not.Null);
            Assert.That((bool)pending.GetValue(group), Is.False);
            Assert.That(owner.GroupPosition, Is.EqualTo(new Vector2(420, 230)));
            Assert.That(owner.Position.position, Is.EqualTo(new Vector2(20, 30)));
            Assert.That(owner.Position.size, Is.EqualTo(new Vector2(240, 160)));
            Assert.That(relationship.Position, Is.EqualTo(new Rect(300, 30, 260, 180)));
            Assert.That(external.Position, Is.EqualTo(new Rect(280, 0, 200, 100)));
        }

        [Test]
        public void PersistingGroupsStoresGraphSpacePositionsSoDistinctGroupsStaySeparatedAfterReload()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Round Trip");
            GdoGraphNodeRecord first = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "First", new Rect(100, 120, 240, 160));
            GdoGraphNodeRecord second = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Second", new Rect(600, 420, 240, 160));
            var view = new GdoBundleGraphView();
            view.Load(graph);
            GdoGraphOwnerGroup firstGroup = view.FindOwnerGroup(first.Id);
            GdoGraphOwnerGroup secondGroup = view.FindOwnerGroup(second.Id);
            view.PersistMovedElements(new GraphElement[] { firstGroup }, new Vector2(-40, -40));
            view.PersistMovedElements(new GraphElement[] { secondGroup }, new Vector2(60, -40));

            Assert.That(second.GroupPosition.x - first.GroupPosition.x, Is.GreaterThan(500f));
            Assert.That(second.GroupPosition.y - first.GroupPosition.y, Is.GreaterThan(200f));
            string graphPath = AssetDatabase.GetAssetPath(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            GdoBundleGraphAsset reopened = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(graphPath);
            GdoGraphNodeRecord reopenedFirst = reopened.Nodes.Single(node => node.Id == first.Id);
            GdoGraphNodeRecord reopenedSecond = reopened.Nodes.Single(node => node.Id == second.Id);
            Assert.That(reopenedSecond.GroupPosition.x - reopenedFirst.GroupPosition.x, Is.GreaterThan(500f));
            Assert.That(reopenedFirst.Position, Is.EqualTo(new Rect(100, 120, 240, 160)));
            Assert.That(reopenedSecond.Position, Is.EqualTo(new Rect(600, 420, 240, 160)));
        }

        [Test]
        public void GroupedChildrenCompactWithoutUnmountingTitlesOrPorts()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Compact");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoBundleGraphRepository.SetExpanded(graph, owner.Id, true);
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(280, 0, 260, 180));
            var view = new GdoBundleGraphView(); view.Load(graph);
            var ownerView = (GdoGraphNodeView)view.FindElement(owner.Id);
            var relationshipView = (GdoGraphRelationshipNodeView)view.FindElement(relationship.Id);
            int ownerPorts = ownerView.inputContainer.childCount + ownerView.outputContainer.childCount;
            int relationshipPorts = relationshipView.inputContainer.childCount + relationshipView.outputContainer.childCount;
            ownerView.SetGroupExpanded(false);
            relationshipView.SetGroupExpanded(false);

            Assert.That(ownerView.EditorBody, Is.Not.Null);
            Assert.That(ownerView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(relationshipView.EditorBody, Is.Not.Null);
            Assert.That(relationshipView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(ownerView.titleContainer.parent, Is.Not.Null);
            Assert.That(relationshipView.titleContainer.parent, Is.Not.Null);
            Assert.That(ownerView.inputContainer.childCount + ownerView.outputContainer.childCount, Is.EqualTo(ownerPorts));
            Assert.That(relationshipView.inputContainer.childCount + relationshipView.outputContainer.childCount,
                Is.EqualTo(relationshipPorts));

            ownerView.SetGroupExpanded(true);
            relationshipView.SetGroupExpanded(true);
            Assert.That(ownerView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(relationshipView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void DetachedCollapsedGroupDefersInitialLayoutPrimingUntilItIsMounted()
        {
            var group = new GdoGraphOwnerGroup("owner", "Owner");
            var initialLayoutPending = typeof(GdoGraphOwnerGroup).GetProperty("IsInitialLayoutPending",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var primeInitialLayout = typeof(GdoGraphOwnerGroup).GetMethod("PrimeInitialLayout",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(primeInitialLayout, Is.Not.Null);
            primeInitialLayout.Invoke(group, null);

            Assert.That(initialLayoutPending, Is.Not.Null);
            Assert.That((bool)initialLayoutPending.GetValue(group), Is.True);
            Assert.That(group.IsExpanded, Is.False);
        }

        [Test]
        public void GroupDisclosureCyclesNodesFullConnectedAndBackToNodes()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Compact Properties");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            var view = new GdoBundleGraphView(); view.Load(graph);
            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
            GdoGraphNodeView ownerView = (GdoGraphNodeView)view.FindElement(owner.Id);

            Assert.That(group.PresentationMode, Is.EqualTo(GdoGraphPresentationMode.Nodes));
            Button presentation = ownerView.Q<Button>("properties-presentation-button");
            Assert.That(presentation, Is.Not.Null);
            Assert.That(ownerView.titleContainer[0], Is.SameAs(presentation));
            Assert.That(group.Q<Button>("group-collapse-button"), Is.Null);
            Assert.That(GdoGraphHeaderButton.IsNativeCollapseVisible(ownerView), Is.False);
            Assert.That(ownerView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(presentation.text, Is.EqualTo("▸"));

            Click(presentation);

            Assert.That(group.PresentationMode, Is.EqualTo(GdoGraphPresentationMode.Full));
            Assert.That(ownerView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ownerView.EditorBody, Is.Not.Null);
            Assert.That(presentation.text, Is.EqualTo("▾"));

            Click(presentation);
            Assert.That(group.PresentationMode, Is.EqualTo(GdoGraphPresentationMode.Connected));
            Assert.That(ownerView.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ownerView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(presentation.text, Is.EqualTo("−"));

            Click(presentation);
            Assert.That(group.PresentationMode, Is.EqualTo(GdoGraphPresentationMode.Nodes));
            Assert.That(ownerView.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ownerView.extensionContainer.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(presentation.text, Is.EqualTo("▸"));
        }

        [Test]
        public void PresentationTogglePreservesGroupGraphPosition()
        {
            var preserve = typeof(GdoGraphOwnerGroup).GetMethod("PreserveGraphPosition",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(preserve, Is.Not.Null);
            Rect preserved = (Rect)preserve.Invoke(null, new object[]
            {
                new Rect(640, 360, 320, 420), new Rect(0, 0, 280, 240)
            });
            Assert.That(preserved, Is.EqualTo(new Rect(640, 360, 280, 240)));

            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(
                Root, "Test Author", "Stable Toggle Position");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect(20, 30, 240, 160));
            new GdoGraphConnectionController(graph).CreateNextRelationship(
                owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(20, 210, 260, 180));
            var view = new GdoBundleGraphView();
            view.Load(graph);
            GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
            var expected = new Vector2(640, 360);
            group.SetPosition(new Rect(expected, new Vector2(320, 420)));

            group.SetPresentation(GdoGraphPresentationMode.Full, false);
            group.Reflow();
            group.SetPresentation(GdoGraphPresentationMode.Nodes, false);
            group.Reflow();
            group.SetPresentation(GdoGraphPresentationMode.Connected, false);
            group.Reflow();

            Assert.That(group.GetPosition().position, Is.EqualTo(expected));
        }

        [Test]
        public void ConnectedPresentationKeepsOnlyIncidentCardsAndPorts()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Connected");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphNodeRecord item = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.Item", "Ingredient", new Rect());
            GdoGraphRelationshipRecord connected = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            GdoGraphRelationshipRecord unconnected = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            GdoGraphEdgeRecord edge = new GdoGraphConnectionController(graph).Connect(item.Id, connected.Id,
                "KitchenData.ItemGroup.Sets[].Items[0]", 0);
            GdoGraphEdgeRecord ownerEdge = new GdoGraphConnectionController(graph).Connect(item.Id, owner.Id,
                "KitchenData.Item.DirtiesTo", 1);
            GdoBundleGraphRepository.SetPresentationMode(graph, owner.Id, GdoGraphPresentationMode.Connected);

            var view = new GdoBundleGraphView();
            view.Load(graph);

            Assert.That(view.FindElement(connected.Id).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.FindElement(unconnected.Id).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.FindElement(owner.Id).style.display.value, Is.EqualTo(DisplayStyle.Flex));

            var ownerView = (GdoGraphNodeView)view.FindElement(owner.Id);
            Port[] ownerPorts = ownerView.inputContainer.Children().OfType<Port>().ToArray();
            Assert.That(ownerPorts.Single(port => (string)port.userData ==
                "KitchenData.Item.DirtiesTo").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ownerPorts.Where(port => (string)port.userData !=
                "KitchenData.Item.DirtiesTo").All(port =>
                port.style.display.value == DisplayStyle.None), Is.True);

            view.Disconnect(edge.Id);

            Assert.That(view.FindElement(connected.Id).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.FindElement(owner.Id).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            view.Disconnect(ownerEdge.Id);
            Assert.That(view.FindElement(owner.Id).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(ownerPorts.All(port => port.style.display.value == DisplayStyle.None), Is.True);
        }

        [Test]
        public void BrokenOwnerAndNewRelationshipInheritCollapsedGroupPresentation()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Collapsed");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect());
            GdoGraphRelationshipRecord relationship = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(280, 0, 260, 180));
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(owner.Asset));

            var view = new GdoBundleGraphView(); view.Load(graph);

            Assert.That(view.FindOwnerGroup(owner.Id).IsExpanded, Is.False);
            Assert.That(((GdoGraphNodeView)view.FindElement(owner.Id))
                .Q<Button>("properties-presentation-button"), Is.Not.Null);
            Assert.That(((GdoGraphNodeView)view.FindElement(owner.Id)).EditorBody, Is.Null);
            Assert.That(((GdoGraphRelationshipNodeView)view.FindElement(relationship.Id)).extensionContainer.style.display.value,
                Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ReflowHeightPrefersResolvedLayoutOverStaleSerializedHeight()
        {
            var method = typeof(GdoGraphOwnerGroup).GetMethod("EffectiveHeight",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That((float)method.Invoke(null, new object[]
                { new Rect(0, 0, 240, 160), new Rect(0, 0, 240, 540) }), Is.EqualTo(540f));
            Assert.That((float)method.Invoke(null, new object[]
                { new Rect(0, 0, 240, 160), new Rect(0, 0, 240, float.NaN) }), Is.EqualTo(160f));
        }

        [UnityTest]
        public IEnumerator FullPresentationWaitsForResolvedGeometryBeforeStackingCards()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Measured Full");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect(20, 30, 240, 160));
            GdoGraphRelationshipRecord sets = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(20, 80, 260, 120));
            GdoGraphRelationshipRecord processes = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.Item.Processes[]", new Rect(20, 100, 260, 120));
            GdoBundleGraphRepository.SetPresentationMode(graph, owner.Id, GdoGraphPresentationMode.Full);
            var view = new GdoBundleGraphView();
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(100, 100, 1000, 800);
                window.rootVisualElement.Add(view);
                window.Show();
                view.Load(graph);
                for (int index = 0; index < 5; index++) yield return null;

                Rect ownerPosition = view.FindElement(owner.Id).GetPosition();
                Rect setsPosition = view.FindElement(sets.Id).GetPosition();
                Rect processesPosition = view.FindElement(processes.Id).GetPosition();
                Assert.That(setsPosition.yMin, Is.GreaterThanOrEqualTo(ownerPosition.yMax + 10f));
                Assert.That(processesPosition.yMin, Is.GreaterThanOrEqualTo(setsPosition.yMax + 10f));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator CompactInitialMeasurementNeverShowsTheTemporaryFullState()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Hidden Measure");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect(20, 30, 240, 160));
            new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(20, 80, 260, 120));
            GdoBundleGraphRepository.SetPropertiesExpanded(graph, owner.Id, true);
            var view = new GdoBundleGraphView();
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(100, 100, 1000, 800);
                window.rootVisualElement.Add(view);
                window.Show();
                view.Load(graph);
                GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);

                Assert.That(group.style.visibility.value, Is.EqualTo(Visibility.Hidden),
                    "The temporary Full measurement state must not be painted.");
                for (int index = 0; index < 10; index++) yield return null;

                Assert.That(group.style.visibility.value, Is.EqualTo(Visibility.Visible));
                Assert.That(((GdoGraphNodeView)view.FindElement(owner.Id)).extensionContainer.style.display.value,
                    Is.EqualTo(DisplayStyle.None));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator CompactInitialLayoutIsNonOverlappingWhenItFirstBecomesVisible()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First Paint");
            GdoGraphNodeRecord owner = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "Owner", new Rect(20, 30, 240, 40));
            GdoGraphRelationshipRecord sets = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect(20, 40, 260, 40));
            GdoGraphRelationshipRecord processes = new GdoGraphConnectionController(graph)
                .CreateNextRelationship(owner.Id, "KitchenData.Item.Processes[]", new Rect(20, 50, 260, 40));
            var view = new GdoBundleGraphView();
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(100, 100, 1000, 800);
                window.rootVisualElement.Add(view);
                bool ignoredLogs = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                try { window.Show(); }
                finally { LogAssert.ignoreFailingMessages = ignoredLogs; }
                view.Load(graph);
                GdoGraphOwnerGroup group = view.FindOwnerGroup(owner.Id);
                for (int frame = 0; frame < 10 &&
                    group.style.visibility.value == Visibility.Hidden; frame++)
                    yield return null;

                Assert.That(group.style.visibility.value, Is.EqualTo(Visibility.Visible));
                Rect ownerPosition = view.FindElement(owner.Id).GetPosition();
                Rect setsPosition = view.FindElement(sets.Id).GetPosition();
                Rect processesPosition = view.FindElement(processes.Id).GetPosition();
                Assert.That(setsPosition.yMin, Is.GreaterThanOrEqualTo(ownerPosition.yMax + 10f));
                Assert.That(processesPosition.yMin, Is.GreaterThanOrEqualTo(setsPosition.yMax + 10f));
            }
            finally { window.Close(); }
        }

        [Test]
        public void VisibleExternalNodeRequestsSnapshotOnlyOnce()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Visible Snapshot");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 8123, "KitchenData.Item", new Rect(20, 30, 200, 100));
            var service = new RecordingSnapshotService();
            var view = new GdoBundleGraphView(snapshotServiceFactory: () => service,
                snapshotVisibility: _ => true);
            view.Load(graph);

            RefreshVisibleSnapshots(view);
            RefreshVisibleSnapshots(view);

            Assert.That(service.RequestedIds, Is.EqualTo(new[] { external.ExternalId }));
            Assert.That(((GdoGraphExternalNodeView)view.FindElement(external.Id))
                .Q<Label>("external-snapshot-status").text, Is.EqualTo("Generating snapshot…"));
        }

        [Test]
        public void OffscreenExternalNodeDoesNotRequestSnapshot()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Offscreen Snapshot");
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 8124, "KitchenData.Item", new Rect());
            var service = new RecordingSnapshotService();
            int servicesCreated = 0;
            var view = new GdoBundleGraphView(snapshotServiceFactory: () =>
                { servicesCreated++; return service; },
                snapshotVisibility: _ => false);
            view.Load(graph);

            RefreshVisibleSnapshots(view);

            Assert.That(service.RequestedIds, Is.Empty);
            Assert.That(servicesCreated, Is.Zero);
        }

        [Test]
        public void VisibleUnsupportedExternalNodeDoesNotCreateSnapshotService()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(
                Root, "Test Author", "Unsupported Snapshot");
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, 8126,
                "KitchenData.Process", new Rect(20, 30, 200, 100));
            int servicesCreated = 0;
            var view = new GdoBundleGraphView(snapshotServiceFactory: () =>
                { servicesCreated++; return new RecordingSnapshotService(); },
                snapshotVisibility: _ => true);
            view.Load(graph);

            RefreshVisibleSnapshots(view);

            Assert.That(servicesCreated, Is.Zero);
            var subscriptions = (System.Collections.IDictionary)typeof(GdoBundleGraphView)
                .GetField("_snapshotSubscriptions",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(view);
            Assert.That(subscriptions.Count, Is.Zero);
            var externalView = (GdoGraphExternalNodeView)view.FindElement(external.Id);
            Assert.That(externalView.Q<Image>("external-snapshot"), Is.Null);
            Assert.That(externalView.Q<Label>("external-snapshot-status"), Is.Null);
        }

        [Test]
        public void PartialViewportOverlapCountsAsVisibleButDetachedOrHiddenDoesNot()
        {
            var method = typeof(GdoBundleGraphView).GetMethod("IsSnapshotVisible",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var viewport = new Rect(0, 0, 100, 100);

            Assert.That((bool)method.Invoke(null, new object[]
                { new Rect(90, 90, 40, 40), viewport, true, DisplayStyle.Flex }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[]
                { new Rect(101, 20, 40, 40), viewport, true, DisplayStyle.Flex }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[]
                { new Rect(20, 20, 40, 40), viewport, false, DisplayStyle.Flex }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[]
                { new Rect(20, 20, 40, 40), viewport, true, DisplayStyle.None }), Is.False);
        }

        [Test]
        public void GraphReloadDisposesSnapshotServiceAndSubscriptions()
        {
            GdoBundleGraphAsset first = GdoBundleProjectRepository.Create(Root, "Test Author", "First Snapshot");
            GdoBundleGraphRepository.AddExternal(first, ExternalGdoCatalogRepository.VanillaProviderKey, 8125, "KitchenData.Item", new Rect());
            GdoBundleGraphAsset second = GdoBundleProjectRepository.Create(Root, "Test Author", "Second Snapshot");
            var service = new RecordingSnapshotService();
            var view = new GdoBundleGraphView(snapshotServiceFactory: () => service,
                snapshotVisibility: _ => true);
            view.Load(first);
            RefreshVisibleSnapshots(view);

            view.Load(second);

            Assert.That(service.Disposed, Is.True);
            Assert.That(service.SubscriptionsDisposed, Is.EqualTo(1));
        }

        private static DropdownMenu Actions(GdoGraphOwnerGroup group)
        {
            var menu = new DropdownMenu();
            VisualElement target = group;
            using (var trigger = MouseDownEvent.GetPooled())
            using (var evt = ContextualMenuPopulateEvent.GetPooled(trigger, menu, target, null))
                group.BuildContextualMenu(evt);
            return menu;
        }

        private static void Click(Button button)
        {
            typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null });
        }

        private static void RefreshVisibleSnapshots(GdoBundleGraphView view)
        {
            var method = typeof(GdoBundleGraphView).GetMethod("RefreshVisibleExternalSnapshots",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(view, null);
        }

        private sealed class RecordingSnapshotService : IExternalGdoSnapshotService
        {
            public readonly System.Collections.Generic.List<int> RequestedIds =
                new System.Collections.Generic.List<int>();
            public bool Disposed;
            public int SubscriptionsDisposed;

            public IDisposable Request(string providerKey, int gdoId, string runtimeType,
                Action<ExternalGdoSnapshotResult> completed)
            {
                RequestedIds.Add(gdoId);
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Loading));
                return new CallbackDisposable(() => SubscriptionsDisposed++);
            }

            public void Dispose() => Disposed = true;
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private Action _callback;
            public CallbackDisposable(Action callback) { _callback = callback; }
            public void Dispose() { _callback?.Invoke(); _callback = null; }
        }

        private static void RemoveSelectedOwnerGroups(GdoBundleGraphView view, GdoGraphOwnerGroup group)
        {
            view.ClearSelection();
            view.AddToSelection(group);
            var method = typeof(GdoBundleGraphView).GetMethod("RemoveSelectedOwnerGroups",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "The Delete-key command must bypass Unity's native Group deletion.");
            Assert.That((bool)method.Invoke(view, null), Is.True);
        }
    }
}
