using System;
using System.IO;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public enum ModCatalogCaptureStatus { Pending, Succeeded, Failed, TimedOut, Cancelled }

    public sealed class ModCatalogCaptureSession : IDisposable
    {
        internal ModCatalogCaptureSession(string token, string requestPath, string responseDirectory,
            string destinationPath, string steamBuildId, DateTime startedUtc, TimeSpan timeout, IPlateUpProcess process)
        {
            CaptureToken = token; RequestPath = requestPath; ResponseDirectory = responseDirectory;
            DestinationPath = destinationPath; SteamBuildId = steamBuildId; StartedUtc = startedUtc;
            Timeout = timeout; Process = process; Status = ModCatalogCaptureStatus.Pending;
        }
        public string CaptureToken { get; }
        public string RequestPath { get; }
        public string ResponseDirectory { get; }
        public string ResponseCatalogPath => Path.Combine(ResponseDirectory, ModCatalogCaptureProtocol.ResponseCatalogName);
        public string CompletionMarkerPath => Path.Combine(ResponseDirectory, ModCatalogCaptureProtocol.CompletionMarkerName);
        public string DestinationPath { get; }
        public string SteamBuildId { get; }
        public DateTime StartedUtc { get; }
        public TimeSpan Timeout { get; }
        public ModCatalogCaptureStatus Status { get; internal set; }
        public string Error { get; internal set; }
        internal IPlateUpProcess Process { get; }
        public void Dispose() => Process.Dispose();
    }

    public sealed class ModCatalogCaptureController
    {
        private readonly IPlateUpProcessLauncher _launcher;
        private readonly WorkshopModDiscovery _workshopDiscovery;

        public ModCatalogCaptureController(IPlateUpProcessLauncher launcher, WorkshopModDiscovery workshopDiscovery = null)
        {
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _workshopDiscovery = workshopDiscovery ?? new WorkshopModDiscovery();
        }

        public ModCatalogCaptureSession Begin(PlateUpInstallation installation, string exchangeRoot,
            string destinationPath, DateTime startedUtc, TimeSpan timeout)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            string token = Guid.NewGuid().ToString("N");
            string responseDirectory = Path.Combine(Path.GetFullPath(exchangeRoot), ModCatalogCaptureProtocol.ResponseFolderName, token);
            string requestPath = Path.Combine(Path.GetFullPath(exchangeRoot), ModCatalogCaptureProtocol.RequestFolderName, token + ".json");
            WorkshopModDiscoveryResult workshop = _workshopDiscovery.Scan(installation);
            var request = new ModCatalogCaptureRequestData
            {
                schemaVersion = ModCatalogCaptureProtocol.SchemaVersion,
                captureToken = token,
                steamAppId = ModGdoCatalogFile.PlateUpSteamAppId,
                steamBuildId = installation.BuildId,
                responseDirectory = responseDirectory,
                workshopItems = workshop.Mods.Select(ToData).ToArray()
            };
            ModCatalogCaptureProtocol.WriteRequestAtomic(requestPath, request);
            IPlateUpProcess process = _launcher.Launch(Path.Combine(installation.GameRoot, "PlateUp.exe"));
            return new ModCatalogCaptureSession(token, requestPath, responseDirectory, destinationPath,
                installation.BuildId, startedUtc, timeout, process);
        }

        public ModCatalogCaptureStatus Poll(ModCatalogCaptureSession session, DateTime utcNow)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Status != ModCatalogCaptureStatus.Pending) return session.Status;
            if (File.Exists(session.CompletionMarkerPath)) return ImportCompleted(session);
            if (utcNow - session.StartedUtc >= session.Timeout)
                return Stop(session, ModCatalogCaptureStatus.TimedOut, "PlateUp did not complete the mod catalogue capture in time.", true);
            if (session.Process.HasExited)
                return Stop(session, ModCatalogCaptureStatus.Failed,
                    "PlateUp exited before completing the mod catalogue capture (exit code " + session.Process.ExitCode + ").", false);
            return session.Status;
        }

        public void Cancel(ModCatalogCaptureSession session)
        {
            if (session == null || session.Status != ModCatalogCaptureStatus.Pending) return;
            Stop(session, ModCatalogCaptureStatus.Cancelled, "Mod catalogue capture was cancelled.", true);
        }

        private static ModCatalogCaptureStatus ImportCompleted(ModCatalogCaptureSession session)
        {
            try
            {
                if (File.ReadAllText(session.CompletionMarkerPath).Trim() != session.CaptureToken)
                    return Stop(session, ModCatalogCaptureStatus.Failed, "The capture completion token does not match the request.", false);
                ModGdoCatalogData data = ModGdoCatalogFile.Read(session.ResponseCatalogPath);
                if (data.captureToken != session.CaptureToken || data.steamBuildId != session.SteamBuildId)
                    return Stop(session, ModCatalogCaptureStatus.Failed, "The captured catalogue does not match the active request.", false);
                ModGdoCatalogFile.PublishAtomic(session.DestinationPath, ModGdoCatalogFile.Serialize(data));
                session.Status = ModCatalogCaptureStatus.Succeeded;
                return session.Status;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
            { return Stop(session, ModCatalogCaptureStatus.Failed, exception.Message, false); }
        }

        private static ModCatalogCaptureStatus Stop(ModCatalogCaptureSession session, ModCatalogCaptureStatus status,
            string error, bool terminate)
        {
            if (terminate && !session.Process.HasExited) session.Process.Terminate();
            session.Status = status; session.Error = error; return status;
        }

        private static ModCatalogCaptureWorkshopItemData ToData(WorkshopModRecord item) => new ModCatalogCaptureWorkshopItemData
        {
            workshopId = item.WorkshopId,
            providerKey = item.ProviderKey,
            assemblies = item.Assemblies.Select(ToData).ToArray(),
            assetBundles = item.AssetBundles.Select(ToData).ToArray()
        };

        private static ModCatalogCaptureFileData ToData(WorkshopModFileRecord file) => new ModCatalogCaptureFileData
        { relativePath = file.RelativePath, length = file.Length, sha256 = file.Sha256 };
    }
}
