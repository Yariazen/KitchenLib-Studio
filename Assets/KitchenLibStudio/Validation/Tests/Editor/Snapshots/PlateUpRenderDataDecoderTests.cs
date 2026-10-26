using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class PlateUpRenderDataDecoderTests
    {
        [Test]
        public void WaterMeshesDecodeVerticesIndicesAndSubmeshes()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the installed PlateUp render-data oracle.");

            PreviewMeshData sphere = PlateUpMeshDecoder.DecodeInstalled(root, "sharedassets1.assets", 1860, CancellationToken.None);
            PreviewMeshData cup = PlateUpMeshDecoder.DecodeInstalled(root, "sharedassets1.assets", 2493, CancellationToken.None);

            Assert.That(sphere.Name, Is.EqualTo("sphere_shape_24_16"));
            Assert.That(sphere.Vertices, Has.Length.EqualTo(423));
            Assert.That(sphere.SubmeshIndices, Has.Length.EqualTo(1));
            Assert.That(sphere.SubmeshIndices[0], Is.Not.Empty);
            Assert.That(cup.Name, Is.EqualTo("cup"));
            Assert.That(cup.Vertices, Has.Length.EqualTo(120));
            Assert.That(cup.SubmeshIndices, Has.Length.EqualTo(1));
            Assert.That(cup.SubmeshIndices[0], Is.Not.Empty);
        }

        [Test]
        public void DefaultParticleDecodesStreamedTexture()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the installed PlateUp render-data oracle.");

            PreviewTextureData texture = PlateUpTextureDecoder.DecodeInstalled(root, "sharedassets1.assets", 760, CancellationToken.None);

            Assert.That(texture.Name, Is.EqualTo("Default-Particle"));
            Assert.That(texture.Width, Is.EqualTo(64));
            Assert.That(texture.Height, Is.EqualTo(64));
            Assert.That(texture.Data, Is.Not.Empty);
        }

        [Test]
        public void ResourceReaderRejectsNegativeOrOversizedRanges()
        {
            string directory = Path.Combine(Path.GetTempPath(), "kls-resource-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "fixture.resS"), new byte[32]);
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => PlateUpSerializedResourceReader.Read(
                    directory, "fixture.resS", -1, 4, CancellationToken.None));
                Assert.Throws<ArgumentOutOfRangeException>(() => PlateUpSerializedResourceReader.Read(
                    directory, "fixture.resS", 0, PlateUpSerializedResourceReader.MaximumReadBytes + 1, CancellationToken.None));
                Assert.Throws<EndOfStreamException>(() => PlateUpSerializedResourceReader.Read(
                    directory, "fixture.resS", 24, 16, CancellationToken.None));
            }
            finally
            {
                File.Delete(Path.Combine(directory, "fixture.resS"));
                Directory.Delete(directory);
            }
        }

        [Test]
        public void MalformedPackedMeshLengthsFailBeforeAllocation()
        {
            Assert.Throws<InvalidDataException>(() => PlateUpMeshDecoder.DecodePackedFloats(
                int.MaxValue, 32, 0f, 1f, new byte[1]));
            Assert.Throws<InvalidDataException>(() => PlateUpMeshDecoder.DecodePackedFloats(
                3, 33, 0f, 1f, new byte[16]));
            Assert.Throws<InvalidDataException>(() => PlateUpMeshDecoder.DecodePackedFloats(
                12, 16, 0f, 1f, new byte[1]));
        }

        [Test]
        public void CatalogDecodeIsAvailableThroughDecoderInterface()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the installed decoder interface oracle.");
            string path = Path.Combine(Application.dataPath, "KitchenLibStudio", "Generated",
                "vanilla-snapshot-catalog.json");
            VanillaAssetCatalogData catalog = VanillaAssetCatalogFile.Read(path);
            var repository = new VanillaAssetCatalogRepository(path);
            Assert.That(repository.Reload(), Is.True);
            ExternalGdoSnapshotSourceResolution source =
                ExternalGdoSnapshotSourceResolver.Resolve(catalog, 1657174953);
            IPlateUpPreviewDecoder decoder = new AssetsToolsPlateUpPreviewDecoder();

            VanillaPreviewModel model = decoder.Decode(root, repository.Find(source.GameObjectKey), catalog,
                null, CancellationToken.None);

            Assert.That(model.Materials, Is.Not.Empty);
        }

        [Test]
        public void DecoderUsesClassDatabasePathCapturedAtConstruction()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the installed decoder path oracle.");
            string classData = Path.Combine(Application.dataPath, "KitchenLibStudio", "Editor", "ThirdParty",
                "AssetsTools.NET", "classdata.tpk");
            IPlateUpPreviewDecoder decoder = new AssetsToolsPlateUpPreviewDecoder(classData);

            VanillaPreviewModel model = Task.Run(() => decoder.Decode(root,
                new VanillaObjectIdentity("sharedassets1.assets", 5390, 1, "GameObject"), null,
                CancellationToken.None)).GetAwaiter().GetResult();

            Assert.That(model.Root.Name, Is.EqualTo("Sphere"));
        }
    }
}
