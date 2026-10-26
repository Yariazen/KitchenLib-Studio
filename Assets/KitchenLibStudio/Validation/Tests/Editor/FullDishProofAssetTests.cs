using System.IO;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class FullDishProofAssetTests
    {
        [TearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(FullDishProofAsset.Root); }

        [Test]
        public void CreateBuildsCompleteDishOverlayAndPresentationAssets()
        {
            Dish dish = FullDishProofAsset.CreateOrUpdate();
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(dish);

            Assert.That(dish.ID, Is.EqualTo(FullDishProofAsset.DishId));
            Assert.That(dish.Difficulty, Is.EqualTo(3));
            Assert.That(dish.IconPrefab.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(dish.DisplayPrefab.GetComponentInChildren<Renderer>().sharedMaterial, Is.Not.Null);
            Assert.That(overlay.Fields.Select(field => field.FieldName), Is.EquivalentTo(new[]
            {
                "Info", "HardcodedRequirements", "HardcodedBlockers", "UnlockItemOverride",
                "ResultingMenuItems", "IngredientsUnlocks", "MinimumIngredients", "ExtraOrderUnlocks",
                "RequiredProcesses", "BlockProviders", "BeneficialIngredients"
            }));
            Assert.That(overlay.Fields.Single(field => field.FieldName == "Info").Value.Locales, Has.Count.EqualTo(2));
            Assert.That(overlay.Fields.Single(field => field.FieldName == "ResultingMenuItems").Value.Values.Single().Members, Has.Count.EqualTo(5));
            Assert.That(overlay.Fields.Single(field => field.FieldName == "IngredientsUnlocks").Value.Values.Single().Members, Has.Count.EqualTo(2));
            Assert.That(FullDishProofAsset.RuntimeAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(dish)));
            Assert.That(FullDishProofAsset.RuntimeAssetPaths, Does.Not.Contain(AssetDatabase.GetAssetPath(overlay)));
        }

        [Test]
        public void NegativeBuildRestoresPositiveAuthoringOverlay()
        {
            FullDishProofAsset.BuildNegativePackageForBatch();
            Dish dish = AssetDatabase.LoadAssetAtPath<Dish>(FullDishProofAsset.DishPath);
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.Find(dish);
            GdoOverlayValue menu = overlay.Fields.Single(field => field.FieldName == "ResultingMenuItems").Value;
            int nestedId = menu.Values.Single().Members.Single(member => member.Name == "DynamicMenuIngredient").Value.ReferenceValue.Id;
            Assert.That(nestedId, Is.EqualTo(FullDishProofAsset.OnionId));
        }
    }
}
