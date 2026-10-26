using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal sealed class SnapshotRenderOptions
    {
        public SnapshotRenderOptions(int width, int height) { Width = width; Height = height; }
        public int Width { get; }
        public int Height { get; }
    }

    internal sealed class SnapshotRenderResult
    {
        public SnapshotRenderResult(byte[] png, int width, int height, int visiblePixels, Bounds bounds)
        { PngBytes = png; Width = width; Height = height; VisiblePixelCount = visiblePixels; Bounds = bounds; }
        public byte[] PngBytes { get; }
        public int Width { get; }
        public int Height { get; }
        public int VisiblePixelCount { get; }
        public Bounds Bounds { get; }
    }

    internal static class GameObjectSnapshotRenderer
    {
        public static SnapshotRenderResult Render(GameObject source, SnapshotRenderOptions options)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Renderer[] renderers = source.GetComponentsInChildren<Renderer>(false).Where(value => value.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Snapshot subject has no visible renderers.");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            var preview = new PreviewRenderUtility();
            Texture2D image = null;
            try
            {
                preview.AddSingleGO(source);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 1.35f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.clear;
                Vector3 direction = new Vector3(1f, -1f, 1f).normalized;
                preview.camera.transform.position = bounds.center - direction * 10f;
                preview.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                preview.camera.nearClipPlane = 0.01f; preview.camera.farClipPlane = 50f;
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                preview.ambientColor = new Color(0.65f, 0.65f, 0.65f, 1f);
                preview.BeginStaticPreview(new Rect(0, 0, options.Width, options.Height));
                preview.camera.Render();
                image = preview.EndStaticPreview();
                Color32[] pixels = image.GetPixels32();
                int visible = pixels.Count(pixel => pixel.a > 4 && (pixel.r > 4 || pixel.g > 4 || pixel.b > 4));
                return new SnapshotRenderResult(image.EncodeToPNG(), options.Width, options.Height, visible, bounds);
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                preview.Cleanup();
            }
        }
    }
}
