using System.Collections.Generic;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class PlateUpInstallation
    {
        public PlateUpInstallation(string gameRoot, string steamRoot, string libraryRoot, string workshopRoot, string manifestPath, string buildId)
            : this(PlateUpPlatformLayout.ResolveLayout(gameRoot), steamRoot, libraryRoot, workshopRoot, manifestPath, buildId) { }

        internal PlateUpInstallation(PlateUpPlatformLayout layout, string steamRoot, string libraryRoot, string workshopRoot, string manifestPath, string buildId)
        {
            GameRoot = layout.GameRoot;
            DataRoot = layout.DataRoot;
            ManagedDirectory = layout.ManagedDirectory;
            ExecutablePath = layout.ExecutablePath;
            SteamRoot = steamRoot;
            LibraryRoot = libraryRoot;
            WorkshopRoot = workshopRoot;
            ManifestPath = manifestPath;
            BuildId = buildId;
        }

        public string GameRoot { get; }
        public string DataRoot { get; }
        public string ManagedDirectory { get; }
        public string ExecutablePath { get; }
        public string SteamRoot { get; }
        public string LibraryRoot { get; }
        public string WorkshopRoot { get; }
        public string ManifestPath { get; }
        public string BuildId { get; }
    }

    public sealed class PlateUpDiscoveryResult
    {
        public PlateUpDiscoveryResult(IEnumerable<PlateUpInstallation> installations, IEnumerable<string> diagnostics)
        {
            Installations = installations.ToArray();
            Diagnostics = diagnostics.ToArray();
        }

        public IReadOnlyList<PlateUpInstallation> Installations { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool RequiresManualSelection => Installations.Count != 1;
    }

    public sealed class PlateUpInstallationValidation
    {
        public PlateUpInstallationValidation(PlateUpInstallation installation, IEnumerable<string> diagnostics)
        {
            Installation = installation;
            Diagnostics = diagnostics.ToArray();
        }

        public PlateUpInstallation Installation { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsValid => Installation != null;
    }
}
