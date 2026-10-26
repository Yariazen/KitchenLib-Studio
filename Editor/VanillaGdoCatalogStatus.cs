namespace KitchenLib.Studio.Editor
{
    public enum VanillaGdoCatalogState { Ready, Missing, Malformed, IncompatibleSchema, DescriptorMismatch, StaleBuild, SavedInstallUnavailable }
    public sealed class VanillaGdoCatalogStatus
    {
        public VanillaGdoCatalogStatus(VanillaGdoCatalogState state, string message) { State = state; Message = message; }
        public VanillaGdoCatalogState State { get; }
        public string Message { get; }
    }
}
