using System;
using System.Linq;
using System.Threading;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ArbitraryVanillaSnapshotTests
    {
        [Test]
        public void ReconstructsOrderedMaterialsAndTextureProperties()
        {
            PreviewMeshData mesh = Triangle("Two Slots", 2);
            var texture = new PreviewTextureData("Checker", 2, 2, TextureFormat.RGBA32, false,
                new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 }, PreviewTextureEncoding.Raw);
            var first = new PreviewMaterialData("First", "Standard",
                new[] { new PreviewMaterialFloatData("_Glossiness", 0.25f) },
                new[] { new PreviewMaterialColorData("_Color", Color.red) },
                Array.Empty<PreviewMaterialVectorData>(),
                new[] { new PreviewMaterialTextureData("_MainTex", 0, new Vector2(2, 3), new Vector2(0.25f, 0.5f)) });
            var second = new PreviewMaterialData("Second", "Standard", Array.Empty<PreviewMaterialFloatData>(),
                Array.Empty<PreviewMaterialColorData>(), Array.Empty<PreviewMaterialVectorData>(), Array.Empty<PreviewMaterialTextureData>());
            var node = new PreviewNodeData { Name = "Textured", MeshIndex = 0, RendererKind = PreviewRendererKind.Mesh,
                MaterialIndices = new[] { 0, 1 } };
            var model = new VanillaPreviewModel(node, new[] { mesh }, new[] { first, second }, new[] { texture },
                Array.Empty<PreviewSpriteData>(), Array.Empty<string>());

            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                Material[] materials = preview.Root.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.That(materials.Select(value => value.name), Is.EqualTo(new[] { "First", "Second" }));
                Assert.That(materials[0].GetFloat("_Glossiness"), Is.EqualTo(0.25f));
                Assert.That(materials[0].color, Is.EqualTo(Color.red));
                Assert.That(materials[0].mainTextureScale, Is.EqualTo(new Vector2(2, 3)));
                Assert.That(materials[0].mainTextureOffset, Is.EqualTo(new Vector2(0.25f, 0.5f)));
            }
        }

        [Test]
        public void MapsPlateUpColor0OntoStandardFallbackMaterial()
        {
            var sourceColor = new Color(0.887f, 0.854f, 0.790f, 0f);
            var material = new PreviewMaterialData("Flour", "Standard",
                new[] { new PreviewMaterialFloatData("_Shininess", 0.25f),
                    new PreviewMaterialFloatData("_Specular", 0.4f) },
                new[] { new PreviewMaterialColorData("_Color0", sourceColor) },
                Array.Empty<PreviewMaterialVectorData>(), Array.Empty<PreviewMaterialTextureData>());
            var node = new PreviewNodeData { Name = "Flour", MeshIndex = 0,
                RendererKind = PreviewRendererKind.Mesh, MaterialIndices = new[] { 0 } };
            var model = new VanillaPreviewModel(node, new[] { Triangle("Flour", 1) }, new[] { material },
                Array.Empty<PreviewTextureData>(), Array.Empty<PreviewSpriteData>(), Array.Empty<string>());

            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                Color actual = preview.Root.GetComponent<MeshRenderer>().sharedMaterial.color;
                Assert.That(actual.r, Is.EqualTo(sourceColor.r).Within(0.001f));
                Assert.That(actual.g, Is.EqualTo(sourceColor.g).Within(0.001f));
                Assert.That(actual.b, Is.EqualTo(sourceColor.b).Within(0.001f));
                Assert.That(actual.a, Is.EqualTo(1f));
                Assert.That(preview.Root.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Glossiness"),
                    Is.EqualTo(0.25f));
                Assert.That(preview.Root.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Metallic"),
                    Is.EqualTo(0.4f).Within(0.001f));
            }
        }

        [Test]
        public void TransparentNamedFallbackUsesTransparentStandardBlend()
        {
            var material = new PreviewMaterialData("Sphere Transparent", "Standard",
                Array.Empty<PreviewMaterialFloatData>(), Array.Empty<PreviewMaterialColorData>(),
                Array.Empty<PreviewMaterialVectorData>(), Array.Empty<PreviewMaterialTextureData>());
            var node = new PreviewNodeData { Name = "Water", MeshIndex = 0,
                RendererKind = PreviewRendererKind.Mesh, MaterialIndices = new[] { 0 } };
            var model = new VanillaPreviewModel(node, new[] { Triangle("Water", 1) }, new[] { material },
                Array.Empty<PreviewTextureData>(), Array.Empty<PreviewSpriteData>(), Array.Empty<string>());

            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                Material actual = preview.Root.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(actual.color.a, Is.LessThan(1f));
                Assert.That(actual.renderQueue, Is.EqualTo(3000));
                Assert.That(actual.GetInt("_ZWrite"), Is.Zero);
            }
        }

        [Test]
        public void PreservesInactiveChildrenAndDisabledRenderers()
        {
            var inactive = new PreviewNodeData { Name = "Inactive", Active = false, MeshIndex = 0,
                RendererKind = PreviewRendererKind.Mesh };
            var disabled = new PreviewNodeData { Name = "Disabled", MeshIndex = 0,
                RendererKind = PreviewRendererKind.Mesh, RendererEnabled = false };
            var root = new PreviewNodeData { Name = "Root", Children = new[] { inactive, disabled } };
            var model = new VanillaPreviewModel(root, new[] { Triangle("Triangle", 1) }, Array.Empty<PreviewMaterialData>(),
                Array.Empty<PreviewTextureData>(), Array.Empty<PreviewSpriteData>(), Array.Empty<string>());

            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                Transform inactiveTransform = preview.Root.transform.Find("Inactive");
                Transform disabledTransform = preview.Root.transform.Find("Disabled");
                Assert.That(inactiveTransform.gameObject.activeSelf, Is.False);
                Assert.That(disabledTransform.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(preview.Root.GetComponentsInChildren<Renderer>(false).Where(value => value.enabled), Is.Empty);
                Assert.Throws<InvalidOperationException>(() => GameObjectSnapshotRenderer.Render(preview.Root,
                    new SnapshotRenderOptions(64, 64)));
            }
        }

        [Test]
        public void ReconstructsStaticSkinnedFallbackAndSpriteGeometry()
        {
            var skinned = new PreviewNodeData { Name = "Skinned", MeshIndex = 0, RendererKind = PreviewRendererKind.SkinnedMesh };
            var sprite = new PreviewNodeData { Name = "Sprite", SpriteIndex = 0, RendererKind = PreviewRendererKind.Sprite };
            var root = new PreviewNodeData { Name = "Root", Children = new[] { skinned, sprite } };
            var texture = new PreviewTextureData("Sprite Texture", 2, 2, TextureFormat.RGBA32, false,
                Enumerable.Repeat((byte)255, 16).ToArray(), PreviewTextureEncoding.Raw);
            var spriteData = new PreviewSpriteData("Icon", 0, new Rect(0, 0, 2, 2), new Vector2(1, 1),
                Vector4.zero, 2, PreviewSpritePackingRotation.None);
            var model = new VanillaPreviewModel(root, new[] { Triangle("Skinned Mesh", 1) }, Array.Empty<PreviewMaterialData>(),
                new[] { texture }, new[] { spriteData }, new[] { "Static skinned-mesh fallback: Skinned" });

            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                Assert.That(preview.Root.transform.Find("Skinned").GetComponent<MeshRenderer>(), Is.Not.Null);
                SpriteRenderer renderer = preview.Root.transform.Find("Sprite").GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sprite.rect, Is.EqualTo(new Rect(0, 0, 2, 2)));
                Assert.That(renderer.sprite.pivot, Is.EqualTo(new Vector2(1, 1)));
            }
        }

        [Test]
        public void InstalledSkinnedAndSpriteFixturesDecodeAsArbitraryRoots()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run installed snapshot fixtures.");
            var decoder = new AssetsToolsPlateUpPreviewDecoder();
            VanillaPreviewModel skinned = decoder.Decode(root,
                new VanillaObjectIdentity("sharedassets1.assets", 19549, 1, "GameObject"), null, CancellationToken.None);
            VanillaPreviewModel sprite = decoder.Decode(root,
                new VanillaObjectIdentity("sharedassets1.assets", 4784, 1, "GameObject"), null, CancellationToken.None);

            Assert.That(Flatten(skinned.Root).Any(value => value.RendererKind == PreviewRendererKind.SkinnedMesh), Is.True);
            Assert.That(skinned.Diagnostics, Has.Some.Contains("Static skinned-mesh fallback"));
            Assert.That(Flatten(sprite.Root).Any(value => value.RendererKind == PreviewRendererKind.Sprite), Is.True);
            Assert.That(sprite.Sprites, Is.Not.Empty);
            using (ReconstructedPreview skinnedPreview = VanillaPreviewReconstructor.Reconstruct(skinned))
                Assert.That(skinnedPreview.Root.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty);
            using (ReconstructedPreview spritePreview = VanillaPreviewReconstructor.Reconstruct(sprite))
                Assert.That(spritePreview.Root.GetComponentsInChildren<SpriteRenderer>(true), Is.Not.Empty);
        }

        [Test]
        public void InstalledNestedChildCanBeDecodedAsSnapshotRoot()
        {
            string root = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run installed snapshot fixtures.");
            VanillaPreviewModel model = new AssetsToolsPlateUpPreviewDecoder().Decode(root,
                new VanillaObjectIdentity("sharedassets1.assets", 5390, 1, "GameObject"), null, CancellationToken.None);
            Assert.That(model.Root.Name, Is.EqualTo("Sphere"));
            Assert.That(model.Root.RendererKind, Is.EqualTo(PreviewRendererKind.Mesh));
            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
            {
                SnapshotRenderResult render = GameObjectSnapshotRenderer.Render(preview.Root, new SnapshotRenderOptions(128, 128));
                Assert.That(render.VisiblePixelCount, Is.GreaterThan(20));
            }
        }

        [Test]
        public void SourceFingerprintRejectsChangedInstalledData()
        {
            string root = Path.Combine(Path.GetTempPath(), "plateup-fingerprint-" + Guid.NewGuid().ToString("N"));
            string dataRoot = Path.Combine(root, "PlateUp_Data"); Directory.CreateDirectory(dataRoot);
            string sourcePath = Path.Combine(dataRoot, "sharedassets1.assets"); File.WriteAllText(sourcePath, "first");
            try
            {
                string hash = PlateUpSourceFingerprint.Compute(sourcePath);
                var catalog = new VanillaAssetCatalogData { sourceFingerprint = PlateUpSourceFingerprint.ComputeStrings(new[] { hash }),
                    sources = new[] { new VanillaAssetSourceData { relativePath = "sharedassets1.assets", sha256 = hash } } };
                Assert.DoesNotThrow(() => PlateUpSourceFingerprint.Validate(root, catalog));
                File.WriteAllText(sourcePath, "changed");
                Assert.Throws<InvalidDataException>(() => PlateUpSourceFingerprint.Validate(root, catalog));
            }
            finally { Directory.Delete(root, true); }
        }

        private static System.Collections.Generic.IEnumerable<PreviewNodeData> Flatten(PreviewNodeData root)
        { yield return root; foreach (PreviewNodeData child in root.Children) foreach (PreviewNodeData value in Flatten(child)) yield return value; }

        private static PreviewMeshData Triangle(string name, int submeshes)
        {
            int[][] indices = Enumerable.Range(0, submeshes).Select(_ => new[] { 0, 1, 2 }).ToArray();
            return new PreviewMeshData(name, new[] { Vector3.zero, Vector3.right, Vector3.up }, Array.Empty<Vector3>(),
                Array.Empty<Vector4>(), Array.Empty<Vector2>(), indices, new Bounds(new Vector3(0.5f, 0.5f), Vector3.one));
        }
    }
}
