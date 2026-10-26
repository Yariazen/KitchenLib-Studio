using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class PlateUpCatalogImportServiceTests
    {
        [UnityTest]
        public IEnumerator SuccessRunsPipelineOnceAndPublishesCatalog()
        {
            var pipeline = new FakePipeline();
            var service = new PlateUpCatalogImportService(pipeline);
            Task<PlateUpCatalogImportResult> operation = service.ImportAsync(Installation(), "catalog.json", null, CancellationToken.None);
            while (!operation.IsCompleted) yield return null;
            PlateUpCatalogImportResult result = operation.Result;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(pipeline.ScanCalls, Is.EqualTo(1));
            Assert.That(pipeline.BuildCalls, Is.EqualTo(1));
            Assert.That(pipeline.PublishCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OnlyOneImportRunsAndCancellationNeverPublishes()
        {
            var pipeline = new FakePipeline { BlockScan = true };
            var service = new PlateUpCatalogImportService(pipeline);
            var cancellation = new CancellationTokenSource();
            Task<PlateUpCatalogImportResult> first = service.ImportAsync(Installation(), "first.json", null, cancellation.Token);
            Assert.That(pipeline.ScanEntered.Wait(2000), Is.True);

            Task<PlateUpCatalogImportResult> concurrentOperation = service.ImportAsync(Installation(), "second.json", null, CancellationToken.None);
            while (!concurrentOperation.IsCompleted) yield return null;
            PlateUpCatalogImportResult concurrent = concurrentOperation.Result;
            Assert.That(concurrent.Succeeded, Is.False);
            Assert.That(concurrent.Error, Does.Contain("already"));

            cancellation.Cancel();
            pipeline.ReleaseScan.Set();
            while (!first.IsCompleted) yield return null;
            PlateUpCatalogImportResult cancelled = first.Result;
            Assert.That(cancelled.Cancelled, Is.True);
            Assert.That(pipeline.PublishCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PipelineFailureDoesNotInvokePublication()
        {
            var pipeline = new FakePipeline { BuildError = new InvalidDataException("bad") };
            var service = new PlateUpCatalogImportService(pipeline);
            Task<PlateUpCatalogImportResult> operation = service.ImportAsync(Installation(), "catalog.json", null, CancellationToken.None);
            while (!operation.IsCompleted) yield return null;
            PlateUpCatalogImportResult result = operation.Result;
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("bad"));
            Assert.That(pipeline.PublishCalls, Is.Zero);
        }

        private static PlateUpInstallation Installation() => new PlateUpInstallation("root", null, null, null, null, null);

        private sealed class FakePipeline : IPlateUpCatalogImportPipeline
        {
            public int ScanCalls, BuildCalls, PublishCalls;
            public bool BlockScan;
            public Exception BuildError;
            public readonly ManualResetEventSlim ScanEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseScan = new ManualResetEventSlim();

            public VanillaGdoScanResult Scan(PlateUpInstallation installation, IProgress<VanillaGdoScanProgress> progress, CancellationToken token)
            {
                ScanCalls++;
                ScanEntered.Set();
                if (BlockScan) ReleaseScan.Wait(token);
                token.ThrowIfCancellationRequested();
                return new VanillaGdoScanResult(null, null);
            }

            public byte[] Build(PlateUpInstallation installation, VanillaGdoScanResult scan, CancellationToken token)
            {
                BuildCalls++;
                if (BuildError != null) throw BuildError;
                token.ThrowIfCancellationRequested();
                return new byte[] { 1 };
            }

            public void Publish(string destination, byte[] content, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                PublishCalls++;
            }
        }
    }
}
