using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class ScalarProofInstaller
    {
        public static void InstallFromEnvironmentForBatch()
        {
            string plateUpRoot = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(plateUpRoot))
            {
                throw new InvalidOperationException("KITCHENLIB_STUDIO_PLATEUP_ROOT must point to the PlateUp installation directory.");
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string packageRoot = Path.Combine(projectRoot, "Build", "KitchenLibStudioScalarProof");
            string target = Install(packageRoot, plateUpRoot);
            Debug.Log("Installed scalar proof package: " + target);
        }

        public static string Install(string packageRoot, string plateUpRoot)
        {
            string bundlePath = Path.Combine(packageRoot, "content", "scalar-proof.assets");
            IReadOnlyList<ValidationIssue> issues = BundleOutputVerifier.Verify(packageRoot, bundlePath, ExpectedGdo.ScalarProof);
            if (issues.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal))
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
            }

            string modsRoot = Path.GetFullPath(Path.Combine(plateUpRoot, "Mods"));
            string target = Path.GetFullPath(Path.Combine(modsRoot, "KitchenLibStudioScalarProof"));
            string requiredPrefix = modsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Resolved mod installation target escaped the PlateUp Mods directory.");
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            foreach (string source in Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(packageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
            }

            return target;
        }
    }
}
