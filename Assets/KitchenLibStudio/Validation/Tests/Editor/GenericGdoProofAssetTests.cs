using System.IO;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GenericGdoProofAssetTests
    {
        [OneTimeTearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(GenericGdoProofAsset.Root); }

        [Test]
        public void CreateAuthorsAvailableDishItemAndApplianceThroughGenericDescriptors()
        {
            GameDataObject[] targets = GenericGdoProofAsset.CreateOrUpdate().ToArray();

            Assert.That(targets.Select(target => target.GetType()), Is.EquivalentTo(new[] { typeof(Dish), typeof(Item), typeof(Appliance) }));
            Assert.That(targets.Select(target => target.ID), Is.EquivalentTo(new[]
            {
                GenericGdoProofAsset.DishId, GenericGdoProofAsset.ItemId, GenericGdoProofAsset.ApplianceId
            }));
            Assert.That(targets.All(target => GdoDescriptorCatalog.ForTarget(target).IsAvailable), Is.True);
            Assert.That(targets.All(target => GdoOverlayAuthoringRepository.Find(target) != null), Is.True);
            Dish dish = targets.OfType<Dish>().Single();
            Appliance appliance = targets.OfType<Appliance>().Single();
            Assert.That(dish.IconPrefab.GetComponentInChildren<Renderer>().sharedMaterial,
                Is.SameAs(appliance.Prefab.GetComponentInChildren<Renderer>().sharedMaterial));
            Assert.That(GdoOverlayAuthoringRepository.Find(dish).Fields.Single(field => field.FieldName == "UnlockItemOverride")
                .Value.ReferenceValue.Asset.ID, Is.EqualTo(GenericGdoProofAsset.ItemId));
            Assert.That(GdoOverlayAuthoringRepository.Find(appliance).Fields.Single(field => field.FieldName == "CrateItem")
                .Value.ReferenceValue.Asset.ID, Is.EqualTo(GenericGdoProofAsset.ItemId));
            Item item = targets.OfType<Item>().Single();
            Assert.That(GdoOverlayAuthoringRepository.Find(item).Fields.Single(field => field.FieldName == "DirtiesTo")
                .Value.ReferenceValue.Asset, Is.SameAs(item));
        }

        [Test]
        public void BuildCreatesOneDataOnlyBundleContainingAllThreeTargets()
        {
            BundleBuildResult result = GenericGdoProofAsset.BuildPackage(GenericGdoProofAsset.PositivePackageName, false);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            Assert.That(AssetDatabase.LoadAssetAtPath<Dish>(GenericGdoProofAsset.DishPath).ID, Is.EqualTo(GenericGdoProofAsset.DishId));
            Assert.That(AssetDatabase.LoadAssetAtPath<Item>(GenericGdoProofAsset.ItemPath).ID, Is.EqualTo(GenericGdoProofAsset.ItemId));
            Assert.That(AssetDatabase.LoadAssetAtPath<Appliance>(GenericGdoProofAsset.AppliancePath).ID, Is.EqualTo(GenericGdoProofAsset.ApplianceId));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(result.BundlePath)), "*.dll", SearchOption.AllDirectories), Is.Empty);

            AssetBundle bundle = AssetBundle.LoadFromFile(result.BundlePath);
            try
            {
                Assert.That(bundle.GetAllAssetNames(), Does.Contain(DishPathInBundle()));
                Assert.That(bundle.GetAllAssetNames(), Does.Contain(ItemPathInBundle()));
                Assert.That(bundle.GetAllAssetNames(), Does.Contain(AppliancePathInBundle()));
                Assert.That(bundle.GetAllAssetNames(), Does.Contain("assets/__kitchenlibstudiobuild/kitchenlib-gdo-overlay.json"));
            }
            finally { bundle.Unload(true); }

            string DishPathInBundle() => GenericGdoProofAsset.DishPath.ToLowerInvariant();
            string ItemPathInBundle() => GenericGdoProofAsset.ItemPath.ToLowerInvariant();
            string AppliancePathInBundle() => GenericGdoProofAsset.AppliancePath.ToLowerInvariant();
        }
    }
}
