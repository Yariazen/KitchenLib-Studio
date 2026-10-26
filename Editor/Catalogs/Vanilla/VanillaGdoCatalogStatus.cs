namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal enum VanillaGdoCatalogState { Ready, Missing, Malformed, IncompatibleSchema, DescriptorMismatch, StaleBuild, SavedInstallUnavailable }
    internal sealed class VanillaGdoCatalogStatus
    {
        public VanillaGdoCatalogStatus(VanillaGdoCatalogState state, string message) { State = state; Message = message; }
        public VanillaGdoCatalogState State { get; }
        public string Message { get; }
    }
}
