using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class CleanProjectProof
    {
        [Serializable]
        private sealed class Result
        {
            public bool succeeded;
            public bool imported;
            public bool compiled;
            public bool schemaGenerated;
            public bool representativeAuthored;
            public bool bundleBuilt;
            public bool zeroManagedBinaries;
            public string diagnostic;
            public string bundlePath;
        }

        public static void Run()
        {
            string reportPath = Environment.GetEnvironmentVariable("KLSTUDIO_CLEAN_REPORT");
            if (string.IsNullOrWhiteSpace(reportPath)) throw new InvalidOperationException("KLSTUDIO_CLEAN_REPORT is required.");
            var result = new Result { imported = true, compiled = true, diagnostic = string.Empty, bundlePath = string.Empty };
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                result.schemaGenerated = File.Exists(Path.Combine(Application.dataPath, "KitchenLibStudio", "Generated", "plateup-schema.json"));
                if (!result.schemaGenerated) throw new FileNotFoundException("Generated schema was not imported into the clean project.");

                GdoBundleGraphAsset authored = GraphFirstGdoProofAsset.CreateOrUpdate();
                result.representativeAuthored = authored.Nodes.Count(node => node.Kind == GdoGraphNodeKind.Local) == 3;
                BundleBuildResult build = GdoBundleGraphBuilder.Build(authored);
                if (!build.Succeeded) throw new InvalidOperationException(string.Join(Environment.NewLine, build.Issues.Select(issue => issue.ToString())));
                result.bundlePath = build.BundlePath;
                result.bundleBuilt = File.Exists(build.BundlePath);
                string packageRoot = Directory.GetParent(Directory.GetParent(build.BundlePath).FullName).FullName;
                result.zeroManagedBinaries = !Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories)
                    .Any(path => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase));
                result.succeeded = result.imported && result.compiled && result.schemaGenerated && result.representativeAuthored && result.bundleBuilt && result.zeroManagedBinaries;
                if (!result.succeeded) throw new InvalidOperationException("One or more clean-project proof stages did not succeed.");
            }
            catch (Exception exception)
            {
                result.succeeded = false;
                result.diagnostic = exception.ToString();
                Write(reportPath, result);
                throw;
            }
            Write(reportPath, result);
            Debug.Log("KitchenLib Studio clean-project proof succeeded: " + result.bundlePath);
        }

        private static void Write(string path, Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }
    }
}
