using System;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoOverlayAuthoringTests
    {
        [Test]
        public void UpsertLookupAndRemoveFieldDoNotCreateDuplicates()
        {
            GdoOverlayAuthoringAsset companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                companion.UpsertField("KitchenData.Item", "DirtiesTo", GdoOverlayValue.String("first"));
                companion.UpsertField("KitchenData.Item", "DirtiesTo", GdoOverlayValue.String("second"));

                Assert.That(companion.Fields, Has.Count.EqualTo(1));
                Assert.That(companion.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField field), Is.True);
                Assert.That(field.Value.ScalarValue, Is.EqualTo("second"));
                Assert.That(companion.RemoveField("KitchenData.Item", "DirtiesTo"), Is.True);
                Assert.That(companion.TryGetField("KitchenData.Item", "DirtiesTo", out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(companion); }
        }

        [Test]
        public void ReferenceTokensPreserveCanonicalIdExpectedTypeAndOptionalProjectAsset()
        {
            Item asset = ScriptableObject.CreateInstance<Item>();
            asset.ID = -123;
            try
            {
                GdoReferenceToken project = GdoReferenceToken.ForAsset(asset, "KitchenData.Item");
                GdoReferenceToken vanilla = GdoReferenceToken.Create(
                    ExternalGdoCatalogRepository.VanillaProviderKey, 456, "KitchenData.Item");

                Assert.That(project.Id, Is.EqualTo(-123));
                Assert.That(project.ExpectedType, Is.EqualTo("KitchenData.Item"));
                Assert.That(project.Asset, Is.SameAs(asset));
                Assert.That(vanilla.Id, Is.EqualTo(456));
                Assert.That(vanilla.Asset, Is.Null);
                Assert.Throws<ArgumentException>(() => GdoReferenceToken.Create(
                    ExternalGdoCatalogRepository.VanillaProviderKey, 0, "KitchenData.Item"));
                Assert.Throws<ArgumentException>(() => GdoReferenceToken.Create(
                    string.Empty, 456, "KitchenData.Item"));
                Assert.Throws<ArgumentException>(() => GdoReferenceToken.ForAsset(asset, "KitchenData.Dish"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void SetRejectsDuplicateReferenceIdsInsteadOfSilentlyCollapsingThem()
        {
            GdoReferenceToken first = GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item");
            GdoReferenceToken duplicate = GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item");

            Assert.Throws<ArgumentException>(() => GdoOverlayValue.Set(
                new[] { GdoOverlayValue.Reference(first), GdoOverlayValue.Reference(duplicate) }));
        }

        [Test]
        public void SetRejectsStructurallyEqualNestedValues()
        {
            GdoOverlayValue first = GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("Item", GdoOverlayValue.Reference(
                    GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item"))),
                new GdoOverlayMember("Amount", GdoOverlayValue.Scalar("2"))
            });
            GdoOverlayValue duplicate = GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("Amount", GdoOverlayValue.Scalar("2")),
                new GdoOverlayMember("Item", GdoOverlayValue.Reference(
                    GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item")))
            });

            Assert.Throws<ArgumentException>(() => GdoOverlayValue.Set(new[] { first, duplicate }));
        }

        [Test]
        public void NestedMenuItemAndIngredientUnlockSurviveUnityJsonRoundTrip()
        {
            GdoOverlayValue value = GdoOverlayValue.List(new[]
            {
                GdoOverlayValue.Struct(new[]
                {
                    new GdoOverlayMember("Item", GdoOverlayValue.Reference(
                        GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item"))),
                    new GdoOverlayMember("IngredientUnlock", GdoOverlayValue.Struct(new[]
                    {
                        new GdoOverlayMember("MenuItem", GdoOverlayValue.Reference(
                            GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 84, "KitchenData.Item")))
                    }))
                })
            });

            Assert.That(GdoOverlaySerializedGraph.Encode(value).TryDecode(
                out GdoOverlayValue restored,
                out System.Collections.Generic.IReadOnlyList<GdoOverlayPersistenceDiagnostic> diagnostics),
                Is.True, string.Join("\n", diagnostics.Select(issue => issue.Message)));

            Assert.That(restored.Kind, Is.EqualTo(GdoOverlayValueKind.List));
            Assert.That(restored.Values.Single().Members.Single(member => member.Name == "Item").Value.ReferenceValue.Id,
                Is.EqualTo(42));
            Assert.That(restored.Values.Single().Members.Single(member => member.Name == "IngredientUnlock")
                .Value.Members.Single().Value.ReferenceValue.Id, Is.EqualTo(84));
        }

        [Test]
        public void CompanionStoresOneTypedFieldPerDeclaringTypeAndName()
        {
            Dish target = ScriptableObject.CreateInstance<Dish>();
            GdoOverlayAuthoringAsset companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                target.ID = -1000;
                companion.Initialise(target);
                companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
                {
                    GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item"))
                }));

                Assert.That(companion.Target, Is.SameAs(target));
                Assert.That(companion.Fields.Single().DeclaringType, Is.EqualTo("KitchenData.Dish"));
                Assert.That(companion.Fields.Single().FieldName, Is.EqualTo("MinimumIngredients"));
                Assert.Throws<InvalidOperationException>(() => companion.SetField(
                    "KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Null()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(companion);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void LocalizationMapRejectsDuplicateLocalesAndOrdersEntriesNumerically()
        {
            GdoLocaleEntry english = new GdoLocaleEntry(1, "English");
            GdoLocaleEntry french = new GdoLocaleEntry(2, "French");

            GdoOverlayValue map = GdoOverlayValue.LocalizationMap(new[] { french, english });

            Assert.That(map.Locales.Select(entry => entry.Locale), Is.EqualTo(new[] { 1, 2 }));
            Assert.Throws<ArgumentException>(() => GdoOverlayValue.LocalizationMap(new[] { english, english }));
        }

        [Test]
        public void LocalizationEntryPreservesCompleteUnlockInfo()
        {
            var entry = new GdoLocaleEntry(1, "Name", "Description", "Flavour");
            Assert.That(GdoOverlaySerializedGraph.Encode(GdoOverlayValue.LocalizationMap(new[] { entry })).TryDecode(
                out GdoOverlayValue restored,
                out System.Collections.Generic.IReadOnlyList<GdoOverlayPersistenceDiagnostic> diagnostics),
                Is.True, string.Join("\n", diagnostics.Select(issue => issue.Message)));
            Assert.That(restored.Locales.Single().DisplayName, Is.EqualTo("Name"));
            Assert.That(restored.Locales.Single().Description, Is.EqualTo("Description"));
            Assert.That(restored.Locales.Single().FlavourText, Is.EqualTo("Flavour"));
        }
    }
}
