using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoBundleBuilderTests
    {
        private const string AssetRoot = "Assets/__KitchenLibStudioBundleTests";
        private string _outputRoot;
        private readonly List<string> _companionPaths = new List<string>();

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(AssetRoot);
            AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioBundleTests");
            _outputRoot = Path.Combine(Path.GetTempPath(), "KitchenLib Studio Bundle Tests", Guid.NewGuid().ToString("N"), "content");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string path in _companionPaths)
            {
                AssetDatabase.DeleteAsset(path);
            }
            _companionPaths.Clear();
            AssetDatabase.DeleteAsset(AssetRoot);
            string package = Directory.GetParent(_outputRoot)?.FullName;
            if (!string.IsNullOrEmpty(package) && Directory.Exists(package))
            {
                Directory.Delete(package, true);
            }
        }

        [Test]
        public void BuildCreatesLoadableScalarProofBundle()
        {
            var request = new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath },
                _outputRoot,
                "scalar-proof.assets");

            BundleBuildResult result = GdoBundleBuilder.Build(request);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.BundlePath, Is.EqualTo(Path.Combine(_outputRoot, "scalar-proof.assets")));
            Assert.That(File.Exists(result.BundlePath), Is.True);
            Assert.That(result.AuditReport, Is.Not.Null);
            Assert.That(result.AuditReport.HasFatalFindings, Is.False, result.AuditReport.ToJson());
            Assert.That(File.Exists(Path.Combine(Directory.GetParent(_outputRoot).FullName, GdoPackageAudit.ReportFileName)), Is.True);
        }

        [Test]
        public void BuildRejectsPackageContainingManagedBinaryAfterBundleCreation()
        {
            string packageRoot = Directory.GetParent(_outputRoot).FullName;
            Directory.CreateDirectory(packageRoot);
            File.WriteAllBytes(Path.Combine(packageRoot, "leak.dll"), new byte[] { 1, 2, 3 });

            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath }, _outputRoot, "scalar-proof.assets"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.AuditReport.Findings, Has.Some.Property("Category").EqualTo("ManagedBinary"));
        }

        [Test]
        public void BuildProducesByteIdenticalBundleAcrossCleanOutputDirectories()
        {
            string secondOutputRoot = Path.Combine(
                Directory.GetParent(_outputRoot).FullName,
                "second-content");
            var firstRequest = new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath },
                _outputRoot,
                "scalar-proof.assets");
            var secondRequest = new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath },
                secondOutputRoot,
                "scalar-proof.assets");

            BundleBuildResult first = GdoBundleBuilder.Build(firstRequest);
            BundleBuildResult second = GdoBundleBuilder.Build(secondRequest);

            Assert.That(first.Succeeded, Is.True, string.Join("\n", first.Issues));
            Assert.That(second.Succeeded, Is.True, string.Join("\n", second.Issues));
            Assert.That(Sha256(first.BundlePath), Is.EqualTo(Sha256(second.BundlePath)));
        }

        [TestCase("scalar-proof.bundle", "extension")]
        [TestCase("../scalar-proof.assets", "file name")]
        public void BuildRejectsInvalidBundleFileName(string fileName, string expectedMessage)
        {
            var request = new BundleBuildRequest(new[] { ScalarProofAsset.AssetPath }, _outputRoot, fileName);

            BundleBuildResult result = GdoBundleBuilder.Build(request);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Some.Property("Message").Contains(expectedMessage).IgnoreCase);
            Assert.That(Directory.Exists(_outputRoot), Is.False);
        }

        [Test]
        public void BuildRejectsMissingAssetPath()
        {
            const string missing = "Assets/Does Not Exist.asset";

            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(new[] { missing }, _outputRoot, "scalar-proof.assets"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Some.Property("AssetPath").EqualTo(missing));
        }

        [Test]
        public void BuildRejectsZeroAndDuplicateIdsBeforeCreatingOutput()
        {
            GameDifficultySettings zero = Create("Zero.asset", 0);
            GameDifficultySettings duplicateOne = Create("DuplicateOne.asset", 42);
            GameDifficultySettings duplicateTwo = Create("DuplicateTwo.asset", 42);

            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { AssetDatabase.GetAssetPath(zero), AssetDatabase.GetAssetPath(duplicateOne), AssetDatabase.GetAssetPath(duplicateTwo) },
                _outputRoot,
                "scalar-proof.assets"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Count.EqualTo(3));
            Assert.That(Directory.Exists(_outputRoot), Is.False);
        }

        [Test]
        public void BuildStagesOneManifestWithDishAndNeverBundlesAuthoringAsset()
        {
            Dish dish = CreateLinkedDish("Linked Dish.asset", -1950922446, -201067776);

            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { AssetDatabase.GetAssetPath(dish) }, _outputRoot, "dish-link.assets"));

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            Assert.That(AssetDatabase.IsValidFolder(GdoBundleBuilder.StagingRoot), Is.False);
            AssetBundle bundle = AssetBundle.LoadFromFile(result.BundlePath);
            try
            {
                Assert.That(bundle.LoadAllAssets<Dish>(), Has.Length.EqualTo(1));
                TextAsset[] manifests = bundle.LoadAllAssets<TextAsset>()
                    .Where(asset => asset.name == Path.GetFileNameWithoutExtension(GdoOverlayManifest.AssetName)).ToArray();
                Assert.That(manifests, Has.Length.EqualTo(1));
                Assert.That(manifests[0].text, Does.Contain("\"field\":\"MinimumIngredients\""));
                Assert.That(manifests[0].text, Does.Contain("\"schemaVersion\": 3"));
                Assert.That(manifests[0].text, Does.Contain("\"globalFingerprint\": \"" + GdoDescriptorCatalog.GlobalFingerprint + "\""));
                Assert.That(manifests[0].text, Does.Contain("\"descriptorFingerprint\": \"" + GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish").DescriptorFingerprint + "\""));
                Assert.That(bundle.LoadAllAssets<GdoOverlayAuthoringAsset>(), Is.Empty);
            }
            finally
            {
                bundle.Unload(true);
            }
        }

        [Test]
        public void BuildRejectsDishWithoutCompanionBeforeCreatingOutput()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            dish.ID = -1950922446;
            const string path = AssetRoot + "/Unlinked Dish.asset";
            AssetDatabase.CreateAsset(dish, path);

            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { path }, _outputRoot, "dish-link.assets"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Some.Property("Message").Contains("companion").IgnoreCase);
            Assert.That(Directory.Exists(_outputRoot), Is.False);
        }

        [Test]
        public void ScalarBuildContainsNoLinkManifest()
        {
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath }, _outputRoot, "scalar-proof.assets"));
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            AssetBundle bundle = AssetBundle.LoadFromFile(result.BundlePath);
            try
            {
                Assert.That(bundle.LoadAllAssets<TextAsset>()
                    .Where(asset => asset.name == Path.GetFileNameWithoutExtension(GdoOverlayManifest.AssetName)), Is.Empty);
            }
            finally
            {
                bundle.Unload(true);
            }
        }

        [Test]
        public void LinkedDishBundleIsByteIdenticalAcrossCleanOutputDirectories()
        {
            Dish dish = CreateLinkedDish("Deterministic Dish.asset", -1950922446, -201067776);
            string secondOutputRoot = Path.Combine(Directory.GetParent(_outputRoot).FullName, "second-content");
            var firstRequest = new BundleBuildRequest(
                new[] { AssetDatabase.GetAssetPath(dish) }, _outputRoot, "dish-link.assets");
            var secondRequest = new BundleBuildRequest(
                new[] { AssetDatabase.GetAssetPath(dish) }, secondOutputRoot, "dish-link.assets");

            BundleBuildResult first = GdoBundleBuilder.Build(firstRequest);
            BundleBuildResult second = GdoBundleBuilder.Build(secondRequest);

            Assert.That(first.Succeeded, Is.True, string.Join("\n", first.Issues));
            Assert.That(second.Succeeded, Is.True, string.Join("\n", second.Issues));
            Assert.That(Sha256(second.BundlePath), Is.EqualTo(Sha256(first.BundlePath)));
            Assert.That(AssetDatabase.IsValidFolder(GdoBundleBuilder.StagingRoot), Is.False);
        }

        private Dish CreateLinkedDish(string name, int id, params int[] itemIds)
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            dish.name = Path.GetFileNameWithoutExtension(name);
            dish.ID = id;
            AssetDatabase.CreateAsset(dish, AssetRoot + "/" + name);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(dish);
            companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(itemIds.Select(itemId =>
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, itemId, "KitchenData.Item")))));
            AddRequiredReconstructedFields(companion);
            EditorUtility.SetDirty(companion);
            AssetDatabase.SaveAssets();
            _companionPaths.Add(AssetDatabase.GetAssetPath(companion));
            return dish;
        }

        private static void AddRequiredReconstructedFields(GdoOverlayAuthoringAsset companion)
        {
            companion.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "Test Dish") }));
            companion.SetField("KitchenData.Dish", "IngredientsUnlocks", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            companion.SetField("KitchenData.Dish", "ExtraOrderUnlocks", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            companion.SetField("KitchenData.Dish", "RequiredProcesses", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            companion.SetField("KitchenData.Dish", "BlockProviders", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            companion.SetField("KitchenData.Dish", "BeneficialIngredients", GdoOverlayValue.Set(new GdoOverlayValue[0]));
        }

        private static GameDifficultySettings Create(string name, int id)
        {
            var asset = ScriptableObject.CreateInstance<GameDifficultySettings>();
            asset.ID = id;
            string path = AssetRoot + "/" + name;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string Sha256(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }
}
