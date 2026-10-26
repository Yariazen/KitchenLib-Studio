using System;
using System.Linq;
using System.Reflection;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphAssetCommandsTests
    {
        private const string Root = "Assets/GraphCommandTests";
        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root); AssetDatabase.CreateFolder("Assets", "GraphCommandTests"); Undo.ClearAll(); }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(Root); AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root); Undo.ClearAll(); }

        [Test]
        public void AuthoringApiExposesOnlyFinalIdentityAndOwnedMutationPaths()
        {
            MethodInfo[] projectCreates = typeof(GdoBundleProjectRepository).GetMethods(
                BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == "Create").ToArray();

            Assert.That(projectCreates, Has.Length.EqualTo(1));
            Assert.That(projectCreates[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(string), typeof(string), typeof(string) }));
            Assert.That(typeof(GdoGraphAssetCommands).GetMethod("AddExternalReference"), Is.Null);
            Assert.That(typeof(GdoGraphAssetCommands).GetMethod("RemoveMember"), Is.Null);
        }

        [Test]
        public void CreateGdoCreatesMirrorCompanionAndMembershipAsOneCommand()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            var position = new Rect(1, 2, 240, 140);
            GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.ItemGroup", "Dough Ball", position);

            ItemGroup item = AssetDatabase.LoadAssetAtPath<ItemGroup>(Root + "/My Bakery/GDOs/Dough Ball.asset");
            Assert.That(item, Is.Not.Null);
            Assert.That(item.name, Is.EqualTo("Dough Ball"));
            Assert.That(item.ID, Is.EqualTo(GdoProjectIdentity.RuntimeId(
                graph.AuthorNamespace, graph.BundleName, "Dough Ball")));
            Assert.That(node.Asset, Is.SameAs(item));
            Assert.That(node.Position, Is.EqualTo(position));
            Assert.That(GdoOverlayAuthoringRepository.Find(item), Is.Not.Null);
            Assert.That(GdoGraphAssetCommands.SupportedRuntimeTypes, Does.Contain("KitchenData.Item"));
        }

        [Test]
        public void AuthorScopedProjectGeneratesTripleIdentityOnce()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "My Studio", "My Bakery");
            GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.Item", "Flour", new Rect());

            Assert.That(graph.AuthorNamespace, Is.EqualTo("My Studio"));
            Assert.That(graph.IdentitySchemeVersion, Is.EqualTo(1));
            Assert.That(node.Asset.ID, Is.EqualTo(GdoProjectIdentity.RuntimeId("My Studio", "My Bakery", "Flour")));
        }

        [Test]
        public void AddExistingExternalAndRemoveMembershipDoNotDeleteAssets()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Existing"; item.ID = 502;
            string path = Root + "/My Bakery/GDOs/Existing.asset";
            AssetDatabase.CreateAsset(item, path);
            GdoGraphNodeRecord local = GdoGraphAssetCommands.AddExistingGdo(graph, item, new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 503, "KitchenData.Item", new Rect());

            new GdoGraphConnectionController(graph).RemoveMember(local.Id, false);

            Assert.That(AssetDatabase.LoadAssetAtPath<Item>(path), Is.Not.Null);
            Assert.That(graph.Nodes, Has.None.Matches<GdoGraphNodeRecord>(node => node.Id == local.Id));
            Assert.That(graph.Nodes, Has.Some.Matches<GdoGraphNodeRecord>(node => node.Id == external.Id));
        }

        [Test]
        public void CreateGdoRejectsCaseInsensitiveDuplicateNamesWithoutReplacingAsset()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            GdoGraphNodeRecord original = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Dough Ball", new Rect());

            Assert.Throws<ArgumentException>(() => GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.ItemGroup", "dough ball", new Rect()));

            Assert.That(graph.Nodes.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<Item>(Root + "/My Bakery/GDOs/Dough Ball.asset"), Is.SameAs(original.Asset));
        }

        [Test]
        public void CreateGdoRejectsOccupiedPathEvenWhenAssetIsNotAMember()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            Item existing = ScriptableObject.CreateInstance<Item>();
            string path = Root + "/My Bakery/GDOs/Dough Ball.asset";
            AssetDatabase.CreateAsset(existing, path);

            Assert.Throws<ArgumentException>(() => GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "dough ball", new Rect()));

            Assert.That(AssetDatabase.LoadAssetAtPath<Item>(path), Is.SameAs(existing));
            Assert.That(graph.Nodes, Is.Empty);
        }

        [Test]
        public void AddExistingGdoRejectsAssetOutsideOwnedFolder()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            Item item = ScriptableObject.CreateInstance<Item>();
            AssetDatabase.CreateAsset(item, Root + "/Outside.asset");

            Assert.Throws<ArgumentException>(() => GdoGraphAssetCommands.AddExistingGdo(graph, item, new Rect()));

            Assert.That(graph.Nodes, Is.Empty);
        }

        [Test]
        public void AddExistingGdoRejectsCaseInsensitiveDuplicateObjectName()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Bakery");
            GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Dough Ball", new Rect());
            Item duplicate = ScriptableObject.CreateInstance<Item>();
            AssetDatabase.CreateAsset(duplicate, Root + "/My Bakery/GDOs/Other.asset");
            duplicate.name = "dough ball";

            Assert.Throws<ArgumentException>(() => GdoGraphAssetCommands.AddExistingGdo(graph, duplicate, new Rect()));
            Assert.That(graph.Nodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void CreateGdoRejectsInvalidLayoutWithoutCreatingFolders()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Legacy.asset", "Test Author", "Legacy", "legacy.assets");

            Assert.Throws<ArgumentException>(() => GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Dough Ball", new Rect()));
            Assert.That(AssetDatabase.GetSubFolders(Root), Is.Empty);
        }
    }
}
