using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor.PackageManager;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal sealed class PlateUpMirrorGenerationResult
    {
        public string GeneratorVersion { get; internal set; }
        public string SourceFingerprint { get; internal set; }
        public string[] OutputFiles { get; internal set; }
        public int TypeCount { get; internal set; }
        public KitchenLibStudioMirrorState ToState() => new KitchenLibStudioMirrorState {
            generatorVersion = GeneratorVersion, sourceFingerprint = SourceFingerprint, complete = true, outputFiles = OutputFiles };
    }

    internal sealed class PlateUpMirrorGenerator
    {
        // Bump the format revision when generation changes. A package release also changes this identity.
        public static string Version => "3/" + (PackageInfo.FindForAssembly(typeof(PlateUpMirrorGenerator).Assembly)?.version ?? "development");
        private readonly CancellationToken _cancellation;
        private readonly string _templates;
        public PlateUpMirrorGenerator(CancellationToken cancellation = default, string templatesDirectory = null)
        { _cancellation = cancellation; _templates = templatesDirectory; }

        public PlateUpMirrorGenerationResult Generate(PlateUpInstallation installation, string stagingRoot)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            AtomicGeneratedDirectory.ValidatePaths(stagingRoot, KitchenLibStudioGeneratedPaths.AssetDirectory);
            _cancellation.ThrowIfCancellationRequested();
            if (Directory.Exists(stagingRoot) && Directory.EnumerateFileSystemEntries(stagingRoot).Any())
                throw new IOException("Mirror staging directory must be empty: " + stagingRoot);
            string templates = _templates;
            if (templates == null)
            {
                PackageInfo package = PackageInfo.FindForAssembly(typeof(PlateUpMirrorGenerator).Assembly);
                if (package == null) throw new InvalidOperationException("KitchenLib Studio must be installed as a Unity package before generating mirrors.");
                templates = Path.Combine(package.resolvedPath, "Editor", "Generation", "Templates~");
            }
            try
            {
                using (PlateUpMirrorModel model = PlateUpMirrorModel.Read(Path.Combine(installation.ManagedDirectory, "Kitchen.GameData.dll"), installation.ManagedDirectory))
                {
                    _cancellation.ThrowIfCancellationRequested();
                    new PlateUpMirrorEmitter(templates).Emit(model, stagingRoot);
                    _cancellation.ThrowIfCancellationRequested();
#if UNITY_EDITOR
                    PlateUpMirrorCompilation.Validate(stagingRoot, _cancellation);
#endif
                    string[] files = ValidateOutput(stagingRoot);
                    return new PlateUpMirrorGenerationResult { GeneratorVersion = Version, SourceFingerprint = model.SourceFingerprint,
                        OutputFiles = files, TypeCount = model.Types.Count };
                }
            }
            catch { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); throw; }
        }

        internal static string[] ValidateOutput(string root)
        {
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => path.Substring(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] required = { "Kitchen.GameData.asmdef", "Sirenix/Sirenix.Serialization.asmdef", "Sirenix/SerializedScriptableObject.cs",
                "plateup-schema.json", "GdoSchemaRegistry.g.cs", "Linker/GdoSchemaRegistry.g.cs", "Linker/SupportedOverlaySchema.g.cs" };
            if (required.Any(path => !files.Contains(path)) || !files.Any(path => path.StartsWith("Types/", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal))
                || files.Any(path => new FileInfo(Path.Combine(root, path)).Length == 0))
                throw new InvalidDataException("Mirror output is incomplete. The previous generation has been preserved.");
            return files;
        }
    }

