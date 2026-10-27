using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KitchenLib.Studio.Editor.Catalogs.Vanilla;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal sealed class VanillaSnapshotBatchRenderer
    {
        private readonly Func<VanillaAssetCatalogData, int, ExternalGdoSnapshotSourceResolution> _resolve;
        private readonly Func<string, CancellationToken, Task<SnapshotRenderResult>> _render;

        public VanillaSnapshotBatchRenderer(
            Func<VanillaAssetCatalogData, int, ExternalGdoSnapshotSourceResolution> resolve,
            Func<string, CancellationToken, Task<SnapshotRenderResult>> render)
        {
            _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public async Task<IReadOnlyList<VanillaSnapshotPng>> RenderAsync(
            VanillaAssetCatalogData catalog, CancellationToken token)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var unsupported = new List<VanillaSnapshotPng>();
            var resolved = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (VanillaAssetRecordData gdo in (catalog.records ?? Array.Empty<VanillaAssetRecordData>())
                .Where(value => value != null && value.kind == "gdo" && value.gdoId != 0)
                .OrderBy(value => value.gdoId))
            {
                token.ThrowIfCancellationRequested();
                if (!ExternalGdoSnapshotEligibility.IsEligible(gdo.runtimeType))
                {
                    unsupported.Add(new VanillaSnapshotPng(null, null, new[] { gdo.gdoId }, null, null,
                        "No snapshot for this GDO type."));
                    continue;
                }
                ExternalGdoSnapshotSourceResolution source = _resolve(catalog, gdo.gdoId);
                if (!source.Succeeded)
                {
                    unsupported.Add(new VanillaSnapshotPng(null, null, new[] { gdo.gdoId }, null, null,
                        source.Diagnostic));
                    continue;
                }
                if (!resolved.TryGetValue(source.GameObjectKey, out List<int> ids))
                    resolved.Add(source.GameObjectKey, ids = new List<int>());
                ids.Add(gdo.gdoId);
            }

            var output = new List<VanillaSnapshotPng>();
            var options = new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                ExternalGdoSnapshotCacheIdentity.ThumbnailSize);
            foreach (KeyValuePair<string, List<int>> item in resolved.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                SnapshotRenderResult rendered;
                try { rendered = await _render(item.Key, token); }
                catch (SnapshotSubjectUnsupportedException exception)
                {
                    unsupported.Add(new VanillaSnapshotPng(null, item.Key, item.Value.ToArray(), null, null,
                        exception.Message));
                    continue;
                }
                token.ThrowIfCancellationRequested();
                if (rendered == null || rendered.Width != options.Width || rendered.Height != options.Height ||
                    !SnapshotCache.IsPng(rendered.PngBytes))
                    throw new InvalidOperationException("Snapshot renderer returned an invalid 128x128 PNG for " + item.Key + ".");
                string sourceFingerprint = ExternalGdoSnapshotCacheIdentity.SourceFingerprint(catalog, item.Key);
                string cacheKey = ExternalGdoSnapshotCacheIdentity.BuildKey(item.Key, sourceFingerprint);
                var metadata = new SnapshotCacheMetadata
                {
                    width = rendered.Width,
                    height = rendered.Height,
                    subjectIdentity = ExternalGdoSnapshotCacheIdentity.VanillaSubject(item.Key),
                    sourceFingerprint = sourceFingerprint,
                    rendererVersion = ExternalGdoSnapshotCacheIdentity.RendererVersion,
                    diagnostics = Array.Empty<string>()
                };
                output.Add(new VanillaSnapshotPng(cacheKey, item.Key, item.Value.ToArray(),
                    rendered.PngBytes, metadata));
            }
            output.AddRange(unsupported.OrderBy(value => value.GdoIds[0]));
            return output;
        }
    }
}
