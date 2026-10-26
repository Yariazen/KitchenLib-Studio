using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoDescriptorValueValidatorTests
    {
        [Test]
        public void WrongNodeKindReportsExactDescriptorFieldPath()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            GdoOverlayAuthoringAsset companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                companion.Initialise(dish);
                companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.String("wrong"));

                ValidationIssue issue = GdoDescriptorValueValidator.Validate(
                    dish, companion, GdoDescriptorCatalog.ForTarget(dish))
                    .Single(item => item.PropertyPath == "KitchenData.Dish.MinimumIngredients");

                Assert.That(issue.Severity, Is.EqualTo(ValidationSeverity.Fatal));
                Assert.That(issue.PropertyPath, Is.EqualTo("KitchenData.Dish.MinimumIngredients"));
                Assert.That(issue.Message, Does.Contain("Expected Set"));
            }
            finally
            {
                Object.DestroyImmediate(companion);
                Object.DestroyImmediate(dish);
            }
        }

        [Test]
        public void ValidGdoReferenceSetPassesRecursiveValidation()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            GdoOverlayAuthoringAsset companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                companion.Initialise(dish);
                companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
                {
                    GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 17, "KitchenData.Item"))
                }));

                Assert.That(GdoDescriptorValueValidator.Validate(
                    dish, companion, GdoDescriptorCatalog.ForTarget(dish))
                    .Where(issue => issue.PropertyPath == "KitchenData.Dish.MinimumIngredients"), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(companion);
                Object.DestroyImmediate(dish);
            }
        }

        [Test]
        public void MissingRequiredFieldsAreReportedWithoutCompanion()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            try
            {
                Assert.That(GdoDescriptorValueValidator.Validate(
                    dish, null, GdoDescriptorCatalog.ForTarget(dish)),
                    Has.Some.Matches<ValidationIssue>(issue => issue.Message.Contains("Required overlay field is missing")));
            }
            finally { Object.DestroyImmediate(dish); }
        }

        [Test]
        public void ReferenceExpectedTypeMismatchIsReportedAtFieldPath()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            GdoOverlayAuthoringAsset companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                companion.Initialise(dish);
                companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
                {
                    GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 17, "KitchenData.Appliance"))
                }));

                Assert.That(GdoDescriptorValueValidator.Validate(
                    dish, companion, GdoDescriptorCatalog.ForTarget(dish)),
                    Has.Some.Matches<ValidationIssue>(issue => issue.PropertyPath == "KitchenData.Dish.MinimumIngredients[0]" &&
                        issue.Message.Contains("expected type")));
            }
            finally
            {
                Object.DestroyImmediate(companion);
                Object.DestroyImmediate(dish);
            }
        }

        [Test]
        public void StructuralDuplicateDetectionHandlesLocalesAndNestedSetOrder()
        {
            GdoOverlayValue firstLocale = GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "First") });
            GdoOverlayValue secondLocale = GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "Second") });
            GdoOverlayValue setOne = GdoOverlayValue.Set(new[] { GdoOverlayValue.String("a"), GdoOverlayValue.String("b") });
            GdoOverlayValue setTwo = GdoOverlayValue.Set(new[] { GdoOverlayValue.String("b"), GdoOverlayValue.String("a") });

            Assert.That(GdoDescriptorValueValidator.ContainsStructuralDuplicates(new[] { firstLocale, secondLocale }), Is.False);
            Assert.That(GdoDescriptorValueValidator.ContainsStructuralDuplicates(new[] { setOne, setTwo }), Is.True);
        }
    }
}
