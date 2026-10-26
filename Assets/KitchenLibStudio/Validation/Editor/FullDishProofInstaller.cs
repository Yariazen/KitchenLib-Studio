using System;
using System.IO;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class FullDishProofInstallResult
    {
        public FullDishProofInstallResult(string content, string linker, string validator) { ContentRoot = content; LinkerRoot = linker; ValidatorRoot = validator; }
        public string ContentRoot { get; } public string LinkerRoot { get; } public string ValidatorRoot { get; }
    }
    public static class FullDishProofInstaller
    {
        public static FullDishProofInstallResult Install(string packageRoot, string bundlePath, string linkerDll, string validatorDll, string plateUpRoot)
        {
            if (!File.Exists(linkerDll)) throw new FileNotFoundException("Shared linker DLL does not exist.", linkerDll);
            if (!File.Exists(validatorDll)) throw new FileNotFoundException("Validator DLL does not exist.", validatorDll);
            if (!Directory.Exists(packageRoot)) throw new DirectoryNotFoundException(packageRoot);
            var issues = BundleOutputVerifier.Verify(packageRoot, bundlePath, ExpectedGdo.FullDishProof);
            if (issues.Any(issue => issue.Severity >= ValidationSeverity.Error)) throw new InvalidOperationException(string.Join(Environment.NewLine, issues));
            string mods = Path.GetFullPath(Path.Combine(plateUpRoot, "Mods"));
            string content = Child(mods, "KitchenLibStudioFullDishProof"); string linker = Child(mods, "KitchenLibStudioLinker"); string validator = Child(mods, "KitchenLibStudioFullDishProofValidator");
            ReplaceFrom(content, packageRoot); Replace(linker); File.Copy(linkerDll, Path.Combine(linker, Path.GetFileName(linkerDll)), true); Replace(validator); File.Copy(validatorDll, Path.Combine(validator, Path.GetFileName(validatorDll)), true);
            return new FullDishProofInstallResult(content, linker, validator);
        }
        private static string Child(string root, string name) { string result = Path.GetFullPath(Path.Combine(root, name)); if (!result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Install path escaped Mods."); return result; }
        private static void ReplaceFrom(string destination, string source) { Replace(destination); foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { string target = Path.Combine(destination, file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, true); } }
        private static void Replace(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); Directory.CreateDirectory(path); }
    }
}
