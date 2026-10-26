using System;
using System.IO;
using System.Linq;
using System.Text;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoOverlayManifestWriterTests
    {
        private string _schemaPath;
        private Dish _dish;
        private GdoOverlayAuthoringAsset _overlay;

        [SetUp]
        public void SetUp()
        {
            _schemaPath = Path.GetTempFileName();
            File.WriteAllText(_schemaPath, "{}", new UTF8Encoding(false));
            _dish = ScriptableObject.CreateInstance<Dish>();
            _dish.ID = -100;
            _overlay = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            _overlay.Initialise(_dish);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_overlay);
            UnityEngine.Object.DestroyImmediate(_dish);
            File.Delete(_schemaPath);
        }

        [Test]
        public void WriteEmitsExactCanonicalSchemaVersionThreeJson()
        {
            _overlay.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
            {
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 20, "KitchenData.Item")),
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, -5, "KitchenData.Item"))
            }));
            _overlay.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(2, "Français", "Description FR", "Saveur"), new GdoLocaleEntry(1, "English", "Description EN", "Flavour")
            }));
            CompleteRequiredFields(_overlay);

            string json = GdoOverlayManifestWriter.Write(new GameDataObject[] { _dish }, new[] { _overlay }, _schemaPath);

            string expected = "{\n" +
                "  \"schemaVersion\": 3,\n" +
                "  \"globalFingerprint\": \"" + GdoDescriptorCatalog.GlobalFingerprint + "\",\n" +
                "  \"targets\": [\n" +
                "    {\n" +
                "      \"gdoId\": -100,\n" +
                "      \"gdoType\": \"KitchenData.Dish\",\n" +
                "      \"descriptorFingerprint\": \"" + GdoDescriptorCatalog.ForTarget(_dish).DescriptorFingerprint + "\",\n" +
                "      \"fields\": [\n" +
                "        {\"declaringType\":\"KitchenData.Dish\",\"field\":\"BeneficialIngredients\",\"value\":{\"kind\":\"Set\",\"values\":[]}},\n" +
                "        {\"declaringType\":\"KitchenData.Dish\",\"field\":\"BlockProviders\",\"value\":{\"kind\":\"Set\",\"values\":[]}},\n" +
                "        {\"declaringType\":\"KitchenData.Dish\",\"field\":\"ExtraOrderUnlocks\",\"value\":{\"kind\":\"Set\",\"values\":[]}},\n" +
                "        {\"declaringType\":\"KitchenData.Dish\",\"field\":\"IngredientsUnlocks\",\"value\":{\"kind\":\"Set\",\"values\":[]}},\n" +
                "        {\"declaringType\":\"KitchenData.Dish\",\"field\":\"MinimumIngredients\",\"value\":{\"kind\":\"Set\",\"values\":[{\"kind\":\"GdoReference\",\"expectedType\":\"KitchenData.Item\",\"id\":-5},{\"kind\":\"GdoReference\",\"expectedType\":\"KitchenData.Item\",\"id\":20}]}},\n" +
                "        {\"declaringType\":\"KitchenData.Dish\",\"field\":\"RequiredProcesses\",\"value\":{\"kind\":\"Set\",\"values\":[]}},\n" +
                "        {\"declaringType\":\"KitchenData.LocalisedGameDataObject`1\",\"field\":\"Info\",\"value\":{\"kind\":\"LocalizationMap\",\"locales\":[{\"locale\":1,\"displayName\":\"English\",\"description\":\"Description EN\",\"flavourText\":\"Flavour\"},{\"locale\":2,\"displayName\":\"Français\",\"description\":\"Description FR\",\"flavourText\":\"Saveur\"}]}}\n" +
                "      ]\n" +
                "    }\n" +
                "  ]\n" +
                "}\n";
            Assert.That(json, Is.EqualTo(expected));
            Assert.That(json, Does.Not.Contain("\r"));
        }

        [Test]
        public void WriteIsByteIdenticalWhenTargetsFieldsAndSetValuesAreReordered()
        {
            _overlay.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
            {
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 2, "KitchenData.Item")),
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 1, "KitchenData.Item"))
            }));
            CompleteRequiredFields(_overlay);
            string first = GdoOverlayManifestWriter.Write(new GameDataObject[] { _dish }, new[] { _overlay }, _schemaPath);

            GdoOverlayAuthoringAsset reordered = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                reordered.Initialise(_dish);
                reordered.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
                {
                    GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 1, "KitchenData.Item")),
                    GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 2, "KitchenData.Item"))
                }));
                CompleteRequiredFields(reordered);
                string second = GdoOverlayManifestWriter.Write(new GameDataObject[] { _dish }, new[] { reordered }, _schemaPath);
                Assert.That(Encoding.UTF8.GetBytes(second), Is.EqualTo(Encoding.UTF8.GetBytes(first)));
            }
            finally { UnityEngine.Object.DestroyImmediate(reordered); }
        }

        [Test]
        public void WriteRejectsUnknownFieldsBeforeSerialization()
        {
            _overlay.SetField("KitchenData.Dish", "DoesNotExist", GdoOverlayValue.Null());
            Assert.Throws<InvalidOperationException>(() =>
                GdoOverlayManifestWriter.Write(new GameDataObject[] { _dish }, new[] { _overlay }, _schemaPath));
        }

        [Test]
        public void WriteRejectsNativeFieldInSidecar()
        {
            _overlay.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            _overlay.SetField("KitchenData.Dish", "Difficulty", GdoOverlayValue.Scalar("3"));
            Assert.Throws<InvalidOperationException>(() =>
                GdoOverlayManifestWriter.Write(new GameDataObject[] { _dish }, new[] { _overlay }, _schemaPath));
        }

        [Test]
        public void WriteRejectsLocalizationNodeForNonLocalizationField()
        {
            _overlay.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "Wrong") }));
            Assert.Throws<InvalidOperationException>(() =>
                GdoOverlayManifestWriter.Write(new GameDataObject[] { _dish }, new[] { _overlay }, _schemaPath));
        }

        private static void CompleteRequiredFields(GdoOverlayAuthoringAsset overlay)
        {
            if (!overlay.Fields.Any(field => field.DeclaringType == "KitchenData.LocalisedGameDataObject`1" && field.FieldName == "Info"))
                overlay.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new GdoLocaleEntry[0]));
            foreach (string fieldName in new[] { "BeneficialIngredients", "BlockProviders", "ExtraOrderUnlocks", "IngredientsUnlocks", "MinimumIngredients", "RequiredProcesses" })
                if (!overlay.Fields.Any(field => field.DeclaringType == "KitchenData.Dish" && field.FieldName == fieldName))
                    overlay.SetField("KitchenData.Dish", fieldName, GdoOverlayValue.Set(new GdoOverlayValue[0]));
        }
    }
}
