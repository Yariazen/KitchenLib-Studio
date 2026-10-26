using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class ImportedGdoSnapshotService : IExternalGdoSnapshotService
    {
        private readonly IExternalGdoSnapshotService _vanilla;
        private readonly ExternalGdoCatalogRepository _catalogues;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public ImportedGdoSnapshotService(IExternalGdoSnapshotService vanilla, ExternalGdoCatalogRepository catalogues)
        { _vanilla = vanilla; _catalogues = catalogues ?? throw new ArgumentNullException(nameof(catalogues)); }

        public IDisposable Request(string providerKey, int gdoId, string runtimeType,
            Action<ExternalGdoSnapshotResult> completed)
        {
            if (!ExternalGdoSnapshotEligibility.IsEligible(runtimeType))
            {
                completed(ExternalGdoSnapshotResults.Unsupported());
                return CompletedSnapshotSubscription.Instance;
            }
            if (providerKey == ExternalGdoCatalogRepository.VanillaProviderKey)
                return _vanilla?.Request(providerKey, gdoId, runtimeType, completed) ?? Complete(completed, "The vanilla snapshot service is unavailable.");
            if (string.IsNullOrEmpty(providerKey))
                return Complete(completed, "An external GDO provider is required.");
            ExternalGdoCatalogEntry entry = _catalogues.Find(providerKey, gdoId, runtimeType);
            if (entry == null || string.IsNullOrEmpty(entry.CatalogRoot) || string.IsNullOrEmpty(entry.PreviewRelativePath))
                return Complete(completed, "The imported GDO has no cached snapshot.");
            string identity = providerKey + "\n" + gdoId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "\n" + (runtimeType ?? string.Empty);
            if (_textures.TryGetValue(identity, out Texture2D cached) && cached != null)
            {
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Ready, cached));
                return CompletedSnapshotSubscription.Instance;
            }
            try
            {
                string path = LocalModCatalogPackage.ResolveContainedPath(entry.CatalogRoot, entry.PreviewRelativePath);
                if (!File.Exists(path)) return Complete(completed, "The imported snapshot payload is missing.");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    { name = entry.DisplayName + " Preview", hideFlags = HideFlags.HideAndDontSave };
                if (!texture.LoadImage(File.ReadAllBytes(path), true))
                { UnityEngine.Object.DestroyImmediate(texture); return Complete(completed, "The imported snapshot PNG is invalid."); }
                _textures[identity] = texture;
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Ready, texture));
                return CompletedSnapshotSubscription.Instance;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
            { return Complete(completed, exception.Message); }
        }

        public void Dispose()
        {
            _vanilla?.Dispose();
            foreach (Texture2D texture in _textures.Values) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            _textures.Clear();
        }

        private static IDisposable Complete(Action<ExternalGdoSnapshotResult> completed, string diagnostic)
        { completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Unavailable, diagnostic: diagnostic)); return CompletedSnapshotSubscription.Instance; }
    }
}
