using System.IO;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class ScalarProofAssetTests
    {
        private byte[] _assetBackup;
        private byte[] _metaBackup;

        [SetUp]
        public void SetUp()
        {
            _assetBackup = ReadIfPresent(ScalarProofAsset.AssetPath);
            _metaBackup = ReadIfPresent(ScalarProofAsset.AssetPath + ".meta");
            AssetDatabase.DeleteAsset(ScalarProofAsset.AssetPath);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ScalarProofAsset.AssetPath);
            if (_assetBackup != null && _metaBackup != null)
            {
                File.WriteAllBytes(ScalarProofAsset.AssetPath, _assetBackup);
                File.WriteAllBytes(ScalarProofAsset.AssetPath + ".meta", _metaBackup);
                AssetDatabase.ImportAsset(ScalarProofAsset.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void CreateOrUpdateCreatesExactInactiveScalarFixture()
        {
            GameDifficultySettings asset = ScalarProofAsset.CreateOrUpdate();

            Assert.That(asset.name, Is.EqualTo("KLStudio Scalar Proof"));
            Assert.That(asset.ID, Is.EqualTo(-1950922447));
            Assert.That(asset.IsActive, Is.False);
            Assert.That(asset.CustomersPerHourBase, Is.EqualTo(1f));
            Assert.That(asset.CustomersPerHourIncreasePerDay, Is.EqualTo(0.2f));
            Assert.That(asset.CustomerSideChance, Is.EqualTo(1f));
            Assert.That(asset.QueuePatienceTime, Is.EqualTo(100f));
            Assert.That(asset.QueuePatienceBoost, Is.EqualTo(10f));
            Assert.That(asset.CustomerStarterChance, Is.EqualTo(1f));
            Assert.That(asset.GroupDessertChance, Is.EqualTo(1f));
            Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(ScalarProofAsset.AssetPath));
            MonoScript script = MonoScript.FromScriptableObject(asset);
            Assert.That(script, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(script), Does.EndWith("/GameDifficultySettings.cs"));
        }

        [Test]
        public void CreateOrUpdateUpdatesExistingAssetWithoutChangingGuid()
        {
            GameDifficultySettings first = ScalarProofAsset.CreateOrUpdate();
            string guid = AssetDatabase.AssetPathToGUID(ScalarProofAsset.AssetPath);
            first.ID = 7;
            first.IsActive = true;
            EditorUtility.SetDirty(first);
            AssetDatabase.SaveAssets();

            GameDifficultySettings second = ScalarProofAsset.CreateOrUpdate();

            Assert.That(second, Is.SameAs(first));
            Assert.That(AssetDatabase.AssetPathToGUID(ScalarProofAsset.AssetPath), Is.EqualTo(guid));
            Assert.That(second.ID, Is.EqualTo(-1950922447));
            Assert.That(second.IsActive, Is.False);
        }

        private static byte[] ReadIfPresent(string path)
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
