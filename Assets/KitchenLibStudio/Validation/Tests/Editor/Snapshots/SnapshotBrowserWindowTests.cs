using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class SnapshotBrowserWindowTests
    {
        [Test]
        public void PickerSearchHidesStructuralRecords()
        {
            string path = Path.Combine(Path.GetTempPath(), "snapshot-picker-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                VanillaAssetCatalogData data = VanillaAssetCatalogTests.FixtureForRepository();
                VanillaAssetCatalogFile.PublishAtomic(path, VanillaAssetCatalogFile.Serialize(data));
                var repository = new VanillaAssetCatalogRepository(path); Assert.That(repository.Reload(), Is.True);
                var picker = new VanillaGameObjectPicker(repository, null);
                Assert.That(picker.Search("water").Select(value => value.Key), Is.EqualTo(new[] { "plateup/game-object/water" }));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void BrowserExposesRequiredControlsAndMetadataRemainsBrowsable()
        {
            var root = SnapshotBrowserWindow.BuildContentForTests();
            Assert.That(root.Q<TextField>("snapshot-search"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("snapshot-local-object"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("snapshot-custom-gdo"), Is.Not.Null);
            Assert.That(root.Q<PopupField<string>>("snapshot-prefab-field"), Is.Not.Null);
            Assert.That(root.Q<IntegerField>("snapshot-resolution"), Is.Not.Null);
            Assert.That(root.Q<Button>("snapshot-generate"), Is.Not.Null);
            Assert.That(root.Q<HelpBox>("snapshot-diagnostics"), Is.Not.Null);
        }

        [Test]
        public void VanillaBrowserFindsStableCachedPngBeforeRendering()
        {
            string root = Path.Combine(Path.GetTempPath(), "snapshot-browser-cache-" + Guid.NewGuid().ToString("N"));
            var cache = new SnapshotCache(root);
            VanillaAssetCatalogData catalog = VanillaAssetCatalogTests.FixtureForRepository();
            string key = ExternalGdoSnapshotCacheIdentity.BuildKey(catalog,
                "plateup/game-object/water", new SnapshotRenderOptions(128, 128));
            byte[] png = { 137, 80, 78, 71, 13, 10, 26, 10, 7 };
            try
            {
                cache.Put(key, png, new SnapshotCacheMetadata { width = 128, height = 128 });

                SnapshotCacheEntry entry = SnapshotBrowserWindow.TryGetCachedVanilla(cache, catalog,
                    "plateup/game-object/water", 128);

                Assert.That(entry, Is.Not.Null);
                Assert.That(entry.PngBytes, Is.EqualTo(png));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
