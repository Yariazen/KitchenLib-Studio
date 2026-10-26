using System;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal interface IPlateUpCatalogPreferenceStore { string Get(); void Set(string value); void Delete(); }

    internal sealed class PlateUpCatalogInstallationSettings
    {
        private readonly IPlateUpCatalogPreferenceStore _store;
        private readonly Func<string, PlateUpInstallationValidation> _validate;
        public PlateUpCatalogInstallationSettings(IPlateUpCatalogPreferenceStore store, Func<string, PlateUpInstallationValidation> validate)
        { _store = store; _validate = validate; }
        public string SavedRoot => _store.Get();
        public bool TrySave(string path)
        {
            PlateUpInstallationValidation result = _validate(path);
            if (!result.IsValid) return false;
            _store.Set(result.Installation.GameRoot);
            return true;
        }
        public void Clear() => _store.Delete();
    }

}
