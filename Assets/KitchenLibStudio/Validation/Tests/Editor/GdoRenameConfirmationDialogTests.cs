using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoRenameConfirmationDialogTests
    {
        private const string Root = "Assets/RenameDialogTests";
        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.CreateFolder("Assets", "RenameDialogTests"); }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(Root); Undo.ClearAll(); }

        [Test]
        public void GdoModelExposesCanonicalIdentityIdChangesAndCompatibilityWarningWithoutMutation()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Bakery");
            GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Flour", new Rect());
            GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(graph, node.Id, "Bread Flour");
            var model = new GdoRenameConfirmationModel(preview);

            Assert.That(model.RequiresCompatibilityWarning, Is.True);
            Assert.That(model.AffectedCount, Is.EqualTo(1));
            Assert.That(model.Changes.Single().OldCanonicalIdentity, Is.EqualTo("bakery:flour"));
            Assert.That(model.Changes.Single().NewCanonicalIdentity, Is.EqualTo("bakery:bread flour"));
            Assert.That(model.Changes.Single().OldId, Is.EqualTo(node.Asset.ID));
            Assert.That(model.Changes.Single().NewId, Is.EqualTo(preview.NewId));
            Assert.That(node.Asset.name, Is.EqualTo("Flour"));
        }

        [Test]
        public void ProjectModelListsEveryAffectedGdoIncludingCaseOnlyRename()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Bakery");
            GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Flour", new Rect());
            GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item", "Water", new Rect());
            GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(graph, "BAKERY");
            var model = new GdoRenameConfirmationModel(preview);

            Assert.That(model.RequiresCompatibilityWarning, Is.True);
            Assert.That(model.AffectedCount, Is.EqualTo(2));
            Assert.That(model.Changes.Select(value => value.OldCanonicalIdentity), Is.EquivalentTo(new[] { "bakery:flour", "bakery:water" }));
            Assert.That(model.Changes.All(value => value.OldCanonicalIdentity == value.NewCanonicalIdentity && value.OldId == value.NewId), Is.True);
            Assert.That(graph.BundleName, Is.EqualTo("Bakery"));
        }
    }
}
