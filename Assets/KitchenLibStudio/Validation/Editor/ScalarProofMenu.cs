using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class ScalarProofMenu
    {
        [MenuItem("KitchenLib Studio/Validation/Build Scalar Proof")]
        public static void BuildScalarProof()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string packageRoot = Path.Combine(projectRoot, "Build", "KitchenLibStudioScalarProof");
            string contentRoot = Path.Combine(packageRoot, "content");
            if (Directory.Exists(packageRoot)) Directory.Delete(packageRoot, true);
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                new[] { ScalarProofAsset.AssetPath }, contentRoot, "scalar-proof.assets"));
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Issues));
            IReadOnlyList<ValidationIssue> verification = BundleOutputVerifier.Verify(packageRoot, result.BundlePath, ExpectedGdo.ScalarProof);
            if (verification.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal))
                throw new InvalidOperationException(string.Join(Environment.NewLine, verification));
            Debug.Log("Built and verified scalar proof bundle: " + result.BundlePath);
        }
    }
}
