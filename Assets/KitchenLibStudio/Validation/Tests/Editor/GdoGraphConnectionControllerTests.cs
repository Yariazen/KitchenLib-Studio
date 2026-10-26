using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphConnectionControllerTests
    {
        private const string Root = "Assets/GraphConnectionTests";
        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root); AssetDatabase.CreateFolder("Assets", "GraphConnectionTests"); GdoBundleProjectRepository.Create(Root, "Test Author", "Graph"); Undo.ClearAll(); }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(Root); AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root); Undo.ClearAll(); }

        [Test]
        public void StructuredChoicesHaveReadableLabelsExactPathsAndNoDirectReferenceFields()
        {
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, Create<ItemGroup>("Group"), new Rect());
            var choices = new GdoGraphConnectionController(graph).RelationshipChoices(owner.Id);

            Assert.That(choices.Select(choice => choice.FieldPath), Is.EquivalentTo(new[] {
                "KitchenData.ItemGroup.Sets[]", "KitchenData.ItemGroup.Rewards[]", "KitchenData.Item.Processes[]", "KitchenData.Item.AutomaticItemProcess" }));
            Assert.That(choices.Single(choice => choice.FieldPath == "KitchenData.ItemGroup.Sets[]").Label, Is.EqualTo("Sets"));
            Assert.That(choices.Single(choice => choice.FieldPath == "KitchenData.ItemGroup.Rewards[]").Label, Is.EqualTo("Rewards"));
            Assert.That(choices.Single(choice => choice.FieldPath == "KitchenData.Item.AutomaticItemProcess").Label, Is.EqualTo("Automatic Item Process"));
        }

        [Test]
        public void RepeatedRootsUseSuccessiveOrdersAndProjectCreationAndRemoval()
        {
            GdoBundleGraphAsset graph = Graph(); ItemGroup asset = Create<ItemGroup>("Group");
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, asset, new Rect());
            var controller = new GdoGraphConnectionController(graph);
            const string path = "KitchenData.ItemGroup.Sets[]";
            GdoGraphRelationshipRecord first = controller.CreateNextRelationship(owner.Id, path, new Rect());
            GdoGraphRelationshipRecord second = controller.CreateNextRelationship(owner.Id, path, new Rect());

            Assert.That(new[] { first.Order, second.Order }, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(GdoOverlayAuthoringRepository.Find(asset).TryGetField("KitchenData.ItemGroup", "Sets", out GdoOverlayField field), Is.True);
            Assert.That(field.Value.Values.Count, Is.EqualTo(2));
            controller.RemoveRelationship(first.Id, false);
            GdoGraphRelationshipRecord third = controller.CreateNextRelationship(owner.Id, path, new Rect());
            Assert.That(third.Order, Is.EqualTo(1));
            Assert.That(graph.Relationships.Select(value => value.Id), Is.EquivalentTo(new[] { second.Id, third.Id }));
        }

        [Test]
        public void SingletonRootRejectsSecondCreationIncludingExplicitOrderApi()
        {
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, Create<Item>("Owner"), new Rect());
            var controller = new GdoGraphConnectionController(graph);
            const string path = "KitchenData.Item.AutomaticItemProcess";
            Assert.That(controller.RelationshipChoices(owner.Id).Single(choice => choice.FieldPath == path).IsRepeated, Is.False);
            GdoGraphRelationshipRecord first = controller.CreateNextRelationship(owner.Id, path, new Rect());

            Assert.That(first.Order, Is.Zero);
            Assert.Throws<System.InvalidOperationException>(() => controller.CreateNextRelationship(owner.Id, path, new Rect()));
            Assert.Throws<System.InvalidOperationException>(() => controller.CreateRelationship(owner.Id, path, 1, new Rect()));
            Assert.That(graph.Relationships.Single().Id, Is.EqualTo(first.Id));
        }

        [Test]
        public void RemovalRequiresExplicitEdgeConsentAndUndoRestoresGraphAndProjectionTogether()
        {
            GdoBundleGraphAsset graph = Graph(); Dish asset = Create<Dish>("Dish");
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, asset, new Rect());
            GdoGraphNodeRecord target = GdoBundleGraphRepository.AddMember(graph, Create<Item>("Target"), new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord relationship = controller.CreateNextRelationship(owner.Id, "KitchenData.Dish.ResultingMenuItems[]", new Rect());
            GdoGraphEdgeRecord edge = controller.Connect(target.Id, relationship.Id, "KitchenData.Dish.ResultingMenuItems[].Item", 0);
            Assert.Throws<System.InvalidOperationException>(() => controller.RemoveRelationship(relationship.Id, false));
            Assert.That(graph.Edges.Single().Id, Is.EqualTo(edge.Id));
            Undo.IncrementCurrentGroup();
            controller.RemoveRelationship(relationship.Id, true);

            Assert.That(graph.Relationships, Is.Empty);
            Assert.That(graph.Edges, Is.Empty);
            Assert.That(GdoOverlayAuthoringRepository.Find(asset).TryGetField("KitchenData.Dish", "ResultingMenuItems", out GdoOverlayField removed), Is.True);
            Assert.That(removed.Value.Values, Is.Empty);
            Undo.PerformUndo();
            Assert.That(graph.Relationships.Single().Id, Is.EqualTo(relationship.Id));
            Assert.That(graph.Edges.Single().Id, Is.EqualTo(edge.Id));
            Assert.That(GdoOverlayAuthoringRepository.Find(asset).TryGetField("KitchenData.Dish", "ResultingMenuItems", out GdoOverlayField restored), Is.True);
            Assert.That(restored.Value.Values.Single().Members.Single(member => member.Name == "Item").Value.ReferenceValue.Asset, Is.SameAs(target.Asset));
        }

        [Test]
        public void RelationshipCreationProjectionAndUndoAreOneIsolatedTransaction()
        {
            GdoBundleGraphAsset graph = Graph(); ItemGroup asset = Create<ItemGroup>("Group");
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, asset, new Rect());
            Undo.IncrementCurrentGroup();
            new GdoGraphConnectionController(graph).CreateNextRelationship(owner.Id, "KitchenData.ItemGroup.Sets[]", new Rect());
            Undo.PerformUndo();

            Assert.That(graph.Nodes.Single().Id, Is.EqualTo(owner.Id));
            Assert.That(graph.Relationships, Is.Empty);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(asset);
            Assert.That(companion == null || !companion.TryGetField("KitchenData.ItemGroup", "Sets", out _), Is.True);
        }

        [Test]
        public void FailedRemovalProjectionRestoresRelationshipAndConnectedEdges()
        {
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, Create<Dish>("Dish"), new Rect());
            GdoGraphNodeRecord target = GdoBundleGraphRepository.AddMember(graph, Create<Item>("Target"), new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord relationship = controller.CreateNextRelationship(owner.Id, "KitchenData.Dish.ResultingMenuItems[]", new Rect());
            GdoGraphEdgeRecord edge = controller.Connect(target.Id, relationship.Id, "KitchenData.Dish.ResultingMenuItems[].Item", 0);
            Item duplicate = Create<Item>("Duplicate"); duplicate.ID = target.Asset.ID;
            GdoBundleGraphRepository.AddMember(graph, duplicate, new Rect());

            Assert.Throws<System.InvalidOperationException>(() => controller.RemoveRelationship(relationship.Id, true));
            Assert.That(graph.Relationships.Single().Id, Is.EqualTo(relationship.Id));
            Assert.That(graph.Edges.Single().Id, Is.EqualTo(edge.Id));
            Assert.That(graph.Nodes.Count, Is.EqualTo(3));
        }

        [Test]
        public void ConnectDisconnectAndReorderOwnDirectRelationshipProjection()
        {
            Appliance owner = Create<Appliance>("Owner"); Item first = Create<Item>("First"); Item second = Create<Item>("Second");
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord firstNode = GdoBundleGraphRepository.AddMember(graph, first, new Rect());
            GdoGraphNodeRecord secondNode = GdoBundleGraphRepository.AddMember(graph, second, new Rect());
            var controller = new GdoGraphConnectionController(graph);
            const string path = "KitchenData.Appliance.RequiresIngredientForShop[]";
            GdoGraphEdgeRecord firstEdge = controller.Connect(firstNode.Id, ownerNode.Id, path, 1);
            GdoGraphEdgeRecord secondEdge = controller.Connect(secondNode.Id, ownerNode.Id, path, 0);

            controller.Reorder(firstEdge.Id, 0);
            controller.Disconnect(secondEdge.Id);

            Assert.That(graph.Edges.Single().Id, Is.EqualTo(firstEdge.Id));
            Assert.That(graph.Edges.Single().Order, Is.EqualTo(0));
            Assert.That(GdoOverlayAuthoringRepository.Find(owner).TryGetField("KitchenData.Appliance", "RequiresIngredientForShop", out GdoOverlayField field), Is.True);
            Assert.That(field.Value.Values.Single().ReferenceValue.Asset, Is.SameAs(first));
        }

        [Test]
        public void StructuredRelationshipNodeRendersAndAcceptsTypedNestedConnection()
        {
            Dish dish = Create<Dish>("Dish"); Item item = Create<Item>("Item");
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord dishNode = GdoBundleGraphRepository.AddMember(graph, dish, new Rect());
            GdoGraphNodeRecord itemNode = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord relationship = controller.CreateRelationship(dishNode.Id,
                "KitchenData.Dish.ResultingMenuItems[]", 0, new Rect(30, 40, 220, 160));
            GdoGraphEdgeRecord edge = controller.Connect(itemNode.Id, relationship.Id,
                "KitchenData.Dish.ResultingMenuItems[].Item", 0);

            var view = new GdoGraphRelationshipNodeView(relationship, graph);
            Assert.That(view.RecordId, Is.EqualTo(relationship.Id));
            Assert.That(view.title, Is.EqualTo("Resulting Menu Items #0"));
            Assert.That(view.ClassListContains("kls-gdo-node--relationship"), Is.True);
            Assert.That((view.capabilities & Capabilities.Deletable) == 0, Is.True);
            Assert.That(view.inputContainer.childCount, Is.GreaterThan(0));
            Port itemPort = view.inputContainer.Query<Port>().ToList()
                .Single(port => (string)port.userData == "KitchenData.Dish.ResultingMenuItems[].Item");
            Assert.That(itemPort.portName, Is.EqualTo("Item · Item"));
            Assert.That(itemPort.tooltip, Does.Contain("KitchenData.Dish.ResultingMenuItems[].Item"));
            Assert.That(edge.InputNodeId, Is.EqualTo(relationship.Id));
            Assert.That(view.EditorBody, Is.Not.Null);
            Assert.That(view.EditorBody.EditableMemberCount, Is.GreaterThan(0));
            Assert.That(view.EditorBody.Query<Label>(className: "kls-relationship-summary").ToList(), Is.Empty);
            view.EditorBody.CommitMember("Weight", GdoOverlayValue.Scalar("3.5"));
            GdoOverlayMember authoredWeight = graph.Relationships.Single(value => value.Id == relationship.Id)
                .Value.Members.Single(member => member.Name == "Weight");
            Assert.That(authoredWeight.Value.ScalarValue, Is.EqualTo("3.5"));
            Assert.That(GdoOverlayAuthoringRepository.Find(dish).TryGetField("KitchenData.Dish", "ResultingMenuItems",
                out GdoOverlayField projected), Is.True);
            Assert.That(projected.Value.Values.Single().Members.Single(member => member.Name == "Weight").Value.ScalarValue,
                Is.EqualTo("3.5"));
        }

        [Test]
        public void ConnectionAndProjectionUndoAsOneTransaction()
        {
            Item owner = Create<Item>("UndoOwner"); Item target = Create<Item>("UndoTarget");
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            Undo.IncrementCurrentGroup();
            new GdoGraphConnectionController(graph).Connect(targetNode.Id, ownerNode.Id, "KitchenData.Item.DirtiesTo", 0);

            Undo.PerformUndo();

            Assert.That(graph.Edges, Is.Empty);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(owner);
            Assert.That(companion == null || !companion.TryGetField("KitchenData.Item", "DirtiesTo", out _), Is.True);
        }

        [Test]
        public void RejectsIncompatibleConnectionWithoutMutatingGraph()
        {
            Item owner = Create<Item>("Owner"); Appliance appliance = Create<Appliance>("Appliance");
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord applianceNode = GdoBundleGraphRepository.AddMember(graph, appliance, new Rect());
            var controller = new GdoGraphConnectionController(graph);

            Assert.Throws<System.ArgumentException>(() => controller.Connect(applianceNode.Id, ownerNode.Id,
                "KitchenData.Item.DirtiesTo", 0));
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void ProjectionFailureRollsBackConnectionMutation()
        {
            Item owner = Create<Item>("BadOwner"); Item target = Create<Item>("BadTarget");
            Item duplicate = Create<Item>("Duplicate");
            duplicate.ID = target.ID;
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            GdoBundleGraphRepository.AddMember(graph, duplicate, new Rect());

            Assert.Throws<System.InvalidOperationException>(() => new GdoGraphConnectionController(graph)
                .Connect(targetNode.Id, ownerNode.Id, "KitchenData.Item.DirtiesTo", 0));
            Assert.That(graph.Edges, Is.Empty);
        }

        [Test]
        public void NestedCollectionElementsUseDistinctIndexedReferencePorts()
        {
            ItemGroup owner = Create<ItemGroup>("Group"); Item first = Create<Item>("NestedFirst");
            Item second = Create<Item>("NestedSecond");
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord firstNode = GdoBundleGraphRepository.AddMember(graph, first, new Rect());
            GdoGraphNodeRecord secondNode = GdoBundleGraphRepository.AddMember(graph, second, new Rect());
            var controller = new GdoGraphConnectionController(graph);
            const string relationshipPath = "KitchenData.ItemGroup.Sets[]";
            GdoGraphRelationshipRecord relationship = controller.CreateRelationship(ownerNode.Id, relationshipPath, 0, new Rect());
            GdoOverlayValue withSlots = GdoOverlayValueEditor.ReplaceStructMember(relationship.Value, "Items",
                GdoOverlayValue.List(new[] { GdoOverlayValue.Null(), GdoOverlayValue.Null() }));
            controller.SetRelationshipValue(relationship.Id, withSlots);
            var view = new GdoGraphRelationshipNodeView(relationship, graph);

            string firstPath = relationshipPath + ".Items[0]";
            string secondPath = relationshipPath + ".Items[1]";
            Assert.That(view.ReferencePortPaths, Does.Contain(firstPath));
            Assert.That(view.ReferencePortPaths, Does.Contain(secondPath));
            controller.Connect(firstNode.Id, relationship.Id, firstPath, 0);
            controller.Connect(secondNode.Id, relationship.Id, secondPath, 1);
            var graphView = new GdoBundleGraphView();
            graphView.Load(graph);

            Assert.That(GdoOverlayAuthoringRepository.Find(owner).TryGetField("KitchenData.ItemGroup", "Sets",
                out GdoOverlayField field), Is.True);
            GdoOverlayValue items = field.Value.Values.Single().Members.Single(member => member.Name == "Items").Value;
            Assert.That(items.Values[0].ReferenceValue.Asset, Is.SameAs(first));
            Assert.That(items.Values[1].ReferenceValue.Asset, Is.SameAs(second));

            GdoOverlayValue withoutFirst = GdoOverlayValueEditor.ReplaceStructMember(relationship.Value, "Items",
                GdoOverlayValueEditor.RemoveCollectionElement(
                    relationship.Value.Members.Single(member => member.Name == "Items").Value, 0));
            var relationshipView = (GdoGraphRelationshipNodeView)graphView.FindElement(relationship.Id);
            relationshipView.EditorBody.CommitCollectionRemoval(
                relationshipPath + ".Items", 0, withoutFirst);

            Assert.That(graph.Edges.Count, Is.EqualTo(1));
            Assert.That(graph.Edges.Single().OutputNodeId, Is.EqualTo(secondNode.Id));
            Assert.That(graph.Edges.Single().InputPortPath, Is.EqualTo(firstPath));
            Edge visualEdge = graphView.edges.ToList().Single();
            Assert.That(visualEdge.userData, Is.EqualTo(graph.Edges.Single().Id));
            Assert.That(visualEdge.input.userData, Is.EqualTo(firstPath));
            Assert.That(GdoOverlayAuthoringRepository.Find(owner).TryGetField("KitchenData.ItemGroup", "Sets",
                out GdoOverlayField shiftedField), Is.True);
            GdoOverlayValue shiftedItems = shiftedField.Value.Values.Single().Members
                .Single(member => member.Name == "Items").Value;
            Assert.That(shiftedItems.Values.Count, Is.EqualTo(1));
            Assert.That(shiftedItems.Values[0].ReferenceValue.Asset, Is.SameAs(second));
        }

        private static GdoBundleGraphAsset Graph() => AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(Root + "/Graph/Graph.asset");
        private static T Create<T>(string name) where T : GameDataObject
        { T value = ScriptableObject.CreateInstance<T>(); value.name = name; value.ID = GdoProjectIdentity.RuntimeId("Graph", name); AssetDatabase.CreateAsset(value, Root + "/Graph/GDOs/" + name + ".asset"); return value; }
    }
}
