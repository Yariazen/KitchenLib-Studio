using System;
using System.IO;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class BundleOutputVerifierTests
    {
        private const string AssetRoot = "Assets/__KitchenLibStudioVerifierTests";
        private string _packageRoot;
        private string _contentRoot;
        private string _bundlePath;
        private string _companionPath;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(AssetRoot);
            AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioVerifierTests");
            _packageRoot = Path.Combine(Path.GetTempPath(), "KitchenLib Studio Verifier Tests", Guid.NewGuid().ToString("N"));
            _contentRoot = Path.Combine(_packageRoot, "content");
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath }, _contentRoot, "scalar-proof.assets"));
            Assert.That(result.Succeeded, Is.True, "Test fixture bundle must build.");
            _bundlePath = result.BundlePath;
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_companionPath))
            {
                AssetDatabase.DeleteAsset(_companionPath);
            }
            AssetDatabase.DeleteAsset(AssetRoot);
            if (Directory.Exists(_packageRoot))
            {
                Directory.Delete(_packageRoot, true);
            }
        }

        [Test]
        public void VerifyRejectsBundleWithMoreThanOneGdo()
        {
            var second = ScriptableObject.CreateInstance<GameDifficultySettings>();
            second.name = "Second GDO";
            second.ID = 9001;
            const string secondPath = AssetRoot + "/Second.asset";
            AssetDatabase.CreateAsset(second, secondPath);
            string path = BuildBundle("two-gdos.assets", ScalarProofAsset.AssetPath, secondPath);

            var issues = BundleOutputVerifier.Verify(_packageRoot, path, ExpectedGdo.ScalarProof);

            Assert.That(issues.Select(issue => issue.Message), Has.Some.Contains("exactly one"));
            Assert.That(issues.Select(issue => issue.Message), Has.Some.Contains("2"));
        }

        [Test]
        public void BuildRejectsEditorAssemblySerializedType()
        {
            var payload = ScriptableObject.CreateInstance<EditorOnlyPayload>();
            payload.name = "Editor Payload";
            const string payloadPath = AssetRoot + "/Editor Payload.asset";
            AssetDatabase.CreateAsset(payload, payloadPath);

            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath, payloadPath },
                Path.Combine(_packageRoot, "rejected"),
                "editor-payload.assets"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Some.Property("AssetPath").EqualTo(payloadPath));
            Assert.That(result.Issues, Has.Some.Property("Message").Contains("GameDataObject"));
        }

        [Test]
        public void VerifyAcceptsExpectedScalarGdoIdentity()
        {
            Assert.That(BundleOutputVerifier.Verify(_packageRoot, _bundlePath, ExpectedGdo.ScalarProof), Is.Empty);
        }

        [Test]
        public void AuditAcceptsDataOnlyScalarPackageAndReportsZeroScriptsAndBinaries()
        {
            GdoPackageAuditReport report = GdoPackageAudit.Audit(
                _packageRoot, new[] { _bundlePath }, new string[0], new[] { "Kitchen.GameData" });

            Assert.That(report.HasFatalFindings, Is.False, report.ToJson());
            Assert.That(report.ManagedBinaryCount, Is.Zero);
            Assert.That(report.SerializedScriptCount, Is.Zero);
        }

        [TestCase("leak.dll")]
        [TestCase("leak.pdb")]
        public void AuditRejectsManagedBinaryAnywhereInPackage(string fileName)
        {
            string path = Path.Combine(_packageRoot, "nested", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

            GdoPackageAuditReport report = GdoPackageAudit.Audit(
                _packageRoot, new[] { _bundlePath }, new string[0], new[] { "Kitchen.GameData" });

            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("ManagedBinary"));
            Assert.That(report.ManagedBinaryCount, Is.EqualTo(1));
        }

        [Test]
        public void AuditRejectsSnapshotCatalogueAndCachedPngInPackage()
        {
            File.WriteAllText(Path.Combine(_packageRoot, "vanilla-snapshot-catalog.json"), "{}");
            string cache = Path.Combine(_packageRoot, "Snapshots"); Directory.CreateDirectory(cache);
            File.WriteAllBytes(Path.Combine(cache, "preview.png"), new byte[] { 1, 2, 3 });

            GdoPackageAuditReport report = GdoPackageAudit.Audit(
                _packageRoot, new[] { _bundlePath }, new string[0], new[] { "Kitchen.GameData" });

            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("SnapshotMetadata"));
            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("SnapshotCachePayload"));
        }

        [TestCase("Editor", "EditorAsset")]
        [TestCase("Generated", "GeneratedMirrorAsset")]
        public void AuditRejectsEditorAndGeneratedAssets(string folder, string category)
        {
            string assetFolder = AssetRoot + "/" + folder;
            AssetDatabase.CreateFolder(AssetRoot, folder);
            var payload = ScriptableObject.CreateInstance<GameDifficultySettings>();
            payload.ID = 78001;
            string payloadPath = assetFolder + "/Payload.asset";
            AssetDatabase.CreateAsset(payload, payloadPath);
            string bundle = BuildBundle(folder.ToLowerInvariant() + ".assets", payloadPath);

            GdoPackageAuditReport report = GdoPackageAudit.Audit(
                _packageRoot, new[] { bundle }, new string[0], new[] { "Kitchen.GameData" });

            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo(category), report.ToJson());
        }

        [Test]
        public void AuditRejectsUndeclaredAndMalformedSidecars()
        {
            var undeclared = new TextAsset("{}");
            var malformed = new TextAsset("not json");
            const string undeclaredPath = AssetRoot + "/undeclared.json";
            const string malformedPath = AssetRoot + "/kitchenlib-gdo-overlay.json";
            AssetDatabase.CreateAsset(undeclared, undeclaredPath);
            AssetDatabase.CreateAsset(malformed, malformedPath);
            string bundle = BuildBundle("sidecars.assets", ScalarProofAsset.AssetPath, undeclaredPath, malformedPath);

            GdoPackageAuditReport report = GdoPackageAudit.Audit(
                _packageRoot, new[] { bundle }, new[] { GdoOverlayManifest.AssetName }, new[] { "Kitchen.GameData" });

            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("UndeclaredSidecar"));
            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("MalformedManifest"));
        }

        [Test]
        public void AuditRejectsUnexpectedSerializedAssetAndDisallowedAssembly()
        {
            var material = new Material(Shader.Find("Standard"));
            const string materialPath = AssetRoot + "/Unexpected.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            const string prefabPath = AssetRoot + "/Allowed.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            string bundle = BuildBundle("unexpected.assets", ScalarProofAsset.AssetPath, prefabPath, materialPath);

            GdoPackageAuditReport report = GdoPackageAudit.Audit(
                _packageRoot, new[] { bundle }, new string[0], new[] { "Some.Other.Assembly" });

            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("UnexpectedSerializedAsset"));
            Assert.That(report.Findings, Has.Some.Property("Category").EqualTo("DisallowedRuntimeAssembly"));
        }

        [Test]
        public void VerifyAcceptsDishWithExactlyOneLinkManifest()
        {
            Dish dish = CreateDish("KLStudio Link Proof Dish.asset", -1950922446);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(dish);
            companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
            { GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, -201067776, "KitchenData.Item")) }));
            AddRequiredReconstructedFields(companion);
            EditorUtility.SetDirty(companion);
            AssetDatabase.SaveAssets();
            _companionPath = AssetDatabase.GetAssetPath(companion);
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { AssetDatabase.GetAssetPath(dish) },
                Path.Combine(_packageRoot, "linked"),
                "dish-link.assets"));
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));

            Assert.That(BundleOutputVerifier.Verify(_packageRoot, result.BundlePath, ExpectedGdo.DishLinkProof), Is.Empty);
        }

        [Test]
        public void VerifyRejectsDishBundleWithoutLinkManifest()
        {
            Dish dish = CreateDish("Unlinked Dish.asset", -1950922446);
            string rawBundle = BuildBundle("unlinked-dish.assets", AssetDatabase.GetAssetPath(dish));

            var issues = BundleOutputVerifier.Verify(_packageRoot, rawBundle, ExpectedGdo.DishLinkProof);

            Assert.That(issues, Has.Some.Property("Message").Contains("manifest").IgnoreCase);
        }

        [TestCase("leak.dll")]
        [TestCase("leak.pdb")]
        public void VerifyRejectsManagedBinaryAnywhereInPackage(string fileName)
        {
            string path = Path.Combine(_packageRoot, "nested", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

            var issues = BundleOutputVerifier.Verify(_packageRoot, _bundlePath, ExpectedGdo.ScalarProof);

            Assert.That(issues, Has.Some.Property("AssetPath").EqualTo(path));
            Assert.That(issues, Has.Some.Property("Message").Contains("managed binary").IgnoreCase);
        }

        [Test]
        public void VerifyRejectsWrongExpectedTypeName()
        {
            var expected = new ExpectedGdo("KitchenData.Dish", "Kitchen.GameData", "KLStudio Scalar Proof", -1950922447, false);

            var issues = BundleOutputVerifier.Verify(_packageRoot, _bundlePath, expected);

            Assert.That(issues, Has.Some.Property("Message").Contains("KitchenData.GameDifficultySettings"));
        }

        [Test]
        public void VerifyRejectsWrongExpectedId()
        {
            var expected = new ExpectedGdo("KitchenData.GameDifficultySettings", "Kitchen.GameData", "KLStudio Scalar Proof", 123, false);

            var issues = BundleOutputVerifier.Verify(_packageRoot, _bundlePath, expected);

            Assert.That(issues, Has.Some.Property("PropertyPath").EqualTo("ID"));
        }

        [Test]
        public void InstallCopiesOnlyVerifiedContentToExactModFolder()
        {
            string plateUpRoot = Path.Combine(Path.GetTempPath(), "Fake PlateUp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(plateUpRoot);
            try
            {
                string installed = ScalarProofInstaller.Install(_packageRoot, plateUpRoot);

                Assert.That(installed, Is.EqualTo(Path.Combine(plateUpRoot, "Mods", "KitchenLibStudioScalarProof")));
                Assert.That(File.Exists(Path.Combine(installed, "content", "scalar-proof.assets")), Is.True);
                Assert.That(Directory.GetFiles(installed, "*.dll", SearchOption.AllDirectories), Is.Empty);
            }
            finally
            {
                Directory.Delete(plateUpRoot, true);
            }
        }

        private string BuildBundle(string bundleName, params string[] assetPaths)
        {
            var build = new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assetPaths
            };
            BuildPipeline.BuildAssetBundles(
                _contentRoot,
                new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle,
                BuildTarget.StandaloneWindows64);
            return Path.Combine(_contentRoot, bundleName);
        }

        private static Dish CreateDish(string name, int id)
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            dish.name = Path.GetFileNameWithoutExtension(name);
            dish.ID = id;
            AssetDatabase.CreateAsset(dish, AssetRoot + "/" + name);
            return dish;
        }

        private static void AddRequiredReconstructedFields(GdoOverlayAuthoringAsset companion)
        {
            companion.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "Test Dish") }));
            foreach (string field in new[] { "IngredientsUnlocks", "ExtraOrderUnlocks", "RequiredProcesses", "BlockProviders", "BeneficialIngredients" })
                companion.SetField("KitchenData.Dish", field, GdoOverlayValue.Set(new GdoOverlayValue[0]));
        }
    }

    public sealed class EditorOnlyPayload : ScriptableObject
    {
        public int Value;
    }
}
