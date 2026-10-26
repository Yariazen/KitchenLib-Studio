using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class WorkshopModDiscoveryTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "workshop-mods-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void DiscoversIngredientLibStylePayloadWithoutLoadingAssembly()
        {
            string item = Directory.CreateDirectory(Path.Combine(_root, "2913877103")).FullName;
            File.WriteAllBytes(Path.Combine(item, "IngredientLib.dll"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(item, "IngredientLib.assets"), new byte[] { 4, 5 });

            WorkshopModRecord record = new WorkshopModDiscovery().Discover(Installation()).Single();

            Assert.That(record.WorkshopId, Is.EqualTo("2913877103"));
            Assert.That(record.ProviderKey, Is.EqualTo("workshop:2913877103"));
            Assert.That(record.Assemblies.Single().RelativePath, Is.EqualTo("IngredientLib.dll"));
            Assert.That(record.AssetBundles.Single().RelativePath, Is.EqualTo("IngredientLib.assets"));
            Assert.That(record.Assemblies.Single().Sha256, Has.Length.EqualTo(64));
        }

        [Test]
        public void SortsMultipleDllsAndIgnoresNestedAndUnrelatedFiles()
        {
            string item = Directory.CreateDirectory(Path.Combine(_root, "123")).FullName;
            File.WriteAllBytes(Path.Combine(item, "Z.dll"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(item, "A.dll"), new byte[] { 2 });
            File.WriteAllText(Path.Combine(item, "notes.txt"), "ignored");
            string nested = Directory.CreateDirectory(Path.Combine(item, "nested")).FullName;
            File.WriteAllBytes(Path.Combine(nested, "Nested.dll"), new byte[] { 3 });
            Directory.CreateDirectory(Path.Combine(_root, "not-an-id"));

            WorkshopModRecord record = new WorkshopModDiscovery().Discover(Installation()).Single();

            Assert.That(record.Assemblies.Select(file => file.RelativePath), Is.EqualTo(new[] { "A.dll", "Z.dll" }));
        }

        [Test]
        public void UnreadableOrMissingWorkshopRootReturnsDiagnosticsWithoutThrowing()
        {
            PlateUpInstallation missing = new PlateUpInstallation("game", null, null,
                Path.Combine(_root, "missing"), null, "build");

            WorkshopModDiscoveryResult result = new WorkshopModDiscovery().Scan(missing);

            Assert.That(result.Mods, Is.Empty);
            Assert.That(result.Diagnostics, Has.Some.Contains("does not exist"));
        }

        private PlateUpInstallation Installation()
            => new PlateUpInstallation("game", null, null, _root, null, "build");
    }
}
