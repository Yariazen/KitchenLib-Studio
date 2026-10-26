using System;
using System.IO;
using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class DoughBallBundleProjectTests
    {
        private const string Root = "Assets/DoughBallBundleProjectTests";
        // Stable external-reference fixtures, not claims about IDs in an installed game.
        private const int FlourFixtureId = 10001;
        private const int WaterFixtureId = 10002;
        private const string SetsPath = "KitchenData.ItemGroup.Sets[]";
        private string _package;
        private string _companionPath;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "DoughBallBundleProjectTests");
            _package = Path.Combine(Path.GetTempPath(), "KitchenLib Dough Ball Proof", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(_companionPath)) AssetDatabase.DeleteAsset(_companionPath);
            AssetDatabase.DeleteAsset(Root);
            if (Directory.Exists(_package)) Directory.Delete(_package, true);
        }

        [Test]
        public void NamespacedDoughBallRetainsTwoExternalIngredientsAfterReloadAndBuildsDataOnlyBundle()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "MyBakery");
            GdoGraphNodeRecord doughBall = GdoGraphAssetCommands.CreateGdo(
                graph, "KitchenData.ItemGroup", "DoughBall", new Rect(20, 20, 250, 200));
            _companionPath = AssetDatabase.GetAssetPath(GdoOverlayAuthoringRepository.Find(doughBall.Asset));
            var controller = new GdoGraphConnectionController(graph);
            GdoGraphRelationshipRecord set = controller.CreateNextRelationship(
                doughBall.Id, SetsPath, new Rect(320, 20, 250, 200));
            GdoOverlayValue value = GdoOverlayValueEditor.ReplaceStructMember(set.Value, "Min", GdoOverlayValue.Scalar("2"));
            value = GdoOverlayValueEditor.ReplaceStructMember(value, "Max", GdoOverlayValue.Scalar("2"));
            value = GdoOverlayValueEditor.ReplaceStructMember(value, "IsMandatory", GdoOverlayValue.Scalar("True"));
            value = GdoOverlayValueEditor.ReplaceStructMember(value, "Items",
                GdoOverlayValue.List(new[] { GdoOverlayValue.Null(), GdoOverlayValue.Null() }));
            controller.SetRelationshipValue(set.Id, value);

            GdoGraphNodeRecord flour = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, FlourFixtureId, "KitchenData.Item", new Rect(20, 300, 200, 100));
            GdoGraphNodeRecord water = GdoBundleGraphRepository.AddExternal(
                graph, ExternalGdoCatalogRepository.VanillaProviderKey, WaterFixtureId, "KitchenData.Item", new Rect(250, 300, 200, 100));
            controller.Connect(flour.Id, set.Id, SetsPath + ".Items[0]", 0);
            controller.Connect(water.Id, set.Id, SetsPath + ".Items[1]", 1);

            string graphPath = AssetDatabase.GetAssetPath(graph);
            string gdoPath = AssetDatabase.GetAssetPath(doughBall.Asset);
            AssetDatabase.SaveAssets();
            Undo.ClearAll();
            // Unload before importing so the assertions cannot read the in-memory graph fixture.
            Resources.UnloadAsset(graph);
            AssetDatabase.ImportAsset(gdoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(graphPath);
            ItemGroup reloaded = AssetDatabase.LoadAssetAtPath<ItemGroup>(gdoPath);

            Assert.That(graphPath, Is.EqualTo(Root + "/MyBakery/MyBakery.asset"));
            Assert.That(gdoPath, Is.EqualTo(Root + "/MyBakery/GDOs/DoughBall.asset"));
            Assert.That(graph.BundleName, Is.EqualTo("MyBakery"));
            Assert.That(graph.BundleFileName, Is.EqualTo("mybakery.assets"));
            Assert.That(reloaded.name, Is.EqualTo("DoughBall"));
            Assert.That(reloaded.ID, Is.EqualTo(GdoProjectIdentity.RuntimeId(
                "Test Author", "MyBakery", "DoughBall")));
            Assert.That(graph.Nodes.Count(node => node.Kind == GdoGraphNodeKind.Local), Is.EqualTo(1));
            Assert.That(graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.External).Select(node => node.ExternalId),
                Is.EquivalentTo(new[] { 10001, 10002 }));
            Assert.That(graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.External).Select(node => node.ExpectedType),
                Is.All.EqualTo("KitchenData.Item"));
            Assert.That(graph.Relationships.Single().FieldPath, Is.EqualTo(SetsPath));
            Assert.That(graph.Edges.Select(edge => edge.InputPortPath),
                Is.EquivalentTo(new[] { SetsPath + ".Items[0]", SetsPath + ".Items[1]" }));

            GdoGraphProjectionResult projection = GdoGraphRelationshipProjector.Project(graph);
            Assert.That(projection.Diagnostics, Is.Empty);
            AssertIngredientSet(projection.Fields.Single(field => field.FieldKey == "KitchenData.ItemGroup.Sets").Value);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(reloaded);
            Assert.That(companion.TryGetField("KitchenData.ItemGroup", "Sets", out GdoOverlayField overlay), Is.True);
            AssertIngredientSet(overlay.Value);

            BundleBuildResult result = GdoBundleGraphBuilder.Build(graph, Path.Combine(_package, "content"));
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            Assert.That(File.Exists(result.BundlePath), Is.True);
            Assert.That(Path.GetFileName(result.BundlePath), Is.EqualTo("mybakery.assets"));
            Assert.That(result.AuditReport.HasFatalFindings, Is.False, result.AuditReport.ToJson());
            Assert.That(Directory.GetFiles(_package, "*.dll", SearchOption.AllDirectories), Is.Empty);

            AssetBundle bundle = AssetBundle.LoadFromFile(result.BundlePath);
            Assert.That(bundle, Is.Not.Null);
            try
            {
                GameDataObject built = bundle.LoadAllAssets<GameDataObject>().Single();
                Assert.That(built, Is.TypeOf<ItemGroup>());
                Assert.That(built.name, Is.EqualTo("DoughBall"));
                Assert.That(built.ID, Is.EqualTo(reloaded.ID));
                Assert.That(bundle.LoadAllAssets<GdoBundleGraphAsset>(), Is.Empty);
                string manifest = bundle.LoadAllAssets<TextAsset>().Single(asset =>
                    asset.name == Path.GetFileNameWithoutExtension(GdoOverlayManifest.AssetName)).text;
                Assert.That(manifest, Does.Contain("\"field\":\"Sets\""));
                Assert.That(manifest, Does.Contain("\"expectedType\":\"KitchenData.Item\",\"id\":10001"));
                Assert.That(manifest, Does.Contain("\"expectedType\":\"KitchenData.Item\",\"id\":10002"));
            }
            finally { bundle.Unload(true); }
        }

        private static void AssertIngredientSet(GdoOverlayValue sets)
        {
            GdoOverlayValue set = sets.Values.Single();
            Assert.That(set.Members.Single(member => member.Name == "Min").Value.ScalarValue, Is.EqualTo("2"));
            Assert.That(set.Members.Single(member => member.Name == "Max").Value.ScalarValue, Is.EqualTo("2"));
            Assert.That(set.Members.Single(member => member.Name == "IsMandatory").Value.ScalarValue, Is.EqualTo("True"));
            GdoOverlayValue items = set.Members.Single(member => member.Name == "Items").Value;
            Assert.That(items.Values.Select(item => item.ReferenceValue.Id), Is.EqualTo(new[] { 10001, 10002 }));
            Assert.That(items.Values.Select(item => item.ReferenceValue.ExpectedType), Is.All.EqualTo("KitchenData.Item"));
            Assert.That(items.Values.Select(item => item.ReferenceValue.Asset), Is.All.Null);
        }
    }
}
