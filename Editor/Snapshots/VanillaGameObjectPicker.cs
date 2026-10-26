using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class VanillaGameObjectPicker : VisualElement
    {
        private readonly VanillaAssetCatalogRepository _repository;
        private readonly Action<VanillaAssetRecord> _selected;
        private readonly ScrollView _results;
        public VanillaGameObjectPicker(VanillaAssetCatalogRepository repository, Action<VanillaAssetRecord> selected)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository)); _selected = selected;
            name = "vanilla-gameobject-picker";
            var search = new TextField("Search vanilla GameObjects") { name = "snapshot-search" };
            search.RegisterValueChangedCallback(evt => Refresh(evt.newValue)); Add(search);
            _results = new ScrollView { name = "snapshot-results" }; _results.style.maxHeight = 240; Add(_results); Refresh(string.Empty);
        }
        public IReadOnlyList<VanillaAssetRecord> Search(string query) => _repository.Search(query, VanillaAssetKind.GameObject, false);
        private void Refresh(string query)
        {
            _results.Clear(); int count = 0;
            foreach (VanillaAssetRecord record in Search(query))
            {
                if (count++ >= 200) break; VanillaAssetRecord captured = record;
                _results.Add(new Button(() => _selected?.Invoke(captured)) { text = record.DisplayName + " · " + record.Key });
            }
        }
    }
}
