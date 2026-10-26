namespace KitchenLib.Studio.Editor
{
    public sealed class GdoOverlayPersistenceDiagnostic
    {
        public GdoOverlayPersistenceDiagnostic(string category, string path, string message)
        {
            Category = category ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Category { get; }
        public string Path { get; }
        public string Message { get; }

        public override string ToString() => Category + ": " + Message + " (" + Path + ")";
    }
}
