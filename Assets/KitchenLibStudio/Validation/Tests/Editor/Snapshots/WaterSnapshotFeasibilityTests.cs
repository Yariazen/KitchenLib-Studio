using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class WaterSnapshotFeasibilityTests
    {
        [Test]
        public void WaterDecodesReconstructsAndRendersVisiblePng()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the Water snapshot feasibility gate.");
            VanillaPreviewModel model = new AssetsToolsPlateUpPreviewDecoder().Decode(root,
                new VanillaObjectIdentity("sharedassets1.assets", 4244, 1, "GameObject"), null, CancellationToken.None);

            Assert.That(model.Meshes.Select(mesh => mesh.Name), Is.EquivalentTo(new[] { "sphere_shape_24_16", "cup" }));
            Assert.That(model.Root.Children.Select(child => child.Name), Is.EquivalentTo(new[] { "Sphere", "cup" }));
            Assert.That(model.Root.Children.Single(child => child.Name == "Sphere").MaterialNames,
                Is.EqualTo(new[] { "Sphere Transparent" }));
            Assert.That(model.Root.Children.Single(child => child.Name == "cup").Children.Single().MaterialNames,
                Is.EqualTo(new[] { "Plate" }));
            Assert.That(model.Diagnostics, Has.Some.Contains("fallback"));
            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                SnapshotRenderResult result = GameObjectSnapshotRenderer.Render(preview.Root,
                    new SnapshotRenderOptions(256, 256));
                Assert.That(result.PngBytes, Is.Not.Empty);
                Assert.That(result.Width, Is.EqualTo(256));
                Assert.That(result.Height, Is.EqualTo(256));
                Assert.That(result.VisiblePixelCount, Is.GreaterThan(100));
                Assert.That(float.IsNaN(result.Bounds.size.x), Is.False);
            }
        }

        [Test]
        public void FailedReconstructionDestroysObjectsCreatedBeforeFailure()
        {
            int meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name == "Broken Preview Mesh");
            var brokenMesh = new PreviewMeshData("Broken Preview Mesh",
                new[] { Vector3.zero }, new Vector3[0], new Vector4[0], new Vector2[0],
                new[] { new[] { 0, 1, 2 } }, new Bounds(Vector3.zero, Vector3.one));
            var root = new PreviewNodeData { Name = "Broken", MeshIndex = 0 };

            Assert.Throws<System.IO.InvalidDataException>(() =>
                VanillaPreviewReconstructor.Reconstruct(new VanillaPreviewModel(root, new[] { brokenMesh }, new string[0])));
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name == "Broken Preview Mesh"),
                Is.EqualTo(meshesBefore));
        }

        [Test]
        public void ReconstructionDestroysAllOwnedObjectsOnDispose()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the Water snapshot feasibility gate.");
            VanillaPreviewModel model = new AssetsToolsPlateUpPreviewDecoder().Decode(root,
                new VanillaObjectIdentity("sharedassets1.assets", 4244, 1, "GameObject"), null, CancellationToken.None);
            ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model);
            int instanceId = preview.Root.GetInstanceID();

            preview.Dispose();

            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Any(value => value.GetInstanceID() == instanceId), Is.False);
        }
    }
}
