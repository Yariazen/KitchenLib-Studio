using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class VanillaAssetCatalogTests
    {
        [TestCase("plateup", VanillaAssetKind.GameObject, "Water", "plateup/game-object/water")]
        [TestCase("plateup", VanillaAssetKind.Material, "Water (Glass)", "plateup/material/water-glass")]
        [TestCase("plateup", VanillaAssetKind.Mesh, "cup", "plateup/mesh/cup")]
        public void CanonicalKeysAreHumanReadable(string provider, VanillaAssetKind kind, string name, string expected)
        {
            Assert.That(VanillaAssetKey.Generate(provider, kind, name, new string[0], 10), Is.EqualTo(expected));
        }

        [Test]
        public void DuplicateNamesUseQualifierThenPathIdFallback()
        {
            Assert.That(VanillaAssetKey.Generate("plateup", VanillaAssetKind.Material, "Plate", new[] { "Kitchen" }, 42),
                Is.EqualTo("plateup/material/plate/kitchen"));
            Assert.That(VanillaAssetKey.Generate("plateup", VanillaAssetKind.Material, "", new string[0], 42),
                Is.EqualTo("plateup/material/unnamed-2a"));
        }

        [Test]
        public void SerializeIsDeterministicAndContainsNoAbsolutePathOrPayload()
        {
            VanillaAssetCatalogData data = Fixture();
            byte[] first = VanillaAssetCatalogFile.Serialize(data);
            byte[] second = VanillaAssetCatalogFile.Serialize(data);
            string json = System.Text.Encoding.UTF8.GetString(first);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(json, Does.EndWith("\n"));
            Assert.That(json, Does.Not.Contain("S:\\").And.Not.Contain("vertexBuffer").And.Not.Contain("pixelData"));
        }

        [Test]
        public void SerializeRecomputesFingerprintAfterSortingSources()
        {
            VanillaAssetCatalogData data = Fixture();
            data.sources = new[]
            {
                Source("z.assets", 'C'),
                Source("a.assets", 'B')
            };
            data.sourceFingerprint = PlateUpSourceFingerprint.ComputeStrings(
                data.sources.Select(source => source.sha256));

            string path = WriteTemporary(VanillaAssetCatalogFile.Serialize(data));
            try
            {
                VanillaAssetCatalogData restored = VanillaAssetCatalogFile.Read(path);
                Assert.That(restored.sources.Select(source => source.relativePath),
                    Is.EqualTo(new[] { "a.assets", "z.assets" }));
                Assert.That(restored.sourceFingerprint, Is.EqualTo(
                    PlateUpSourceFingerprint.ComputeStrings(restored.sources.Select(source => source.sha256))));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ReadNormalizesLegacyOrderDependentFingerprint()
        {
            VanillaAssetCatalogData data = Fixture();
            data.sources = new[]
            {
                Source("a.assets", 'B'),
                Source("z.assets", 'C')
            };
            data.sourceFingerprint = PlateUpSourceFingerprint.ComputeStrings(
                data.sources.Reverse().Select(source => source.sha256));
            string path = WriteTemporary(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true)));
            try
            {
                VanillaAssetCatalogData restored = VanillaAssetCatalogFile.Read(path);
                Assert.That(restored.sourceFingerprint, Is.EqualTo(
                    PlateUpSourceFingerprint.ComputeStrings(restored.sources.Select(source => source.sha256))));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ValidationRejectsDuplicateKeysAndUnresolvedDependencies()
        {
            VanillaAssetCatalogData duplicate = Fixture();
            duplicate.records = new[] { duplicate.records[0], duplicate.records[0] };
            Assert.Throws<InvalidDataException>(() => VanillaAssetCatalogFile.Serialize(duplicate));

            VanillaAssetCatalogData unresolved = Fixture();
            unresolved.edges = new[] { new VanillaAssetEdgeData { sourceKey = unresolved.records[0].key,
                relation = "mesh", targetKey = "plateup/mesh/missing", slot = "0" } };
            Assert.Throws<InvalidDataException>(() => VanillaAssetCatalogFile.Serialize(unresolved));
        }

        [Test]
        public void FailedAtomicReplacementPreservesPreviousBytes()
        {
            string path = Path.Combine(Path.GetTempPath(), "asset-catalog-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "previous");
            try
            {
                Assert.Throws<IOException>(() => VanillaAssetCatalogFile.PublishAtomic(path,
                    VanillaAssetCatalogFile.Serialize(Fixture()), (_, __) => throw new IOException("injected")));
                Assert.That(File.ReadAllText(path), Is.EqualTo("previous"));
            }
            finally { File.Delete(path); }
        }

        internal static VanillaAssetCatalogData FixtureForRepository()
        {
            VanillaAssetCatalogData data = Fixture();
            data.records = data.records.Concat(new[] { new VanillaAssetRecordData
            {
                key = "plateup/transform/water", displayName = "Water Transform", kind = "transform", structural = true,
                source = new VanillaObjectIdentityData { relativePath = "sharedassets1.assets", pathId = 4245, classId = 4, className = "Transform" },
                metadata = new VanillaAssetMetadataData[0]
            }}).ToArray();
            data.edges = new[] { new VanillaAssetEdgeData { sourceKey = data.records[0].key, relation = "component", slot = "0", targetKey = data.records[1].key } };
            return data;
        }

        private static VanillaAssetSourceData Source(string path, char hash) =>
            new VanillaAssetSourceData { relativePath = path, sha256 = new string(hash, 64),
                containerKind = "assets", unityVersion = "2020.3.48f1" };

        private static string WriteTemporary(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(), "asset-catalog-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static VanillaAssetCatalogData Fixture() => new VanillaAssetCatalogData
        {
            schemaVersion = 2, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
            importerVersion = "1", unityVersion = "2020.3.48f1", sourceFingerprint = new string('A', 64),
            sources = new[] { new VanillaAssetSourceData { relativePath = "sharedassets1.assets", sha256 = new string('B', 64), containerKind = "assets", unityVersion = "2020.3.48f1" } },
            records = new[] { new VanillaAssetRecordData { key = "plateup/game-object/water", displayName = "Water", kind = "gameObject",
                source = new VanillaObjectIdentityData { relativePath = "sharedassets1.assets", pathId = 4244, classId = 1, className = "GameObject" },
                metadata = new VanillaAssetMetadataData[0] } },
            edges = new VanillaAssetEdgeData[0], diagnostics = new VanillaAssetDiagnosticData[0]
        };
    }
}
