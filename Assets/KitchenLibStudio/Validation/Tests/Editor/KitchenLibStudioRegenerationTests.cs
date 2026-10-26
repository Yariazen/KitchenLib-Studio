using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class KitchenLibStudioRegenerationTests
    {
        [Test]
        public void RegenerateAllRunsStagesInDependencyOrder()
        {
            var runner = new Runner(); var fileSystem = new Files();
            var regeneration = new KitchenLibStudioRegeneration("C:\\Project", runner, fileSystem);

            RegenerationResult result = regeneration.RegenerateAll(Installation(), CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(runner.Stages, Is.EqualTo(new[] { "mirrors", "gdo-catalogue", "snapshot-catalogue", "asset-refresh" }));
        }

        [Test]
        public void RegenerateAllStopsAtFirstFailedStage()
        {
            var runner = new Runner { FailStage = "snapshot-catalogue" };
            var regeneration = new KitchenLibStudioRegeneration("C:\\Project", runner, new Files());

            RegenerationResult result = regeneration.RegenerateAll(Installation(), CancellationToken.None);

            Assert.That(result.Succeeded, Is.False); Assert.That(result.FailedStage, Is.EqualTo("snapshot-catalogue"));
            Assert.That(runner.Stages, Is.EqualTo(new[] { "mirrors", "gdo-catalogue", "snapshot-catalogue" }));
        }

        [Test]
        public void CleanTargetsAreExplicitAndNeverIncludeModContentOrValidation()
        {
            IReadOnlyList<string> targets = KitchenLibStudioRegeneration.CleanTargets("C:\\Project");
            Assert.That(targets, Is.EqualTo(new[]
            {
                "C:\\Project\\KitchenLib Studio\\Assets\\KitchenLibStudio\\Generated",
                "C:\\Project\\KitchenLib Studio\\Assets\\KitchenLibStudio\\Generated.meta",
                "C:\\Project\\KitchenLib Studio\\Assets\\__KitchenLibStudioBuild",
                "C:\\Project\\KitchenLib Studio\\Assets\\__KitchenLibStudioBuild.meta",
                "C:\\Project\\KitchenLib Studio\\Library\\KitchenLibStudio\\Snapshots"
            }));
            Assert.That(targets, Has.None.Contains("ModContent").And.None.Contains("Validation"));
        }

        [Test]
        public void CleanAndRegenerateDeletesOnlyAllowlistThenRunsStages()
        {
            var files = new Files(); var runner = new Runner();
            var regeneration = new KitchenLibStudioRegeneration("C:\\Project", runner, files);

            RegenerationResult result = regeneration.CleanAndRegenerate(Installation(), CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(files.Deleted, Is.EqualTo(KitchenLibStudioRegeneration.CleanTargets("C:\\Project")));
            Assert.That(runner.Stages.First(), Is.EqualTo("mirrors"));
        }

        [Test]
        public void CleanAndRegenerateReportsCleanFailureWithoutStartingRegeneration()
        {
            var files = new Files { Fail = true }; var runner = new Runner();
            var regeneration = new KitchenLibStudioRegeneration("C:\\Project", runner, files);

            RegenerationResult result = regeneration.CleanAndRegenerate(Installation(), CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailedStage, Is.EqualTo("clean-generated-environment"));
            Assert.That(runner.Stages, Is.Empty);
        }

        private static PlateUpInstallation Installation() => new PlateUpInstallation("S:\\PlateUp", null, null, null, null, null);
        private sealed class Runner : IKitchenLibStudioRegenerationStageRunner
        {
            public readonly List<string> Stages = new List<string>(); public string FailStage;
            public void Run(string stage, PlateUpInstallation installation, CancellationToken token)
            { Stages.Add(stage); if (stage == FailStage) throw new InvalidOperationException("injected"); }
        }
        private sealed class Files : IKitchenLibStudioRegenerationFileSystem
        {
            public readonly List<string> Deleted = new List<string>(); public bool Fail;
            public void Delete(string path) { if (Fail) throw new IOException("locked"); Deleted.Add(path); }
        }
    }
}
