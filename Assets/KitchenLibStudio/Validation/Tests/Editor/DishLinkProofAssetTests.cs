using System.IO;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class DishLinkProofAssetTests
    {
        private byte[] _assetBackup;
        private byte[] _metaBackup;
        private string _companionPath;

        [SetUp]
        public void SetUp()
        {
            _assetBackup = ReadIfPresent(DishLinkProofAsset.AssetPath);
            _metaBackup = ReadIfPresent(DishLinkProofAsset.AssetPath + ".meta");
            AssetDatabase.DeleteAsset(DishLinkProofAsset.AssetPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_companionPath))
            {
                AssetDatabase.DeleteAsset(_companionPath);
            }

            AssetDatabase.DeleteAsset(DishLinkProofAsset.AssetPath);
            if (_assetBackup != null && _metaBackup != null)
            {
                File.WriteAllBytes(DishLinkProofAsset.AssetPath, _assetBackup);
                File.WriteAllBytes(DishLinkProofAsset.AssetPath + ".meta", _metaBackup);
                AssetDatabase.ImportAsset(DishLinkProofAsset.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void CreateOrUpdateCreatesRealDishAndSeparateOnionToken()
        {
            Dish dish = DishLinkProofAsset.CreateOrUpdate();
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(dish);
            _companionPath = AssetDatabase.GetAssetPath(companion);

            Assert.That(dish.name, Is.EqualTo("KLStudio Link Proof Dish"));
            Assert.That(dish.ID, Is.EqualTo(-1950922446));
            Assert.That(AssetDatabase.GetAssetPath(dish), Is.EqualTo(DishLinkProofAsset.AssetPath));
            Assert.That(companion, Is.Not.Null);
            Assert.That(companion.Target, Is.SameAs(dish));
            Assert.That(companion.Fields.Single().Value.Values.Single().ReferenceValue.Id, Is.EqualTo(-201067776));
            Assert.That(_companionPath, Is.Not.EqualTo(DishLinkProofAsset.AssetPath));
        }

        [Test]
        public void RuntimeBuildAssetsContainDishButNeverCompanion()
        {
            Dish dish = DishLinkProofAsset.CreateOrUpdate();
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(dish);
            _companionPath = AssetDatabase.GetAssetPath(companion);

            string[] runtimePaths = DishLinkProofAsset.RuntimeAssetPaths.ToArray();

            Assert.That(runtimePaths, Is.EqualTo(new[] { DishLinkProofAsset.AssetPath }));
            Assert.That(runtimePaths, Does.Not.Contain(_companionPath));
        }

        private static byte[] ReadIfPresent(string path)
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
