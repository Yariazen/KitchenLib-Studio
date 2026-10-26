using System;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class SnapshotDeploymentTests
    {
        [Test]
        public void InstalledSnapshotCatalogueIsByteDeterministicAndContainsNoPayload()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run deployment oracle.");
            var reader = new AssetsToolsSnapshotCatalogReader();
            byte[] first = VanillaAssetCatalogFile.Serialize(reader.Scan(root, null, CancellationToken.None).Data);
            byte[] second = VanillaAssetCatalogFile.Serialize(reader.Scan(root, null, CancellationToken.None).Data);
            string json = Encoding.UTF8.GetString(first);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(json, Does.Not.Contain(root).And.Not.Contain("vertexBuffer").And.Not.Contain("pixelData"));
        }

        [Test]
        public void SnapshotCacheAndDecodedPayloadStayOutsideAssets()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string cachePng = Path.GetFullPath(Path.Combine(projectRoot,
                new SnapshotCache().PngPath(new string('a', 64))));
            Assert.That(cachePng.StartsWith(Path.Combine(projectRoot, "Library") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(Path.Combine(Application.dataPath, "OfflineSnapshotPoc")), Is.False);
            Assert.That(Directory.GetFiles(Application.dataPath, "*.obj", SearchOption.AllDirectories), Is.Empty);
        }
    }
}
