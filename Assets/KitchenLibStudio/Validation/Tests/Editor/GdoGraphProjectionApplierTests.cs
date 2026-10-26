using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphProjectionApplierTests
    {
        private const string Root = "Assets/GraphApplyTests";
        [SetUp]
        public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.CreateFolder("Assets", "GraphApplyTests"); GdoBundleProjectRepository.Create(Root, "Test Author", "Graph"); }
        [TearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(Root); Undo.ClearAll(); }

        [Test]
        public void ValidProjectionAppliesToCompanionAndUndoRestoresPreviousValue()
        {
            Item owner = Create<Item>("Owner", GdoProjectIdentity.RuntimeId("Graph", "Owner"));
            Item target = Create<Item>("Target", GdoProjectIdentity.RuntimeId("Graph", "Target"));
            GdoBundleGraphAsset graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(Root + "/Graph/Graph.asset");
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, target, new Rect());
            GdoBundleGraphRepository.AddEdge(graph, targetNode.Id, "Value", ownerNode.Id, "KitchenData.Item.DirtiesTo", 0);
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.GetOrCreate(owner);
            overlay.UpsertField("KitchenData.Item", "DirtiesTo",
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 777, "KitchenData.Item")));
            EditorUtility.SetDirty(overlay);
            AssetDatabase.SaveAssets();

            bool applied = GdoGraphProjectionApplier.TryApply(graph, out var diagnostics);

            Assert.That(applied, Is.True, string.Join("; ", diagnostics.Select(value => value.Message)));
            Assert.That(overlay.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField projected), Is.True);
            Assert.That(projected.Value.ReferenceValue.Asset, Is.SameAs(target));
            Undo.PerformUndo();
            Assert.That(overlay.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField restored), Is.True);
            Assert.That(restored.Value.ReferenceValue.Id, Is.EqualTo(777));
        }

        [Test]
        public void InvalidProjectionLeavesEveryCompanionUntouched()
        {
            Item first = Create<Item>("First", 200);
            Item second = Create<Item>("Second", 200);
            GdoBundleGraphAsset graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(Root + "/Graph/Graph.asset");
            GdoBundleGraphRepository.AddMember(graph, first, new Rect());
            GdoBundleGraphRepository.AddMember(graph, second, new Rect());
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.GetOrCreate(first);
            overlay.UpsertField("KitchenData.Item", "DirtiesTo",
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 888, "KitchenData.Item")));

            bool applied = GdoGraphProjectionApplier.TryApply(graph, out var diagnostics);

            Assert.That(applied, Is.False);
            Assert.That(diagnostics.Select(value => value.Category), Does.Contain("DuplicateId"));
            Assert.That(overlay.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField retained), Is.True);
            Assert.That(retained.Value.ReferenceValue.Id, Is.EqualTo(888));
        }

        private static T Create<T>(string name, int id) where T : GameDataObject
        {
            T value = ScriptableObject.CreateInstance<T>(); value.name = name; value.ID = id;
            AssetDatabase.CreateAsset(value, Root + "/Graph/GDOs/" + name + ".asset"); return value;
        }
    }
}
