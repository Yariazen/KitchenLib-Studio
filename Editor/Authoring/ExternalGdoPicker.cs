using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Catalogs.Mods;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class ExternalGdoPicker : VisualElement
    {
        private const string AllTypes = "All types";
        private const string AllProviders = "All providers";
        private readonly string _expectedType;
        private readonly ExternalGdoCatalogRepository _repository;
        private readonly Action<ExternalGdoCatalogEntry> _selected;
        private readonly List<ExternalGdoCatalogEntry> _filtered = new List<ExternalGdoCatalogEntry>();
        private readonly ListView _results;
        private readonly VisualElement _manual;
        private readonly TextField _query;
        private readonly PopupField<string> _typeFilter;
        private readonly PopupField<string> _providerFilter;
        private readonly Label _resultCount;
        private readonly bool _showSelectionSummary;

        internal ExternalGdoPicker(string expectedType, ExternalGdoCatalogRepository repository,
            Action<ExternalGdoCatalogEntry> selected, bool showSelectionSummary = true)
        {
            _expectedType = expectedType;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _selected = selected;
            _showSelectionSummary = showSelectionSummary;
            name = "vanilla-gdo-picker";
            AddToClassList("kls-vanilla-gdo-picker");
            style.flexGrow = 1f;

            if (_showSelectionSummary)
            {
                Add(new Label("ALL PROVIDERS") { name = "vanilla-provenance" });
                Add(new Label("No vanilla GDO selected") { name = "vanilla-selection" });
            }
            _query = new TextField { name = "vanilla-search", label = "Search PlateUp GDOs" };
            _query.RegisterValueChangedCallback(evt => RefreshResults(evt.newValue));
            _query.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                IReadOnlyList<ExternalGdoCatalogEntry> matches = Search(_query.value);
                if (matches.Count > 0) Select(matches[0]);
                evt.StopPropagation();
            });
            Add(_query);

            _providerFilter = new PopupField<string>("Provider", new List<string> { AllProviders }, 0)
                { name = "external-provider-filter" };
            _providerFilter.RegisterValueChangedCallback(evt =>
            {
                SetProvider(ProviderKey(evt.newValue));
            });
            Add(_providerFilter);

            _typeFilter = new PopupField<string>("GDO type", new List<string> { AllTypes }, 0)
                { name = "vanilla-type-filter" };
            _typeFilter.formatSelectedValueCallback = FormatRuntimeType;
            _typeFilter.formatListItemCallback = FormatRuntimeType;
            _typeFilter.RegisterValueChangedCallback(evt =>
            {
                SelectedRuntimeType = string.Equals(evt.newValue, AllTypes, StringComparison.Ordinal)
                    ? null : evt.newValue;
                UpdateTypeFilterTooltip();
                RefreshResults(_query.value);
            });
            Add(_typeFilter);
            _resultCount = new Label("0 results") { name = "vanilla-result-count" };
            Add(_resultCount);

            _results = new ListView(_filtered, 46, MakeResultRow, BindResultRow)
                { name = "vanilla-results", selectionType = SelectionType.None };
            _results.style.minHeight = 220f;
            _results.style.maxHeight = 420f;
            _results.style.flexGrow = 1f;
            _results.style.marginTop = 6f;
            _results.style.marginBottom = 8f;
            Add(_results);

            var manualButton = new Button(() => SetManualExpanded(!ManualExpanded))
                { name = "enter-external-id-manually", text = "Advanced: Enter ID manually" };
            manualButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            Add(manualButton);
            _manual = new VisualElement { name = "manual-external-entry" };
            _manual.style.display = DisplayStyle.None;
            Add(_manual);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _repository.Changed -= RefreshCatalog;
                _repository.Changed += RefreshCatalog;
                RefreshCatalog();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _repository.Changed -= RefreshCatalog);
            RefreshCatalog();
        }

        public bool ManualExpanded { get; private set; }
        internal ExternalGdoCatalogEntry SelectedEntry { get; private set; }
        public string SelectedRuntimeType { get; private set; }
        public string SelectedProviderKey { get; private set; }
        public int ResultCount => _filtered.Count;
        public VisualElement ManualContent => _manual;
        public event Action<bool> ManualExpandedChanged;

        internal IReadOnlyList<ExternalGdoCatalogEntry> Search(string query) =>
            _repository.Search(query, _expectedType, SelectedRuntimeType, SelectedProviderKey);

        public void SetRuntimeType(string runtimeType)
        {
            string value = string.IsNullOrEmpty(runtimeType) ? AllTypes : runtimeType;
            if (!_typeFilter.choices.Contains(value))
                throw new ArgumentException("The runtime type is not available in this picker.", nameof(runtimeType));
            SelectedRuntimeType = string.Equals(value, AllTypes, StringComparison.Ordinal) ? null : value;
            _typeFilter.SetValueWithoutNotify(value);
            UpdateTypeFilterTooltip();
            RefreshResults(_query.value);
        }

        public void SetProvider(string providerKey)
        {
            ExternalGdoProvider provider = string.IsNullOrEmpty(providerKey) ? null :
                _repository.Providers.FirstOrDefault(item => item.Key == providerKey);
            if (!string.IsNullOrEmpty(providerKey) && provider == null)
                throw new ArgumentException("The provider is not available in this picker.", nameof(providerKey));
            SelectedProviderKey = provider?.Key;
            _providerFilter.SetValueWithoutNotify(provider == null ? AllProviders : ProviderLabel(provider));
            RefreshTypes();
            RefreshResults(_query.value);
        }

        internal void Select(ExternalGdoCatalogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (!string.IsNullOrEmpty(_expectedType) && !GdoDescriptorCatalog.IsAssignable(entry.RuntimeType, _expectedType))
                throw new ArgumentException("The selected GDO is not assignable to " + _expectedType + ".", nameof(entry));
            ShowSelection(entry);
            _selected?.Invoke(entry);
        }

        internal void ShowSelection(ExternalGdoCatalogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            SelectedEntry = entry;
            Label selection = this.Q<Label>("vanilla-selection");
            if (selection != null)
                selection.text = entry.DisplayName + " · " + entry.ProviderName + " · " + ShortName(entry.RuntimeType) + " · ID " + entry.RuntimeId;
            Label provenance = this.Q<Label>("vanilla-provenance");
            if (provenance != null) provenance.text = entry.ProviderKey == ExternalGdoCatalogRepository.VanillaProviderKey
                ? "VANILLA · PlateUp" : "IMPORTED · " + entry.ProviderName;
        }

        public void ClearSelection()
        {
            SelectedEntry = null;
            Label selection = this.Q<Label>("vanilla-selection");
            if (selection != null) selection.text = "No vanilla GDO selected";
        }

        public void SetManualExpanded(bool expanded)
        {
            ManualExpanded = expanded;
            _manual.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            ManualExpandedChanged?.Invoke(expanded);
        }

        public void FocusSearch() => _query.Focus();
        public void ApplyQuery(string query)
        {
            _query.SetValueWithoutNotify(query ?? string.Empty);
            RefreshResults(_query.value);
        }

        private void RefreshResults(string query)
        {
            _filtered.Clear();
            _filtered.AddRange(Search(query));
            _results.itemsSource = _filtered;
            _results.Refresh();
            _resultCount.text = _filtered.Count == 1 ? "1 result" : _filtered.Count + " results";
        }

        private void RefreshCatalog()
        {
            HelpBox previous = this.Q<HelpBox>("vanilla-catalog-status");
            previous?.RemoveFromHierarchy();
            if (_repository.Entries.Count == 0)
                Insert(Math.Max(0, childCount - 2), new HelpBox("No external GDO catalogues are available.", HelpBoxMessageType.Warning)
                    { name = "vanilla-catalog-status" });
            if (SelectedEntry != null)
            {
                ExternalGdoCatalogEntry refreshed = _repository.Find(SelectedEntry.ProviderKey, SelectedEntry.RuntimeId, SelectedEntry.RuntimeType);
                if (refreshed != null) ShowSelection(refreshed);
            }
            var providers = new List<string> { AllProviders };
            providers.AddRange(_repository.Providers.Select(ProviderLabel));
            _providerFilter.choices = providers;
            if (!_repository.Providers.Any(item => item.Key == SelectedProviderKey))
            { SelectedProviderKey = null; _providerFilter.SetValueWithoutNotify(AllProviders); }
            RefreshTypes();
            RefreshResults(_query.value);
        }

        private void RefreshTypes()
        {
            string selected = SelectedRuntimeType;
            var choices = new List<string> { AllTypes };
            choices.AddRange(_repository.RuntimeTypes(_expectedType, SelectedProviderKey));
            _typeFilter.choices = choices;
            if (string.IsNullOrEmpty(selected) || !choices.Contains(selected))
            {
                SelectedRuntimeType = null;
                _typeFilter.SetValueWithoutNotify(AllTypes);
            }
            else _typeFilter.SetValueWithoutNotify(selected);
            UpdateTypeFilterTooltip();
        }

        private string ProviderKey(string displayName) => _repository.Providers
            .FirstOrDefault(item => ProviderLabel(item) == displayName)?.Key;

        private string ProviderLabel(ExternalGdoProvider provider) =>
            _repository.Providers.Count(item => item.DisplayName == provider.DisplayName) > 1
                ? provider.DisplayName + " · " + provider.Key
                : provider.DisplayName;

        private VisualElement MakeResultRow()
        {
            var row = new Button { name = "vanilla-result" };
            row.clicked += () => SelectBoundRow(row);
            row.AddToClassList("kls-vanilla-result");
            row.style.height = 46f;
            row.style.flexDirection = FlexDirection.Column;
            row.style.alignItems = Align.FlexStart;
            row.style.justifyContent = Justify.Center;
            row.style.paddingLeft = 10f;
            var name = new Label { name = "vanilla-result-name" };
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            var type = new Label { name = "vanilla-result-type" };
            type.style.fontSize = 11f;
            type.style.opacity = 0.72f;
            row.Add(name);
            row.Add(type);
            return row;
        }

        private void SelectBoundRow(VisualElement row)
        {
            if (row?.userData is ExternalGdoCatalogEntry entry) Select(entry);
        }

        private void BindResultRow(VisualElement element, int index)
        {
            ExternalGdoCatalogEntry entry = _filtered[index];
            element.userData = entry;
            element.name = "vanilla-result-" + entry.RuntimeId;
            element.tooltip = entry.RuntimeType + "\nRuntime ID: " + entry.RuntimeId;
            element.Q<Label>("vanilla-result-name").text = entry.DisplayName;
            element.Q<Label>("vanilla-result-type").text = entry.ProviderName + " · " + ShortName(entry.RuntimeType);
        }

        private static string FormatRuntimeType(string value) =>
            string.Equals(value, AllTypes, StringComparison.Ordinal) ? AllTypes : ShortName(value);

        private void UpdateTypeFilterTooltip()
        {
            _typeFilter.tooltip = string.IsNullOrEmpty(SelectedRuntimeType)
                ? "Show every compatible GDO type" : SelectedRuntimeType;
        }

        internal static string ShortName(string value)
        {
            int index = value == null ? -1 : value.LastIndexOf('.');
            return index < 0 ? value ?? "GDO" : value.Substring(index + 1);
        }
    }
}
