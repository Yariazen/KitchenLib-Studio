using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class AssetsToolsPlateUpAssetReaderTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "KitchenLibStudioAssetReaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ScanRejectsMissingManagedAssembly()
        {
            VanillaGdoScanResult result = new AssetsToolsPlateUpAssetReader(new StubBackend()).Scan(
                _root,
                null,
                CancellationToken.None);

            Assert.That(result.HasFatalDiagnostics, Is.True);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("missing-managed-assembly"));
        }

        [Test]
        public void ScanTreatsUnidentifiedScriptAsWarning()
        {
            CreateManagedAssemblyPlaceholder();
            var backend = new StubBackend
            {
                Objects = new[]
                {
                    PlateUpAssetObject.Unresolved("sharedassets0.assets", 41, "MonoScript reference is null")
                }
            };

            VanillaGdoScanResult result = new AssetsToolsPlateUpAssetReader(backend).Scan(
                _root,
                null,
                CancellationToken.None);

            Assert.That(result.HasFatalDiagnostics, Is.False);
            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(VanillaGdoDiagnosticSeverity.Warning));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("unresolved-script"));
        }

        [Test]
        public void ScanTreatsIdentifiedGdoParseFailureAsFatal()
        {
            CreateManagedAssemblyPlaceholder();
            var backend = new StubBackend
            {
                Objects = new[]
                {
                    PlateUpAssetObject.Failed(
                        "Kitchen.GameData",
                        "KitchenData.Item",
                        "sharedassets1.assets",
                        73340,
                        "ID field could not be read")
                }
            };

            VanillaGdoScanResult result = new AssetsToolsPlateUpAssetReader(backend).Scan(
                _root,
                null,
                CancellationToken.None);

            Assert.That(result.HasFatalDiagnostics, Is.True);
            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(VanillaGdoDiagnosticSeverity.Fatal));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("gdo-parse-failed"));
        }

        [Test]
        public void ScanResolvesOnlyKnownGdoPointerCandidatesAndOrdersThemDeterministically()
        {
            CreateManagedAssemblyPlaceholder();
            var backend = new StubBackend
            {
                Objects = new[]
                {
                    PlateUpAssetObject.Resolved("Kitchen.GameData", "KitchenData.Item", "Dough", 10,
                        "sharedassets1.assets", 100, new[]
                        {
                            new PlateUpAssetPointer("Processes[1].Result", "sharedassets0.assets", 300),
                            new PlateUpAssetPointer("Processes[0].Process", "sharedassets1.assets", 200),
                            new PlateUpAssetPointer("Processes[0].Process", "sharedassets1.assets", 200),
                            new PlateUpAssetPointer("Prefab", "sharedassets1.assets", 999),
                            PlateUpAssetPointer.Failed("Broken", "Unreadable pointer")
                        }),
                    PlateUpAssetObject.Resolved("Kitchen.GameData", "KitchenData.Process", "Knead", 20,
                        "sharedassets1.assets", 200),
                    PlateUpAssetObject.Resolved("Kitchen.GameData", "KitchenData.Item", "Bread", 30,
                        "sharedassets0.assets", 300),
                    PlateUpAssetObject.Resolved("UnityEngine.CoreModule", "UnityEngine.GameObject", "Prefab", 0,
                        "sharedassets1.assets", 999)
                }
            };

            VanillaGdoScanResult result = new AssetsToolsPlateUpAssetReader(backend).Scan(
                _root, null, CancellationToken.None);

            Assert.That(result.Relationships.Select(value => value.FieldPath),
                Is.EqualTo(new[] { "Processes[0].Process", "Processes[1].Result" }));
            Assert.That(result.Relationships[0].SourceId, Is.EqualTo(10));
            Assert.That(result.Relationships[0].TargetId, Is.EqualTo(20));
            Assert.That(result.Relationships[0].Label, Is.EqualTo("Process"));
            Assert.That(result.Relationships[1].TargetRuntimeType, Is.EqualTo("KitchenData.Item"));
            Assert.That(result.Diagnostics.Any(value => value.Code == "relationship-pointer-failed"), Is.True);
        }

        [Test]
        public void InstalledGameFindsWaterBySerializedIdentity()
        {
            string installedRoot = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(installedRoot))
                Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the installed PlateUp compatibility oracle.");

            VanillaGdoScanResult result = new AssetsToolsPlateUpAssetReader().Scan(
                installedRoot,
                null,
                CancellationToken.None);

            Assert.That(result.HasFatalDiagnostics, Is.False, string.Join("\n", result.Diagnostics.Select(item => item.Message)));
            VanillaGdoScanEntry water = result.Entries.Single(item => item.Name == "Water");
            Assert.That(water.Id, Is.EqualTo(1657174953));
            Assert.That(water.RuntimeType, Is.EqualTo("KitchenData.Item"));
            Assert.That(water.RelativeAssetPath, Is.EqualTo("sharedassets1.assets"));
            Assert.That(water.PathId, Is.EqualTo(73340));
            VanillaGdoScanEntry dough = result.Entries.Single(item => item.Name == "Dough");
            Assert.That(result.Relationships.Any(value => value.SourceId == dough.Id || value.TargetId == dough.Id),
                Is.True, "The installed Dough GDO should have at least one serialized vanilla relationship.");
        }

        private void CreateManagedAssemblyPlaceholder()
        {
            string managed = Path.Combine(_root, "PlateUp_Data", "Managed");
            Directory.CreateDirectory(managed);
            File.WriteAllBytes(Path.Combine(managed, "Kitchen.GameData.dll"), new byte[] { 1 });
        }

        private sealed class StubBackend : IPlateUpAssetReaderBackend
        {
            public IReadOnlyList<PlateUpAssetObject> Objects { get; set; } = Array.Empty<PlateUpAssetObject>();

            public IReadOnlyList<PlateUpAssetObject> Read(
                string gameRoot,
                IProgress<VanillaGdoScanProgress> progress,
                CancellationToken cancellationToken)
            {
                return Objects;
            }
        }
    }
}
