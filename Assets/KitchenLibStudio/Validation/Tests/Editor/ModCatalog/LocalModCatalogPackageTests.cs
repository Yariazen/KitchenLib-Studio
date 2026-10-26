using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class LocalModCatalogPackageTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "kls-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ImportsProviderCatalogAndBakedVisualIntoLocalCache()
        {
            string package = Path.Combine(_root, "IngredientLib.klscatalog");
            LocalModCatalogPackage.Write(package, Manifest(), Catalog(), new Dictionary<string, byte[]>
            {
                ["prefabs/42.json"] = BakedPrefabFile.Serialize(Prefab()),
                ["snapshots/42.png"] = new byte[] { 137, 80, 78, 71 }
            });

            LocalModCatalogImportResult result = LocalModCatalogPackage.Import(package, Path.Combine(_root, "LibraryCache"));

            Assert.That(result.ProviderKey, Is.EqualTo("workshop:2913877103"));
            Assert.That(File.Exists(result.CatalogPath), Is.True);
            Assert.That(File.Exists(result.GetAbsolutePath("prefabs/42.json")), Is.True);
            Assert.That(BakedPrefabFile.Read(result.GetAbsolutePath("prefabs/42.json")).nodes.Length, Is.EqualTo(1));
        }

        [Test]
        public void RejectsArchivePathTraversal()
        {
            string package = Path.Combine(_root, "bad.klscatalog");
            using (FileStream stream = File.Create(package))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                archive.CreateEntry("../escape.txt");

            Assert.Throws<InvalidDataException>(() =>
                LocalModCatalogPackage.Import(package, Path.Combine(_root, "LibraryCache")));
        }

        [Test]
        public void RejectsPrefabBearingGdoWithoutBakedVisual()
        {
            LocalModCatalogPackageManifestData manifest = Manifest();
            manifest.visuals = new LocalModCatalogVisualData[0];

            Assert.Throws<InvalidDataException>(() => IngredientLibImportRequirements.Validate(manifest, Catalog()));
        }

        [Test]
        public void RejectsPackageThatIsNotIngredientLib()
        {
            LocalModCatalogPackageManifestData manifest = Manifest();
            manifest.workshopId = "123";

            Assert.Throws<InvalidDataException>(() => IngredientLibImportRequirements.Validate(manifest, Catalog()));
        }

        [Test]
        public void RejectsRelationshipBearingEntryWithoutRelationshipTree()
        {
            ModGdoCatalogData catalog = Catalog();
            catalog.entries[0].references = new[] { new ModGdoReferenceData { path = "Result",
                providerKey = "workshop:2913877103", runtimeId = 42, runtimeType = "KitchenData.Item" } };

            Assert.Throws<InvalidDataException>(() => IngredientLibImportRequirements.Validate(Manifest(), catalog));
        }

        [Test]
        public void RejectsDuplicateRelationshipPaths()
        {
            ModGdoCatalogData catalog = Catalog();
            catalog.entries[0].relationshipValues = new[] { Relationship("Result", 42), Relationship("Result", 42) };

            Assert.Throws<InvalidDataException>(() => IngredientLibImportRequirements.Validate(Manifest(), catalog));
        }

        [Test]
        public void RejectsMismatchedResolvedRelationshipIdentity()
        {
            ModGdoCatalogData catalog = Catalog();
            catalog.entries[0].relationshipValues = new[] { Relationship("Result", 42) };
            catalog.entries[0].relationshipValues[0].resolvedGdo.providerKey = "workshop:other";

            Assert.Throws<InvalidDataException>(() => IngredientLibImportRequirements.Validate(Manifest(), catalog));
        }

        [Test]
        public void RejectsCacheInsideUnityAssets()
        {
            string package = Path.Combine(_root, "IngredientLib.klscatalog");
            LocalModCatalogPackage.Write(package, Manifest(), Catalog(), new Dictionary<string, byte[]>
            {
                ["prefabs/42.json"] = BakedPrefabFile.Serialize(Prefab()),
                ["snapshots/42.png"] = new byte[] { 1 }
            });

            Assert.Throws<InvalidOperationException>(() =>
                LocalModCatalogPackage.Import(package, Path.Combine(_root, "Assets", "Imported")));
        }

        private static LocalModCatalogPackageManifestData Manifest() => new LocalModCatalogPackageManifestData
        {
            schemaVersion = LocalModCatalogPackage.SchemaVersion,
            packageKind = LocalModCatalogPackage.PackageKind,
            localOnly = true,
            providerKey = "workshop:2913877103",
            providerName = "IngredientLib",
            workshopId = "2913877103",
            modVersion = "1.3.7",
            kitchenLibVersion = "1.1.0",
            plateUpBuildId = "test-build",
            sourceAssembly = "IngredientLib",
            sourceAssemblySha256 = new string('a', 64),
            sourceAssetBundleSha256 = new string('b', 64),
            catalogPath = "mod-gdo-catalog.json",
            visuals = new[]
            {
                new LocalModCatalogVisualData
                {
                    runtimeId = 42,
                    prefabPath = "prefabs/42.json",
                    snapshotPath = "snapshots/42.png"
                }
            }
        };

        private static ModGdoCatalogData Catalog() => new ModGdoCatalogData
        {
            schemaVersion = ModGdoCatalogFile.SchemaVersion,
            captureToken = "local-export",
            steamAppId = ModGdoCatalogFile.PlateUpSteamAppId,
            steamBuildId = "test-build",
            bridgeVersion = "1",
            providers = new[] { new ModGdoProviderData { key = "workshop:2913877103", displayName = "IngredientLib", version = "1.3.7", workshopId = "2913877103", sourceAssembly = "IngredientLib" } },
            entries = new[] { new ModGdoEntryData { providerKey = "workshop:2913877103", runtimeId = 42, displayName = "Pork", runtimeType = "KitchenData.Item", sourceAssembly = "IngredientLib", hasPrefab = true, previewState = "available", previewRelativePath = "snapshots/42.png" } },
            relationships = new ExternalGdoRelationshipData[0],
            diagnostics = new ModGdoCatalogDiagnosticData[0]
        };

        private static BakedPrefabData Prefab() => new BakedPrefabData
        {
            schemaVersion = BakedPrefabFile.SchemaVersion,
            runtimeId = 42,
            displayName = "Pork",
            nodes = new[] { new BakedPrefabNodeData { name = "Pork", parentIndex = -1, localScale = new[] { 1f, 1f, 1f }, localPosition = new[] { 0f, 0f, 0f }, localRotation = new[] { 0f, 0f, 0f, 1f }, active = true } },
            meshes = new BakedMeshData[0],
            renderers = new BakedRendererData[0],
            materials = new BakedMaterialData[0],
            textures = new BakedTextureData[0]
        };

        private static ModGdoRelationshipValueData Relationship(string path, int runtimeId) => new ModGdoRelationshipValueData
        {
            name = path,
            path = path,
            declaredType = "KitchenData.Item",
            runtimeType = "KitchenData.Item",
            kind = "gdo-reference",
            rawValue = runtimeId.ToString(),
            resolvedGdo = new ModGdoResolvedIdentityData
            {
                providerKey = "workshop:2913877103",
                runtimeId = runtimeId,
                runtimeType = "KitchenData.Item"
            },
            children = new ModGdoRelationshipValueData[0]
        };
    }
}
