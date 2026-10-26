using System.Collections.Generic;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphRelationshipProjectorTests
    {
        private const string Root = "Assets/GraphProjectionTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "GraphProjectionTests");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void DirectLocalReferenceProjectsToCanonicalOverlayField()
        {
            Item owner = Create<Item>("Owner", 1001);
            Item target = Create<Item>("Target", 1002);
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            GdoBundleGraphRepository.AddEdge(graph, targetNode.Id, "Value", ownerNode.Id,
                "KitchenData.Item.DirtiesTo", 0);

            GdoGraphProjectionResult result = GdoGraphRelationshipProjector.Project(graph);
            GdoGraphProjectedField field = result.Fields.Single(value => value.OwnerNodeId == ownerNode.Id &&
                value.FieldKey == "KitchenData.Item.DirtiesTo");

            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(field.Value.Kind, Is.EqualTo(GdoOverlayValueKind.GdoReference));
            Assert.That(field.Value.ReferenceValue.Asset, Is.SameAs(target));
            Assert.That(field.Value.ReferenceValue.ExpectedType, Is.EqualTo("KitchenData.Item"));
        }

        [Test]
        public void SetReferencesUseExplicitEdgeOrderAndRejectDuplicates()
        {
            Dish owner = Create<Dish>("Dish", 2001);
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord first = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 3001, "KitchenData.Item", new Rect());
            GdoGraphNodeRecord second = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 3002, "KitchenData.Item", new Rect());
            const string port = "KitchenData.Dish.MinimumIngredients[]";
            GdoBundleGraphRepository.AddEdge(graph, second.Id, "Value", ownerNode.Id, port, 1);
            GdoBundleGraphRepository.AddEdge(graph, first.Id, "Value", ownerNode.Id, port, 0);

            GdoGraphProjectionResult ordered = GdoGraphRelationshipProjector.Project(graph);
            GdoOverlayValue value = ordered.Fields.Single(field => field.FieldKey == "KitchenData.Dish.MinimumIngredients").Value;
            Assert.That(value.Kind, Is.EqualTo(GdoOverlayValueKind.Set));
            Assert.That(value.Values.Select(item => item.ReferenceValue.Id), Is.EqualTo(new[] { 3001, 3002 }));

            GdoBundleGraphRepository.AddEdge(graph, first.Id, "Value", ownerNode.Id, port, 2);
            GdoGraphProjectionResult duplicate = GdoGraphRelationshipProjector.Project(graph);
            Assert.That(duplicate.Diagnostics.Select(issue => issue.Category), Does.Contain("DuplicateSetValue"));
            Assert.That(duplicate.Fields.Any(field => field.FieldKey == "KitchenData.Dish.MinimumIngredients"), Is.False);
        }

        [Test]
        public void StructuredRelationshipPreservesIntrinsicMembersAndProjectsReferenceMembers()
        {
            Dish owner = Create<Dish>("Dish", 4001);
            Item target = Create<Item>("Item", 4002);
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            GdoOverlayValue authored = GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("Item", GdoOverlayValue.Null()),
                new GdoOverlayMember("Phase", GdoOverlayValue.Enum(2)),
                new GdoOverlayMember("Weight", GdoOverlayValue.Scalar("3.5")),
                new GdoOverlayMember("DynamicMenuType", GdoOverlayValue.Enum(0)),
                new GdoOverlayMember("DynamicMenuIngredient", GdoOverlayValue.Null())
            });
            GdoGraphRelationshipRecord relationship = GdoBundleGraphRepository.AddRelationship(graph, ownerNode.Id,
                "KitchenData.Dish.ResultingMenuItems[]", 0, new Rect(), authored);
            GdoBundleGraphRepository.AddEdge(graph, targetNode.Id, "Value", relationship.Id,
                "KitchenData.Dish.ResultingMenuItems[].Item", 0);

            GdoGraphProjectionResult result = GdoGraphRelationshipProjector.Project(graph);
            GdoOverlayValue element = result.Fields.Single(field =>
                field.FieldKey == "KitchenData.Dish.ResultingMenuItems").Value.Values.Single();

            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(element.Members.Single(member => member.Name == "Item").Value.ReferenceValue.Asset, Is.SameAs(target));
            Assert.That(element.Members.Single(member => member.Name == "Weight").Value.ScalarValue, Is.EqualTo("3.5"));
            Assert.That(element.Members.Single(member => member.Name == "Phase").Value.ScalarValue, Is.EqualTo("2"));
            Assert.That(element.Members.Single(member => member.Name == "DynamicMenuIngredient").Value.Kind,
                Is.EqualTo(GdoOverlayValueKind.Null));
        }

        [Test]
        public void ProjectionIsIndependentOfEdgeInsertionOrder()
        {
            int[] first = ProjectList(new[] { 5002, 5001 });
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "GraphProjectionTests");
            int[] second = ProjectList(new[] { 5001, 5002 });

            Assert.That(first, Is.EqualTo(new[] { 5001, 5002 }));
            Assert.That(second, Is.EqualTo(first));
        }

        private static int[] ProjectList(int[] insertionOrder)
        {
            Appliance owner = Create<Appliance>("Appliance", 5000);
            GdoBundleGraphAsset graph = Graph();
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            var nodes = new Dictionary<int, GdoGraphNodeRecord>
            {
                [5001] = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 5001, "KitchenData.Item", new Rect()),
                [5002] = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 5002, "KitchenData.Item", new Rect())
            };
            foreach (int id in insertionOrder)
                GdoBundleGraphRepository.AddEdge(graph, nodes[id].Id, "Value", ownerNode.Id,
                    "KitchenData.Appliance.RequiresIngredientForShop[]", id == 5001 ? 0 : 1);
            return GdoGraphRelationshipProjector.Project(graph).Fields.Single(field =>
                field.FieldKey == "KitchenData.Appliance.RequiresIngredientForShop").Value.Values
                .Select(value => value.ReferenceValue.Id).ToArray();
        }

        private static GdoBundleGraphAsset Graph() => GdoBundleGraphRepository.Create(
            Root + "/Graph.asset", "Test Author", "Projection", "projection.assets");

        private static T Create<T>(string name, int id) where T : GameDataObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            value.name = name;
            value.ID = id;
            AssetDatabase.CreateAsset(value, Root + "/" + name + ".asset");
            return value;
        }
    }
}
