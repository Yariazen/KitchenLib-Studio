using System;
using KitchenData;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoReferenceField : VisualElement
    {
        public static GdoOverlayValue ClearValue() => GdoOverlayValue.Null();

        public static GdoOverlayValue ApplyExternal(string providerKey, int id, string expectedTypeName)
        {
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id), "A GDO reference ID cannot be zero.");
            return GdoOverlayValue.Reference(GdoReferenceToken.Create(providerKey, id, expectedTypeName));
        }

        public GdoReferenceField(string label, string expectedTypeName, GdoOverlayValue value, Action<GdoOverlayValue> changed)
            : this(label, expectedTypeName, value, changed, ExternalGdoCatalogRepository.Current) { }

        public GdoReferenceField(string label, string expectedTypeName, GdoOverlayValue value,
            Action<GdoOverlayValue> changed, ExternalGdoCatalogRepository repository)
        {
            ExpectedTypeName = expectedTypeName;
            AddToClassList("kls-gdo-reference");
            Add(new Label(label + " (" + expectedTypeName + ")"));

            GdoReferenceToken token = value != null && value.Kind == GdoOverlayValueKind.GdoReference ? value.ReferenceValue : null;
            var asset = new ObjectField("Project GDO") { objectType = typeof(GameDataObject), allowSceneObjects = false, value = token == null ? null : token.Asset };
            asset.RegisterValueChangedCallback(evt =>
            {
                ClearError();
                if (evt.newValue == null) { Picker?.ClearSelection(); changed(ClearValue()); return; }
                if (!(evt.newValue is GameDataObject gdo)) return;
                try { changed(GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(gdo, ExpectedTypeName))); }
                catch (ArgumentException exception)
                {
                    asset.SetValueWithoutNotify(token?.Asset);
                    ShowError(exception.Message);
                }
            });
            Add(asset);

            Picker = new ExternalGdoPicker(expectedTypeName, repository,
                entry => changed(ApplyExternal(entry.ProviderKey, entry.RuntimeId, ExpectedTypeName)));
            Add(Picker);
            if (token != null && token.Asset == null)
            {
                ExternalGdoCatalogEntry existing = repository.Find(token.ProviderKey, token.Id, token.ExpectedType);
                if (existing != null) Picker.ShowSelection(existing);
            }
            var id = new IntegerField("External GDO ID") { name = "external-gdo-id", value = token == null || token.Asset != null ? 0 : token.Id, isDelayed = true };
            id.RegisterValueChangedCallback(evt =>
            {
                ClearError();
                if (evt.newValue == 0) { Picker.ClearSelection(); changed(ClearValue()); }
                else changed(ApplyExternal(ExternalGdoCatalogRepository.VanillaProviderKey, evt.newValue, ExpectedTypeName));
            });
            Picker.ManualContent.Add(id);
            Add(new Button(() => { Picker.ClearSelection(); id.SetValueWithoutNotify(0); changed(ClearValue()); })
                { name = "clear-reference", text = "Clear Reference" });

            void ClearError()
            {
                HelpBox error = this.Q<HelpBox>("reference-error");
                if (error != null) error.RemoveFromHierarchy();
            }
            void ShowError(string message) => Add(new HelpBox(message, HelpBoxMessageType.Error) { name = "reference-error" });
        }

        public string ExpectedTypeName { get; }
        public ExternalGdoPicker Picker { get; }
    }
}
