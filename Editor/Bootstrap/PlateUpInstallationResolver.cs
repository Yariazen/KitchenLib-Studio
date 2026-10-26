using System;
using System.Collections.Generic;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal enum PlateUpInstallationSource { None, Saved, Standard, Discovered, Picker }

    internal sealed class PlateUpInstallationResolution
    {
        public PlateUpInstallationResolution(PlateUpInstallation installation, PlateUpInstallationSource source,
            IEnumerable<string> diagnostics)
        { Installation = installation; Source = source; Diagnostics = new List<string>(diagnostics ?? Array.Empty<string>()); }
        public PlateUpInstallation Installation { get; }
        public PlateUpInstallationSource Source { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    internal sealed class PlateUpInstallationResolver
    {
        private readonly IPlateUpCatalogPreferenceStore _store;
        private readonly Func<string, PlateUpInstallationValidation> _validate;
        private readonly Func<PlateUpDiscoveryResult> _discover;
        private readonly string _standardRoot;
        private readonly Func<string> _picker;

        public PlateUpInstallationResolver(IPlateUpCatalogPreferenceStore store,
            Func<string, PlateUpInstallationValidation> validate, Func<PlateUpDiscoveryResult> discover, Func<string> picker)
            : this(store, validate, discover, null, picker) { }

        public PlateUpInstallationResolver(IPlateUpCatalogPreferenceStore store,
            Func<string, PlateUpInstallationValidation> validate, Func<PlateUpDiscoveryResult> discover,
            string standardRoot, Func<string> picker)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
            _discover = discover ?? throw new ArgumentNullException(nameof(discover));
            _standardRoot = standardRoot; _picker = picker;
        }

        public PlateUpInstallationResolution Resolve(bool allowPicker)
        {
            var diagnostics = new List<string>();
            PlateUpInstallationResolution resolved = Validate(_store.Get(), PlateUpInstallationSource.Saved, diagnostics, false);
            if (resolved != null) return resolved;
            resolved = Validate(_standardRoot, PlateUpInstallationSource.Standard, diagnostics, true);
            if (resolved != null) return resolved;
            PlateUpDiscoveryResult discovered = _discover(); diagnostics.AddRange(discovered.Diagnostics);
            if (discovered.Installations.Count == 1)
            {
                _store.Set(discovered.Installations[0].GameRoot);
                return new PlateUpInstallationResolution(discovered.Installations[0], PlateUpInstallationSource.Discovered, diagnostics);
            }
            if (allowPicker && _picker != null)
            {
                string selected = _picker();
                resolved = Validate(selected, PlateUpInstallationSource.Picker, diagnostics, true);
                if (resolved != null) return resolved;
            }
            return new PlateUpInstallationResolution(null, PlateUpInstallationSource.None, diagnostics);
        }

        private PlateUpInstallationResolution Validate(string path, PlateUpInstallationSource source,
            ICollection<string> diagnostics, bool persist)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            PlateUpInstallationValidation validation = _validate(path); foreach (string value in validation.Diagnostics) diagnostics.Add(value);
            if (!validation.IsValid) return null;
            if (persist) _store.Set(validation.Installation.GameRoot);
            return new PlateUpInstallationResolution(validation.Installation, source, diagnostics);
        }
    }
}