#if UNITY_EDITOR
    internal static class PlateUpMirrorCompilation
    {
        [Serializable] private sealed class AssemblyRecord { public string name; public string[] references; }

        internal static void Validate(string stagingRoot, CancellationToken cancellation)
        {
            string compilationRoot = Path.Combine(KitchenLibStudioGeneratedPaths.StateDirectory, "Compilation", Path.GetFileName(stagingRoot));
            Directory.CreateDirectory(compilationRoot);
            try
            {
                var definitions = Directory.GetFiles(stagingRoot, "*.asmdef", SearchOption.AllDirectories)
                    .ToDictionary(path => UnityEngine.JsonUtility.FromJson<AssemblyRecord>(File.ReadAllText(path)).name, path => path, StringComparer.Ordinal);
                var remaining = new System.Collections.Generic.SortedSet<string>(definitions.Keys, StringComparer.Ordinal);
                var compiled = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
                var platform = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player)
                    .ToDictionary(assembly => assembly.name, assembly => assembly.outputPath, StringComparer.Ordinal);
                while (remaining.Count != 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string name = remaining.FirstOrDefault(candidate => (UnityEngine.JsonUtility.FromJson<AssemblyRecord>(File.ReadAllText(definitions[candidate]))
                        .references ?? Array.Empty<string>()).All(reference => !definitions.ContainsKey(reference) || compiled.ContainsKey(reference)));
                    if (name == null) throw new InvalidDataException("Generated assembly references contain a cycle.");
                    AssemblyRecord definition = UnityEngine.JsonUtility.FromJson<AssemblyRecord>(File.ReadAllText(definitions[name]));
                    string root = Path.GetDirectoryName(definitions[name]);
                    string[] scripts = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                        .Where(path => !definitions.Values.Any(other => other != definitions[name]
                            && Path.GetDirectoryName(other).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                            && path.StartsWith(Path.GetDirectoryName(other) + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                        .OrderBy(path => path, StringComparer.Ordinal).ToArray();
                    string output = Path.Combine(compilationRoot, name, name + ".dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var builder = new UnityEditor.Compilation.AssemblyBuilder(output, scripts);
                    builder.excludeReferences = builder.defaultReferences.Where(path => definitions.ContainsKey(Path.GetFileNameWithoutExtension(path))).ToArray();
                    builder.additionalReferences = (definition.references ?? Array.Empty<string>()).Select(reference => {
                        if (compiled.TryGetValue(reference, out string dependency)) return dependency;
                        if (platform.TryGetValue(reference, out dependency) && File.Exists(dependency)) return dependency;
                        throw new InvalidDataException("Required Unity package assembly is unavailable: " + reference);
                    }).ToArray();
                    UnityEditor.Compilation.CompilerMessage[] messages = null;
                    builder.buildFinished += (path, result) => messages = result;
                    if (!builder.Build()) throw new InvalidOperationException("Unity is already compiling. Retry mirror generation after compilation finishes.");
                    // AssemblyBuilder.status polls its compilation task, including the completion callback.
                    // Wait for this compiler to close its files before honoring cancellation and removing staging.
                    while (builder.status != UnityEditor.Compilation.AssemblyBuilderStatus.Finished) Thread.Sleep(10);
                    cancellation.ThrowIfCancellationRequested();
                    if (messages == null || messages.Any(message => message.type == UnityEditor.Compilation.CompilerMessageType.Error))
                        throw new InvalidDataException("Staged mirror compilation failed for " + name + ":\n" + string.Join("\n",
                            (messages ?? Array.Empty<UnityEditor.Compilation.CompilerMessage>()).Where(message => message.type == UnityEditor.Compilation.CompilerMessageType.Error)
                                .Select(message => message.file + ":" + message.line + " " + message.message)));
                    if (!File.Exists(output)) throw new InvalidDataException("Unity produced no compiled mirror assembly: " + name);
                    compiled.Add(name, output);
                    remaining.Remove(name);
                }
            }
            finally
            {
                // Unity can retain its compiler log until the next editor update. It is disposable Library state.
                try { if (Directory.Exists(compilationRoot)) Directory.Delete(compilationRoot, true); }
                catch (IOException) { }
            }
        }
    }
#endif
}
