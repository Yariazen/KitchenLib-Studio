using System;
using System.IO;
using System.Linq;
using System.Text;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoLinkManifestWriterTests
    {
        private const string Fingerprint = "1B4E7643CA9A192B4FD5BE20178C71B7C23309486DEA15969BFD477AF91A98FF";
        private string _mirrorPath;
        private Dish _dish;
        private GdoLinkAuthoringAsset _companion;

        [SetUp]
        public void SetUp()
        {
            _mirrorPath = Path.GetTempFileName();
            File.WriteAllText(_mirrorPath, ValidMirrorJson(), new UTF8Encoding(false));
            _dish = ScriptableObject.CreateInstance<Dish>();
            _dish.ID = -1950922446;
            _companion = ScriptableObject.CreateInstance<GdoLinkAuthoringAsset>();
            _companion.Initialise(_dish);
            _companion.SetMinimumIngredientIds(new[] { -201067776 });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_companion);
            UnityEngine.Object.DestroyImmediate(_dish);
            File.Delete(_mirrorPath);
        }

        [Test]
        public void WriteEmitsExactVersionOneDishManifest()
        {
            string json = GdoLinkManifestWriter.Write(
                new GameDataObject[] { _dish },
                new[] { _companion },
                _mirrorPath);

            const string expected =
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"mirrorFingerprint\": \"" + Fingerprint + "\",\n" +
                "  \"targets\": [\n" +
                "    {\n" +
                "      \"gdoId\": -1950922446,\n" +
                "      \"gdoType\": \"Dish\",\n" +
                "      \"links\": [\n" +
                "        {\n" +
                "          \"field\": \"MinimumIngredients\",\n" +
                "          \"kind\": \"GdoHashSet\",\n" +
                "          \"elementType\": \"Item\",\n" +
                "          \"ids\": [-201067776]\n" +
                "        }\n" +
                "      ]\n" +
                "    }\n" +
                "  ]\n" +
                "}\n";
            Assert.That(json, Is.EqualTo(expected));
            Assert.That(new UTF8Encoding(false).GetPreamble(), Is.Empty);
            Assert.That(json.EndsWith("\n", StringComparison.Ordinal), Is.True);
            Assert.That(json, Does.Not.Contain("\r"));
        }

        [Test]
        public void WriteSortsTargetsAndIdsOrdinally()
        {
            Dish secondDish = ScriptableObject.CreateInstance<Dish>();
            secondDish.ID = -2000000000;
            GdoLinkAuthoringAsset second = ScriptableObject.CreateInstance<GdoLinkAuthoringAsset>();
            second.Initialise(secondDish);
            second.SetMinimumIngredientIds(new[] { 20, -30, 10 });
            _companion.SetMinimumIngredientIds(new[] { 3, -1, 2 });

            string json = GdoLinkManifestWriter.Write(
                new GameDataObject[] { _dish, secondDish },
                new[] { _companion, second },
                _mirrorPath);

            Assert.That(json.IndexOf("-2000000000", StringComparison.Ordinal), Is.LessThan(json.IndexOf("-1950922446", StringComparison.Ordinal)));
            Assert.That(json, Does.Contain("\"ids\": [-30, 10, 20]"));
            Assert.That(json, Does.Contain("\"ids\": [-1, 2, 3]"));
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(secondDish);
        }

        [TestCase("not json")]
        [TestCase("{\"schemaVersion\":1,\"sourceSha256\":\"x\",\"supportedTypes\":[]}")]
        public void WriteRejectsMalformedOrIncompatibleMirrorMetadata(string mirrorJson)
        {
            File.WriteAllText(_mirrorPath, mirrorJson);
            Assert.Throws<InvalidOperationException>(() => GdoLinkManifestWriter.Write(
                new GameDataObject[] { _dish }, new[] { _companion }, _mirrorPath));
        }

        [Test]
        public void WriteRejectsZeroAndDuplicateIds()
        {
            SerializedObject serialized = new SerializedObject(_companion);
            SerializedProperty ids = serialized.FindProperty("_minimumIngredientIds");
            ids.arraySize = 2;
            ids.GetArrayElementAtIndex(0).intValue = 0;
            ids.GetArrayElementAtIndex(1).intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(() => GdoLinkManifestWriter.Write(
                new GameDataObject[] { _dish }, new[] { _companion }, _mirrorPath));

            _companion.SetMinimumIngredientIds(new[] { 7, 7 });
            Assert.Throws<InvalidOperationException>(() => GdoLinkManifestWriter.Write(
                new GameDataObject[] { _dish }, new[] { _companion }, _mirrorPath));
        }

        [Test]
        public void WriteRejectsDuplicateTargetsAndUnsupportedTargetTypes()
        {
            Assert.Throws<InvalidOperationException>(() => GdoLinkManifestWriter.Write(
                new GameDataObject[] { _dish }, new[] { _companion, _companion }, _mirrorPath));

            GameDifficultySettings unsupported = ScriptableObject.CreateInstance<GameDifficultySettings>();
            unsupported.ID = 42;
            GdoLinkAuthoringAsset unsupportedCompanion = ScriptableObject.CreateInstance<GdoLinkAuthoringAsset>();
            unsupportedCompanion.Initialise(unsupported);
            unsupportedCompanion.SetMinimumIngredientIds(new[] { 1 });
            Assert.Throws<InvalidOperationException>(() => GdoLinkManifestWriter.Write(
                new GameDataObject[] { unsupported }, new[] { unsupportedCompanion }, _mirrorPath));
            UnityEngine.Object.DestroyImmediate(unsupportedCompanion);
            UnityEngine.Object.DestroyImmediate(unsupported);
        }

        private static string ValidMirrorJson()
        {
            return "{\"schemaVersion\":1,\"sourceSha256\":\"" + Fingerprint + "\",\"supportedTypes\":[{" +
                   "\"fullName\":\"KitchenData.Dish\",\"fields\":[],\"linkFields\":[{" +
                   "\"name\":\"MinimumIngredients\",\"linkKind\":\"GdoHashSet\",\"elementType\":\"KitchenData.Item\"}]}]}";
        }
    }
}
