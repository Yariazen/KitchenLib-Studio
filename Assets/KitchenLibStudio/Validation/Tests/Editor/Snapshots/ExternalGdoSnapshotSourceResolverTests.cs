using System;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ExternalGdoSnapshotSourceResolverTests
    {
        [Test]
        public void ResolvePrefersIconThenDisplayThenPrefab()
        {
            VanillaAssetCatalogData catalog = Catalog(42,
                Edge("Prefab", "plateup/game-object/world"),
                Edge("DisplayPrefab", "plateup/game-object/display"),
                Edge("IconPrefab", "plateup/game-object/icon"));

            ExternalGdoSnapshotSourceResolution result =
                ExternalGdoSnapshotSourceResolver.Resolve(catalog, 42);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.GameObjectKey, Is.EqualTo("plateup/game-object/icon"));
            Assert.That(result.Slot, Is.EqualTo("IconPrefab"));
        }

        [Test]
        public void ResolveFallsBackWhenPreferredTargetIsMissing()
        {
            VanillaAssetCatalogData catalog = Catalog(42,
                Edge("IconPrefab", "plateup/game-object/missing"),
                Edge("DisplayPrefab", "plateup/game-object/display"),
                Edge("Prefab", "plateup/game-object/world"));

            ExternalGdoSnapshotSourceResolution result =
                ExternalGdoSnapshotSourceResolver.Resolve(catalog, 42);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.GameObjectKey, Is.EqualTo("plateup/game-object/display"));
            Assert.That(result.Slot, Is.EqualTo("DisplayPrefab"));
        }

        [Test]
        public void ResolveRejectsDuplicateGdoIds()
        {
            VanillaAssetCatalogData catalog = Catalog(42, Edge("Prefab", "plateup/game-object/world"));
            Array.Resize(ref catalog.records, catalog.records.Length + 1);
            catalog.records[catalog.records.Length - 1] = Record("plateup/gdo/duplicate", "gdo", 42);

            ExternalGdoSnapshotSourceResolution result =
                ExternalGdoSnapshotSourceResolver.Resolve(catalog, 42);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostic, Does.Contain("multiple"));
        }

        [Test]
        public void ResolveRejectsZeroId()
        {
            ExternalGdoSnapshotSourceResolution result =
                ExternalGdoSnapshotSourceResolver.Resolve(Catalog(42), 0);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostic, Is.Not.Empty);
        }

        [Test]
        public void ResolveReportsNoSupportedPrefab()
        {
            VanillaAssetCatalogData catalog = Catalog(42,
                new VanillaAssetEdgeData
                {
                    sourceKey = "plateup/gdo/source",
                    relation = "gameObject",
                    slot = "Visual",
                    targetKey = "plateup/game-object/world"
                });

            ExternalGdoSnapshotSourceResolution result =
                ExternalGdoSnapshotSourceResolver.Resolve(catalog, 42);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostic, Does.Contain("supported"));
        }

        [Test]
        public void CompactIndexCarriesResolvedGameObjectAndScopedFingerprint()
        {
            VanillaAssetCatalogData catalog = Catalog(42, Edge("IconPrefab", "plateup/game-object/icon"));
            catalog.sources = new[] { new VanillaAssetSourceData { relativePath = "sharedassets1.assets", sha256 = "source-hash" } };
            catalog.sourceFingerprint = "aggregate";
            foreach (VanillaAssetRecordData record in catalog.records)
                record.source = new VanillaObjectIdentityData { relativePath = "sharedassets1.assets", pathId = record.kind == "gameObject" ? 7 : 1 };

            ExternalGdoSnapshotIndexData index = ExternalGdoSnapshotIndexFile.Build(catalog);

            Assert.That(index.entries, Has.Length.EqualTo(1));
            Assert.That(index.entries[0].gdoId, Is.EqualTo(42));
            Assert.That(index.entries[0].gameObjectKey, Is.EqualTo("plateup/game-object/icon"));
            Assert.That(index.entries[0].sourceFingerprint, Does.StartWith("source-hash"));
        }

        private static VanillaAssetCatalogData Catalog(int gdoId, params VanillaAssetEdgeData[] edges)
        {
            return new VanillaAssetCatalogData
            {
                records = new[]
                {
                    Record("plateup/gdo/source", "gdo", gdoId),
                    Record("plateup/game-object/icon", "gameObject", 0),
                    Record("plateup/game-object/display", "gameObject", 0),
                    Record("plateup/game-object/world", "gameObject", 0)
                },
                edges = edges ?? Array.Empty<VanillaAssetEdgeData>()
            };
        }

        private static VanillaAssetRecordData Record(string key, string kind, int gdoId)
        {
            return new VanillaAssetRecordData
            {
                key = key,
                displayName = key,
                kind = kind,
                gdoId = gdoId,
                source = new VanillaObjectIdentityData()
            };
        }

        private static VanillaAssetEdgeData Edge(string slot, string targetKey)
        {
            return new VanillaAssetEdgeData
            {
                sourceKey = "plateup/gdo/source",
                relation = "gameObject",
                slot = slot,
                targetKey = targetKey
            };
        }
    }
}
