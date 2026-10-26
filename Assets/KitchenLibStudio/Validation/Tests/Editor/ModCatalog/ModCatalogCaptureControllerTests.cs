using System;
using System.IO;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ModCatalogCaptureControllerTests
    {
        private string _root;
        private string _destination;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "mod-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _destination = Path.Combine(_root, "published.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void BeginWritesRequestAndLaunchesPlateUpWithoutWaiting()
        {
            var launcher = new FakeLauncher();
            var controller = new ModCatalogCaptureController(launcher);

            ModCatalogCaptureSession session = controller.Begin(Installation(), _root, _destination,
                new DateTime(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(2));

            Assert.That(File.Exists(session.RequestPath), Is.True);
            Assert.That(launcher.ExecutablePath, Is.EqualTo(Path.Combine(Installation().GameRoot, "PlateUp.exe")));
            Assert.That(session.Status, Is.EqualTo(ModCatalogCaptureStatus.Pending));
        }

        [Test]
        public void PollPublishesMatchingCompletedResponse()
        {
            var controller = new ModCatalogCaptureController(new FakeLauncher());
            DateTime started = new DateTime(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);
            ModCatalogCaptureSession session = controller.Begin(Installation(), _root, _destination, started, TimeSpan.FromMinutes(2));
            Directory.CreateDirectory(session.ResponseDirectory);
            File.WriteAllBytes(session.ResponseCatalogPath, ModGdoCatalogFile.Serialize(Catalog(session.CaptureToken)));
            File.WriteAllText(session.CompletionMarkerPath, session.CaptureToken);

            ModCatalogCaptureStatus status = controller.Poll(session, started.AddSeconds(1));

            Assert.That(status, Is.EqualTo(ModCatalogCaptureStatus.Succeeded));
            Assert.That(ModGdoCatalogFile.Read(_destination).captureToken, Is.EqualTo(session.CaptureToken));
        }

        [Test]
        public void TimeoutProcessFailureAndStaleResponsePreservePreviousCache()
        {
            byte[] previous = ModGdoCatalogFile.Serialize(Catalog("old"));
            File.WriteAllBytes(_destination, previous);
            DateTime started = new DateTime(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);

            var timeoutController = new ModCatalogCaptureController(new FakeLauncher());
            ModCatalogCaptureSession timeout = timeoutController.Begin(Installation(), _root, _destination, started, TimeSpan.FromSeconds(1));
            Assert.That(timeoutController.Poll(timeout, started.AddSeconds(2)), Is.EqualTo(ModCatalogCaptureStatus.TimedOut));
            Assert.That(File.ReadAllBytes(_destination), Is.EqualTo(previous));

            var failedProcess = new FakeProcess { HasExitedValue = true, ExitCodeValue = 17 };
            var failureController = new ModCatalogCaptureController(new FakeLauncher(failedProcess));
            ModCatalogCaptureSession failure = failureController.Begin(Installation(), _root, _destination, started, TimeSpan.FromMinutes(1));
            Assert.That(failureController.Poll(failure, started), Is.EqualTo(ModCatalogCaptureStatus.Failed));

            var staleController = new ModCatalogCaptureController(new FakeLauncher());
            ModCatalogCaptureSession stale = staleController.Begin(Installation(), _root, _destination, started, TimeSpan.FromMinutes(1));
            Directory.CreateDirectory(stale.ResponseDirectory);
            File.WriteAllBytes(stale.ResponseCatalogPath, ModGdoCatalogFile.Serialize(Catalog("wrong")));
            File.WriteAllText(stale.CompletionMarkerPath, stale.CaptureToken);
            Assert.That(staleController.Poll(stale, started), Is.EqualTo(ModCatalogCaptureStatus.Failed));
            Assert.That(File.ReadAllBytes(_destination), Is.EqualTo(previous));
        }

        [Test]
        public void CancelTerminatesLaunchedProcessAndDoesNotPublish()
        {
            var process = new FakeProcess();
            var controller = new ModCatalogCaptureController(new FakeLauncher(process));
            ModCatalogCaptureSession session = controller.Begin(Installation(), _root, _destination,
                DateTime.UtcNow, TimeSpan.FromMinutes(1));

            controller.Cancel(session);

            Assert.That(session.Status, Is.EqualTo(ModCatalogCaptureStatus.Cancelled));
            Assert.That(process.Terminated, Is.True);
            Assert.That(File.Exists(_destination), Is.False);
        }

        private PlateUpInstallation Installation()
        {
            string game = Path.Combine(_root, "PlateUp");
            Directory.CreateDirectory(game);
            File.WriteAllBytes(Path.Combine(game, "PlateUp.exe"), new byte[0]);
            return new PlateUpInstallation(game, null, null, null, null, "build-1");
        }

        private static ModGdoCatalogData Catalog(string token) => new ModGdoCatalogData
        {
            schemaVersion = ModGdoCatalogFile.SchemaVersion, captureToken = token, steamAppId = 1599600, steamBuildId = "build-1", bridgeVersion = "2",
            providers = new ModGdoProviderData[0], entries = new ModGdoEntryData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new ModGdoCatalogDiagnosticData[0]
        };

        private sealed class FakeLauncher : IPlateUpProcessLauncher
        {
            private readonly FakeProcess _process;
            public FakeLauncher(FakeProcess process = null) { _process = process ?? new FakeProcess(); }
            public string ExecutablePath { get; private set; }
            public IPlateUpProcess Launch(string executablePath) { ExecutablePath = executablePath; return _process; }
        }

        private sealed class FakeProcess : IPlateUpProcess
        {
            public bool HasExitedValue;
            public int ExitCodeValue;
            public bool Terminated;
            public bool HasExited => HasExitedValue;
            public int ExitCode => ExitCodeValue;
            public void Terminate() { Terminated = true; HasExitedValue = true; }
            public void Dispose() { }
        }
    }
}
