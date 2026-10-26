using System;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoLinkAuthoringRepositoryTests
    {
        private const string AssetRoot = "Assets/__KitchenLibStudioLinkRepositoryTests";
        private string _companionPath;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(AssetRoot);
            AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioLinkRepositoryTests");
            _companionPath = null;
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(AssetRoot);
            if (!string.IsNullOrEmpty(_companionPath))
            {
                AssetDatabase.DeleteAsset(_companionPath);
            }
        }

        [Test]
        public void GetOrCreateReturnsOneStableCompanionPerTargetAfterRefresh()
        {
            Dish target = CreateDish("Target.asset", -101);

            GdoLinkAuthoringAsset first = GdoLinkAuthoringRepository.GetOrCreate(target);
            _companionPath = AssetDatabase.GetAssetPath(first);
            first.SetMinimumIngredientIds(new[] { 30, 20, 30 });
            EditorUtility.SetDirty(first);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            GdoLinkAuthoringAsset second = GdoLinkAuthoringRepository.GetOrCreate(target);

            Assert.That(second, Is.SameAs(GdoLinkAuthoringRepository.Find(target)));
            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(_companionPath));
            Assert.That(second.MinimumIngredientIds, Is.EqualTo(new[] { 30, 20, 30 }));
        }

        [Test]
        public void SetMinimumIngredientIdsRejectsZeroAndPreservesExistingValues()
        {
            Dish target = CreateDish("Target.asset", -102);
            GdoLinkAuthoringAsset companion = GdoLinkAuthoringRepository.GetOrCreate(target);
            _companionPath = AssetDatabase.GetAssetPath(companion);
            companion.SetMinimumIngredientIds(new[] { 1, 2 });

            Assert.Throws<ArgumentException>(() => companion.SetMinimumIngredientIds(new[] { 3, 0, 4 }));
            Assert.That(companion.MinimumIngredientIds, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void FindForTargetsExcludesCompanionsOutsideBuildSet()
        {
            Dish included = CreateDish("Included.asset", -103);
            Dish excluded = CreateDish("Excluded.asset", -104);
            GdoLinkAuthoringAsset includedCompanion = GdoLinkAuthoringRepository.GetOrCreate(included);
            GdoLinkAuthoringAsset excludedCompanion = GdoLinkAuthoringRepository.GetOrCreate(excluded);
            string includedPath = AssetDatabase.GetAssetPath(includedCompanion);
            string excludedPath = AssetDatabase.GetAssetPath(excludedCompanion);

            GdoLinkAuthoringAsset[] found = GdoLinkAuthoringRepository.FindForTargets(new GameDataObject[] { included }).ToArray();

            Assert.That(found, Is.EqualTo(new[] { includedCompanion }));
            AssetDatabase.DeleteAsset(includedPath);
            AssetDatabase.DeleteAsset(excludedPath);
        }

        private static Dish CreateDish(string fileName, int id)
        {
            var dish = ScriptableObject.CreateInstance<Dish>();
            dish.ID = id;
            AssetDatabase.CreateAsset(dish, AssetRoot + "/" + fileName);
            return dish;
        }
    }
}
