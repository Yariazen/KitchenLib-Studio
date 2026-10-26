using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using KitchenData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class SnapshotBrowserWindow : EditorWindow
    {
        private const string PlateUpRootPreference = "KitchenLibStudio.VanillaCatalog.PlateUpRoot";
        private CancellationTokenSource _cancellation;
        private Texture2D _previewTexture;
        private VanillaAssetRecord _selectedVanilla;
        private VanillaAssetCatalogData _catalog;
        private VanillaAssetCatalogRepository _repository;
        private GameObjectSnapshotService _service;
        private readonly CustomGdoSnapshotResolver _resolver = new CustomGdoSnapshotResolver();
        private readonly SnapshotCache _cache = new SnapshotCache();

        [MenuItem("KitchenLib Studio/Snapshot Browser")]
        public static void ShowWindow() => GetWindow<SnapshotBrowserWindow>("Snapshot Browser");
        private void OnEnable()
        {
            rootVisualElement.Clear(); VisualElement content = BuildContentForTests(); rootVisualElement.Add(content);
            TryLoadVanilla(content);
            ObjectField gdo = content.Q<ObjectField>("snapshot-custom-gdo");
            gdo.RegisterValueChangedCallback(_ => RefreshCandidates(content));
            content.Q<Button>("snapshot-generate").clicked += () => Generate(content);
        }

        private void OnDisable()
        {
            _cancellation?.Cancel(); _cancellation?.Dispose(); _cancellation = null;
            if (_previewTexture != null) DestroyImmediate(_previewTexture); _previewTexture = null;
        }

        private void TryLoadVanilla(VisualElement content)
        {
            string path = Path.Combine(Application.dataPath, "KitchenLibStudio", "Generated", "vanilla-snapshot-catalog.json");
            if (!File.Exists(path)) return;
            try
            {
                _catalog = VanillaAssetCatalogFile.Read(path); _repository = new VanillaAssetCatalogRepository(path); _repository.Reload();
                var picker = new VanillaGameObjectPicker(_repository, value => { _selectedVanilla = value; SetDiagnostic(content, "Selected " + value.DisplayName, HelpBoxMessageType.Info); });
                content.Insert(2, picker);
                string gameRoot = EditorPrefs.GetString(PlateUpRootPreference, string.Empty);
                if (!string.IsNullOrEmpty(gameRoot)) _service = new GameObjectSnapshotService(gameRoot, _catalog, _repository);
            }
            catch (Exception exception) { SetDiagnostic(content, exception.Message, HelpBoxMessageType.Warning); }
        }

        private void RefreshCandidates(VisualElement content)
        {
            var field = content.Q<PopupField<string>>("snapshot-prefab-field");
            var choices = new List<string> { "Auto" };
            GameDataObject gdo = content.Q<ObjectField>("snapshot-custom-gdo").value as GameDataObject;
            if (gdo != null) foreach (GdoPrefabCandidate candidate in _resolver.Candidates(gdo)) choices.Add(candidate.FieldPath);
            field.choices = choices; field.index = 0;
        }

        private async void Generate(VisualElement content)
        {
            _cancellation?.Cancel(); _cancellation?.Dispose(); _cancellation = new CancellationTokenSource();
            try
            {
                SnapshotSubject subject;
                GameDataObject gdo = content.Q<ObjectField>("snapshot-custom-gdo").value as GameDataObject;
                GameObject local = content.Q<ObjectField>("snapshot-local-object").value as GameObject;
                if (gdo != null)
                {
                    string selected = content.Q<PopupField<string>>("snapshot-prefab-field").value;
                    subject = SnapshotSubject.CustomGdo(gdo, selected == "Auto" ? null : selected);
                }
                else if (local != null) subject = SnapshotSubject.LocalGameObject(local);
                else if (_selectedVanilla != null) subject = SnapshotSubject.VanillaGameObjectKey(_selectedVanilla.Key);
                else throw new InvalidOperationException("Select a vanilla GameObject, local object, or custom GDO first.");
                int size = Mathf.Clamp(content.Q<IntegerField>("snapshot-resolution").value, 32, 2048);
                if (subject.Kind == SnapshotSubjectKind.VanillaGameObjectKey)
                {
                    SnapshotCacheEntry cached = TryGetCachedVanilla(_cache, _catalog, subject.VanillaKey, size);
                    if (cached != null)
                    {
                        ShowPreview(content, cached.PngBytes);
                        SetDiagnostic(content, "Loaded cached snapshot.", HelpBoxMessageType.Info);
                        return;
                    }
                }
                if (_service == null && subject.Kind == SnapshotSubjectKind.VanillaGameObjectKey)
                    throw new InvalidOperationException("The PlateUp installation is missing or stale. Metadata remains browseable, but this snapshot cannot be generated.");
                GameObjectSnapshotService service = _service ?? new GameObjectSnapshotService();
                SnapshotRenderResult result = await service.RenderAsync(subject, new SnapshotRenderOptions(size, size), _cancellation.Token);
                ShowPreview(content, result.PngBytes);
                if (subject.CanUsePersistentCache)
                {
                    string identity = SubjectIdentity(subject); string fingerprint = _catalog?.sourceFingerprint ?? LocalFingerprint(subject);
                    string key = subject.Kind == SnapshotSubjectKind.VanillaGameObjectKey
                        ? ExternalGdoSnapshotCacheIdentity.BuildKey(_catalog, subject.VanillaKey,
                            new SnapshotRenderOptions(size, size))
                        : SnapshotCache.BuildKey(identity, fingerprint,
                            ExternalGdoSnapshotCacheIdentity.RendererVersion, new SnapshotRenderOptions(size, size),
                            ExternalGdoSnapshotCacheIdentity.SettingsFingerprint);
                    if (subject.Kind == SnapshotSubjectKind.VanillaGameObjectKey)
                        fingerprint = ExternalGdoSnapshotCacheIdentity.SourceFingerprint(_catalog, subject.VanillaKey);
                    _cache.Put(key, result.PngBytes, new SnapshotCacheMetadata { width = result.Width, height = result.Height,
                        subjectIdentity = identity, sourceFingerprint = fingerprint,
                        rendererVersion = ExternalGdoSnapshotCacheIdentity.RendererVersion,
                        diagnostics = Array.Empty<string>() });
                }
                SetDiagnostic(content, "Snapshot generated successfully.", HelpBoxMessageType.Info);
            }
            catch (OperationCanceledException) { SetDiagnostic(content, "Snapshot generation cancelled.", HelpBoxMessageType.Warning); }
            catch (Exception exception) { SetDiagnostic(content, exception.Message, HelpBoxMessageType.Error); }
        }

        private void ShowPreview(VisualElement content, byte[] png)
        {
            if (_previewTexture != null) DestroyImmediate(_previewTexture);
            _previewTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!_previewTexture.LoadImage(png, true)) throw new InvalidDataException("Generated snapshot PNG could not be loaded.");
            content.Q<Image>("snapshot-preview").image = _previewTexture;
        }

        private static string SubjectIdentity(SnapshotSubject subject)
        {
            if (subject.Kind == SnapshotSubjectKind.VanillaGameObjectKey)
                return ExternalGdoSnapshotCacheIdentity.VanillaSubject(subject.VanillaKey);
            UnityEngine.Object value = subject.Kind == SnapshotSubjectKind.LocalGameObject ? (UnityEngine.Object)subject.GameObject : subject.Gdo;
            string path = AssetDatabase.GetAssetPath(value); string guid = string.IsNullOrEmpty(path) ? value.GetInstanceID().ToString() : AssetDatabase.AssetPathToGUID(path);
            return subject.Kind.ToString() + ":" + guid + ":" + (subject.PrefabFieldPath ?? string.Empty);
        }

        private static string LocalFingerprint(SnapshotSubject subject)
        {
            UnityEngine.Object value = subject.Kind == SnapshotSubjectKind.LocalGameObject ? (UnityEngine.Object)subject.GameObject : subject.Gdo;
            string path = AssetDatabase.GetAssetPath(value); return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.GetAssetDependencyHash(path).ToString();
        }

        public static SnapshotCacheEntry TryGetCachedVanilla(SnapshotCache cache,
            VanillaAssetCatalogData catalog, string gameObjectKey, int size)
        {
            if (cache == null || catalog == null || string.IsNullOrEmpty(gameObjectKey)) return null;
            int resolution = Mathf.Clamp(size, 32, 2048);
            string key = ExternalGdoSnapshotCacheIdentity.BuildKey(catalog, gameObjectKey,
                new SnapshotRenderOptions(resolution, resolution));
            return cache.TryGet(key);
        }

        private static void SetDiagnostic(VisualElement content, string message, HelpBoxMessageType type)
        { HelpBox box = content.Q<HelpBox>("snapshot-diagnostics"); box.text = message; box.messageType = type; }

        public static VisualElement BuildContentForTests()
        {
            var root = new VisualElement { name = "snapshot-browser" };
            root.Add(new Label("GAMEOBJECT SNAPSHOTS"));
            root.Add(new TextField("Search vanilla GameObjects") { name = "snapshot-search" });
            root.Add(new ObjectField("Local GameObject or prefab") { name = "snapshot-local-object", objectType = typeof(GameObject), allowSceneObjects = true });
            root.Add(new ObjectField("Custom GDO") { name = "snapshot-custom-gdo", objectType = typeof(GameDataObject), allowSceneObjects = false });
            root.Add(new PopupField<string>("Prefab field", new System.Collections.Generic.List<string> { "Auto" }, 0) { name = "snapshot-prefab-field" });
            root.Add(new IntegerField("Resolution") { name = "snapshot-resolution", value = 256 });
            root.Add(new Button { name = "snapshot-generate", text = "Generate / Refresh" });
            root.Add(new Image { name = "snapshot-preview", scaleMode = ScaleMode.ScaleToFit });
            root.Add(new HelpBox("Select a vanilla object, local GameObject, or custom GDO.", HelpBoxMessageType.Info) { name = "snapshot-diagnostics" });
            return root;
        }
    }
}
