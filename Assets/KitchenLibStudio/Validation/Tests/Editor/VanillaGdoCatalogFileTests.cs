using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class VanillaGdoCatalogFileTests
    {
        [Test]
        public void SerializeIsDeterministicUtf8WithoutBomOrMachinePathAndEndsWithNewline()
        {
            VanillaGdoCatalogData data = Sample();
            byte[] first = VanillaGdoCatalogFile.Serialize(data);
            byte[] second = VanillaGdoCatalogFile.Serialize(data);
            string json = Encoding.UTF8.GetString(first);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Take(3).ToArray(), Is.Not.EqualTo(Encoding.UTF8.GetPreamble()));
            Assert.That(json.EndsWith("\n", StringComparison.Ordinal), Is.True);
            Assert.That(json, Does.Not.Contain("C:\\"));
            Assert.That(json, Does.Not.Contain("generatedAt"));
        }

        [Test]
        public void PublishAtomicPreservesPreviousBytesWhenValidationOrReplacementFails()
        {
            string directory = Path.Combine(Path.GetTempPath(), "KitchenLibStudio-CatalogFile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, "catalog.json");
            byte[] previous = Encoding.UTF8.GetBytes("previous");
            File.WriteAllBytes(destination, previous);
            try
            {
                Assert.Throws<InvalidDataException>(() => VanillaGdoCatalogFile.PublishAtomic(destination, Encoding.UTF8.GetBytes("not json")));
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(previous));

                Assert.Throws<IOException>(() => VanillaGdoCatalogFile.PublishAtomic(destination, VanillaGdoCatalogFile.Serialize(Sample()), (source, target) => throw new IOException("injected")));
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(previous));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ReadReturnsValidatedCatalogDataFromDisk()
        {
            string path = Path.Combine(Path.GetTempPath(), "KitchenLibStudio-CatalogRead-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllBytes(path, VanillaGdoCatalogFile.Serialize(Sample()));

                VanillaGdoCatalogData data = VanillaGdoCatalogFile.Read(path);

                Assert.That(data.providerId, Is.EqualTo("plateup"));
                Assert.That(data.steamAppId, Is.EqualTo(1599600));
                Assert.That(data.entries, Is.Empty);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void RoundTripPreservesProviderQualifiedRelationships()
        {
            VanillaGdoCatalogData data = Sample();
            data.relationships = new[]
            {
                new ExternalGdoRelationshipData
                {
                    source = new ExternalGdoIdentityData { providerKey = "plateup", runtimeId = 1, runtimeType = "KitchenData.Item" },
                    target = new ExternalGdoIdentityData { providerKey = "plateup", runtimeId = 2, runtimeType = "KitchenData.Item" },
                    fieldPath = "KitchenData.Item.DirtiesTo", label = "Dirties To"
                }
            };

            string json = Encoding.UTF8.GetString(VanillaGdoCatalogFile.Serialize(data));

            Assert.That(json, Does.Contain("KitchenData.Item.DirtiesTo"));
        }

        private static VanillaGdoCatalogData Sample()
        {
            return new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion,
                providerId = "plateup",
                providerName = "PlateUp",
                steamAppId = 1599600,
                importerVersion = "1",
                unityVersion = "2020.3.48f1",
                sourceAssemblyName = "Kitchen.GameData",
                sourceAssemblySha256 = new string('A', 64),
                schemaFingerprint = "schema",
                assets = new VanillaGdoCatalogAssetData[0],
                entries = new VanillaGdoCatalogEntryData[0],
                relationships = new ExternalGdoRelationshipData[0],
                diagnostics = new VanillaGdoCatalogDiagnosticData[0]
            };
        }
    }
}
