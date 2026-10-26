using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class SnapshotCatalogReaderTests
    {
        [Test]
        public void InstalledBuildCataloguesWaterAndItsRenderableDependencies()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the snapshot catalogue oracle.");

            SnapshotCatalogScanResult result = new AssetsToolsSnapshotCatalogReader().Scan(root, null, CancellationToken.None);
            VanillaAssetRecordData waterObject = result.Data.records.Single(record =>
                record.source.relativePath == "sharedassets1.assets" && record.source.pathId == 4244);
            VanillaAssetRecordData waterGdo = result.Data.records.Single(record =>
                record.source.relativePath == "sharedassets1.assets" && record.source.pathId == 73340);

            Assert.That(waterObject.displayName, Is.EqualTo("Water"));
            Assert.That(waterObject.kind, Is.EqualTo("gameObject"));
            Assert.That(waterObject.hierarchyRoot, Is.True);
            Assert.That(waterGdo.gdoId, Is.EqualTo(1657174953));
            Assert.That(result.Data.edges.Any(edge => edge.sourceKey == waterGdo.key &&
                edge.targetKey == waterObject.key && edge.relation == "gameObject"), Is.True);
            Assert.That(result.Data.records.Count(record => record.kind == "gameObject"), Is.GreaterThan(22000));
            Assert.That(result.Data.records.Any(record => record.kind == "mesh" && record.displayName == "cup"), Is.True);
            Assert.That(result.Data.records.Any(record => record.kind == "material" && record.displayName == "Plate"), Is.True);
            Assert.That(result.HasFatalDiagnostics, Is.False);
        }
    }
}
