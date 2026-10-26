using System;
using System.IO;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoBundleGraphBuilderTests
    {
        private const string Root = "Assets/GraphBuilderTests";
        private string _output;
        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root); AssetDatabase.CreateFolder("Assets", "GraphBuilderTests"); _output = Path.Combine(Path.GetTempPath(), "KLStudio Graph Builder", Guid.NewGuid().ToString("N"), "content"); }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(Root); AssetDatabase.DeleteAsset(GdoOverlayAuthoringRepository.Root); string package = Directory.GetParent(_output)?.FullName; if (package != null && Directory.Exists(package)) Directory.Delete(package, true); }

        [Test]
        public void BuildUsesOnlyLocalMembersAndGraphNamingAndExcludesMetadata()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "My Graph");
            GameDifficultySettings local = ScriptableObject.CreateInstance<GameDifficultySettings>(); local.name = "Settings"; local.ID = -670546766;
            AssetDatabase.CreateAsset(local, GdoBundleProjectRepository.GdoFolder(graph) + "/Settings.asset");
            GdoBundleGraphRepository.AddMember(graph, local, new Rect());
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 702, "KitchenData.Item", new Rect());

            BundleBuildResult result = GdoBundleGraphBuilder.Build(graph, _output);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.BundlePath, Is.EqualTo(Path.Combine(_output, "my-graph.assets")));
            AssetBundle bundle = AssetBundle.LoadFromFile(result.BundlePath);
            try
            {
                Assert.That(bundle.LoadAllAssets<GameDataObject>().Select(asset => asset.ID), Is.EqualTo(new[] { -670546766 }));
                Assert.That(bundle.LoadAllAssets<GdoBundleGraphAsset>(), Is.Empty);
            }
            finally { bundle.Unload(true); }
        }

        [Test]
        public void InvalidGraphStopsBeforeOutputCreation()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Invalid");
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Zero"; item.ID = 0;
            AssetDatabase.CreateAsset(item, GdoBundleProjectRepository.GdoFolder(graph) + "/Zero.asset");
            GdoBundleGraphRepository.AddMember(graph, item, new Rect());

            BundleBuildResult result = GdoBundleGraphBuilder.Build(graph, _output);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Some.Property("Message").Contains("non-zero"));
            Assert.That(Directory.Exists(_output), Is.False);
        }

        [Test]
        public void BuildRejectsCompanionRelationshipDriftWithoutOverwritingIt()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Drift");
            Item owner = ScriptableObject.CreateInstance<Item>(); owner.name = "Owner"; owner.ID = GdoProjectIdentity.RuntimeId("Drift", "Owner");
            Item target = ScriptableObject.CreateInstance<Item>(); target.name = "Target"; target.ID = GdoProjectIdentity.RuntimeId("Drift", "Target");
            AssetDatabase.CreateAsset(owner, GdoBundleProjectRepository.GdoFolder(graph) + "/Owner.asset"); AssetDatabase.CreateAsset(target, GdoBundleProjectRepository.GdoFolder(graph) + "/Target.asset");
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            GdoBundleGraphRepository.AddEdge(graph, targetNode.Id, "Value", ownerNode.Id, "KitchenData.Item.DirtiesTo", 0);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(owner);
            companion.UpsertField("KitchenData.Item", "DirtiesTo",
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 999, "KitchenData.Item")));

            BundleBuildResult result = GdoBundleGraphBuilder.Build(graph, _output);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(companion.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField retained), Is.True);
            Assert.That(retained.Value.ReferenceValue.Id, Is.EqualTo(999));
            Assert.That(Directory.Exists(_output), Is.False);
        }
    }
}
