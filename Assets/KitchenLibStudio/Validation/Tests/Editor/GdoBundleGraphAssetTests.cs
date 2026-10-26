using System;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoBundleGraphAssetTests
    {
        private const string Root = "Assets/GraphModelTests";
        private const string GraphPath = Root + "/Test Bundle Graph.asset";
        private const string ItemPath = Root + "/Test Item.asset";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "GraphModelTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
            Undo.ClearAll();
        }

        [Test]
        public void CreatePersistsBundleIdentityAndRequiresAssetsExtension()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(GraphPath, "Test Author", "Breakfast", "breakfast.assets");

            Assert.That(graph.BundleName, Is.EqualTo("Breakfast"));
            Assert.That(graph.BundleFileName, Is.EqualTo("breakfast.assets"));
            Assert.That(() => GdoBundleGraphRepository.Create(Root + "/Invalid.asset", "Test Author", "Invalid", "invalid.bundle"),
                Throws.ArgumentException);
        }

        [Test]
        public void LocalMemberIdentityAndPositionSurviveSaveReload()
        {
            Item item = CreateItem(1234);
            GdoBundleGraphAsset graph = CreateGraph();
            GdoGraphNodeRecord added = GdoBundleGraphRepository.AddMember(graph, item, new Rect(10, 20, 300, 180));
            string id = added.Id;
            string guid = AssetDatabase.AssetPathToGUID(ItemPath);

            GdoBundleGraphRepository.SetPosition(graph, id, new Rect(30, 40, 360, 220));
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GdoGraphNodeRecord reloaded = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(GraphPath).Nodes.Single();
            Assert.That(reloaded.Id, Is.EqualTo(id));
            Assert.That(reloaded.AssetGuid, Is.EqualTo(guid));
            Assert.That(reloaded.Asset, Is.SameAs(item));
            Assert.That(reloaded.CachedName, Is.EqualTo("Test Item"));
            Assert.That(reloaded.RuntimeTypeName, Is.EqualTo("KitchenData.Item"));
            Assert.That(reloaded.Position, Is.EqualTo(new Rect(30, 40, 360, 220)));
        }

        [Test]
        public void DuplicateLocalMembershipIsRejectedAndExternalReferencesMayRepeat()
        {
            Item item = CreateItem(1234);
            GdoBundleGraphAsset graph = CreateGraph();
            GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 9001, "KitchenData.Item", new Rect());

            Assert.That(() => GdoBundleGraphRepository.AddMember(graph, item, new Rect()), Throws.ArgumentException);
            Assert.That(() => GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 9001, "KitchenData.Item", new Rect()),
                Throws.Nothing);
            Assert.That(() => GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 9001, "KitchenData.Appliance", new Rect()),
                Throws.Nothing);
        }

        [Test]
        public void RemovingMemberDoesNotDeleteUnderlyingAsset()
        {
            Item item = CreateItem(1234);
            GdoBundleGraphAsset graph = CreateGraph();
            GdoGraphNodeRecord member = GdoBundleGraphRepository.AddMember(graph, item, new Rect());

            GdoBundleGraphRepository.RemoveMember(graph, member.Id);

            Assert.That(graph.Nodes, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<Item>(ItemPath), Is.SameAs(item));
        }

        [Test]
        public void DeletedMemberRetainsGuidAndCachedMetadataForDiagnostics()
        {
            Item item = CreateItem(1234);
            GdoBundleGraphAsset graph = CreateGraph();
            GdoGraphNodeRecord member = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            string guid = member.AssetGuid;
            string id = member.Id;
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.DeleteAsset(ItemPath), Is.True);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GdoGraphNodeRecord broken = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(GraphPath).Nodes.Single();
            Assert.That(broken.Id, Is.EqualTo(id));
            Assert.That(broken.Asset == null, Is.True);
            Assert.That(broken.AssetGuid, Is.EqualTo(guid));
            Assert.That(broken.CachedName, Is.EqualTo("Test Item"));
            Assert.That(broken.RuntimeTypeName, Is.EqualTo("KitchenData.Item"));
            Assert.That(broken.IsBroken, Is.True);
        }

        [Test]
        public void RelationshipAndEdgeOrderingSurvivesReloadIndependentlyOfInsertionOrder()
        {
            GdoBundleGraphAsset graph = CreateGraph();
            GdoGraphNodeRecord source = GdoBundleGraphRepository.AddMember(graph, CreateItem(1234), new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 9001, "KitchenData.Item", new Rect());
            GdoGraphRelationshipRecord second = GdoBundleGraphRepository.AddRelationship(
                graph, source.Id, "KitchenData.Dish.ResultingMenuItems[]", 1, new Rect(400, 200, 240, 160));
            GdoGraphRelationshipRecord first = GdoBundleGraphRepository.AddRelationship(
                graph, source.Id, "KitchenData.Dish.ResultingMenuItems[]", 0, new Rect(400, 20, 240, 160));
            GdoBundleGraphRepository.AddEdge(graph, source.Id, "KitchenData.Dish.ResultingMenuItems", first.Id, "Owner", 0);
            GdoBundleGraphRepository.AddEdge(graph, external.Id, "Value", second.Id, "Item", 1);
            string[] relationshipIds = { first.Id, second.Id };

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            GdoBundleGraphAsset reloaded = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(GraphPath);

            Assert.That(reloaded.Relationships.Select(value => value.Id), Is.EqualTo(relationshipIds));
            Assert.That(reloaded.Relationships.Select(value => value.Order), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(reloaded.Edges.Select(value => value.Order), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(reloaded.Edges.Select(value => value.Id).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void FindGraphsContainingUsesPersistentAssetGuid()
        {
            Item item = CreateItem(1234);
            GdoBundleGraphAsset first = CreateGraph();
            GdoBundleGraphAsset second = GdoBundleGraphRepository.Create(Root + "/Second.asset", "Test Author", "Second", "second.assets");
            GdoBundleGraphRepository.AddMember(first, item, new Rect());
            GdoBundleGraphRepository.AddMember(second, item, new Rect());

            Assert.That(GdoBundleGraphRepository.FindGraphsContaining(item).Select(value => value.BundleName),
                Is.EqualTo(new[] { "Second", "Breakfast" }));
        }

        [Test]
        public void AddMemberAndPositionChangesParticipateInUndo()
        {
            Item item = CreateItem(1234);
            GdoBundleGraphAsset graph = CreateGraph();
            GdoGraphNodeRecord member = GdoBundleGraphRepository.AddMember(graph, item, new Rect(1, 2, 3, 4));

            Undo.PerformUndo();
            Assert.That(graph.Nodes, Is.Empty);

            Undo.PerformRedo();
            Assert.That(graph.Nodes.Single().Id, Is.EqualTo(member.Id));
            GdoBundleGraphRepository.SetPosition(graph, member.Id, new Rect(5, 6, 7, 8));
            Undo.PerformUndo();
            Assert.That(graph.Nodes.Single().Position, Is.EqualTo(new Rect(1, 2, 3, 4)));
        }

        [Test]
        public void ExternalIdentityRequiresAnExplicitProvider()
        {
            GdoBundleGraphAsset graph = CreateGraph();
            GdoGraphNodeRecord vanilla = GdoBundleGraphRepository.AddExternal(graph,
                ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", new Rect());
            GdoGraphNodeRecord imported = GdoBundleGraphRepository.AddExternal(graph,
                "workshop:2913877103", 42, "KitchenData.Item", new Rect(), "Pork");

            Assert.That(vanilla.ExternalProviderKey, Is.EqualTo("vanilla:plateup"));
            Assert.That(imported.ExternalProviderKey, Is.EqualTo("workshop:2913877103"));
            Assert.That(imported.CachedName, Is.EqualTo("Pork"));
            Assert.That(graph.Nodes.Count, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => GdoBundleGraphRepository.AddExternal(graph,
                string.Empty, 43, "KitchenData.Item", new Rect()));
        }

        private static GdoBundleGraphAsset CreateGraph() =>
            GdoBundleGraphRepository.Create(GraphPath, "Test Author", "Breakfast", "breakfast.assets");

        private static Item CreateItem(int id)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            item.name = "Test Item";
            item.ID = id;
            AssetDatabase.CreateAsset(item, ItemPath);
            return item;
        }
    }
}
