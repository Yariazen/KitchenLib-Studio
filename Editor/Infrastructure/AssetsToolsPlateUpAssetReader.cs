using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;
using KitchenLib.Studio.Editor.Catalogs.Vanilla;

using KitchenLib.Studio.Editor.Authoring;

namespace KitchenLib.Studio.Editor.Infrastructure
{
    internal sealed class AssetsToolsPlateUpAssetReader : IPlateUpAssetReader
    {
        private readonly IPlateUpAssetReaderBackend _backend;

        public AssetsToolsPlateUpAssetReader() : this(new AssetsToolsPlateUpAssetReaderBackend(
            KitchenLibStudioGeneratedPaths.PackageFilePath("Editor/ThirdParty/AssetRipper.TypeTreeData/classdata.tpk"))) { }

        public AssetsToolsPlateUpAssetReader(IPlateUpAssetReaderBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public VanillaGdoScanResult Scan(string gameRoot, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken)
        {
            string managedAssembly = Path.Combine(PlateUpPlatformLayout.ResolveLayout(gameRoot).ManagedDirectory, "Kitchen.GameData.dll");
            if (!File.Exists(managedAssembly))
            {
                return new VanillaGdoScanResult(Array.Empty<VanillaGdoScanEntry>(), new[]
                {
                    new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Fatal, "missing-managed-assembly", "Kitchen.GameData.dll is missing from the selected installation's Managed directory.")
                });
            }

            var entries = new List<VanillaGdoScanEntry>();
            var diagnostics = new List<VanillaGdoScanDiagnostic>();
            var resolvedGdos = new List<PlateUpAssetObject>();
            foreach (PlateUpAssetObject item in _backend.Read(gameRoot, progress, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!item.ScriptResolved)
                {
                    diagnostics.Add(new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Warning, "unresolved-script", item.ReadError ?? "MonoScript could not be resolved.", item.RelativeAssetPath, item.PathId));
                    continue;
                }

                GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForRuntimeType(item.RuntimeType);
                bool knownGdo = string.Equals(item.AssemblyName, "Kitchen.GameData", StringComparison.Ordinal) &&
                    descriptor != null && GdoDescriptorCatalog.IsAssignable(item.RuntimeType, "KitchenData.GameDataObject");
                if (!knownGdo) continue;

                if (!string.IsNullOrEmpty(item.ReadError) || string.IsNullOrEmpty(item.Name) || item.Id == 0)
                {
                    diagnostics.Add(new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Fatal, "gdo-parse-failed", item.ReadError ?? "GDO identity fields are missing.", item.RelativeAssetPath, item.PathId));
                    continue;
                }

                entries.Add(new VanillaGdoScanEntry(item.Id, item.Name, item.RuntimeType, item.AssemblyName, item.RelativeAssetPath, item.PathId));
                resolvedGdos.Add(item);
            }

            var byLocation = resolvedGdos.ToDictionary(item => Location(item.RelativeAssetPath, item.PathId),
                StringComparer.OrdinalIgnoreCase);
            var relationships = new List<VanillaGdoScanRelationship>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlateUpAssetObject source in resolvedGdos)
            foreach (PlateUpAssetPointer pointer in source.Pointers)
            {
                if (!string.IsNullOrEmpty(pointer.ReadError))
                {
                    diagnostics.Add(new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Warning,
                        "relationship-pointer-failed", pointer.ReadError, source.RelativeAssetPath, source.PathId));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(pointer.FieldPath) || pointer.TargetPathId == 0 ||
                    !byLocation.TryGetValue(Location(pointer.TargetRelativeAssetPath, pointer.TargetPathId), out PlateUpAssetObject target))
                    continue;
                string row = source.Id + "\n" + source.RuntimeType + "\n" + pointer.FieldPath + "\n" +
                    target.Id + "\n" + target.RuntimeType;
                if (!seen.Add(row)) continue;
                relationships.Add(new VanillaGdoScanRelationship(source.Id, source.RuntimeType, target.Id,
                    target.RuntimeType, pointer.FieldPath, HumanLabel(pointer.FieldPath)));
            }

            return new VanillaGdoScanResult(entries, diagnostics, relationships
                .OrderBy(value => value.SourceId).ThenBy(value => value.SourceRuntimeType, StringComparer.Ordinal)
                .ThenBy(value => value.FieldPath, StringComparer.Ordinal)
                .ThenBy(value => value.TargetId).ThenBy(value => value.TargetRuntimeType, StringComparer.Ordinal));
        }

        private static string Location(string relativePath, long pathId) =>
            (relativePath ?? string.Empty).Replace('\\', '/') + "\n" + pathId;

        private static string HumanLabel(string path)
        {
            string value = path ?? string.Empty;
            int dot = value.LastIndexOf('.');
            if (dot >= 0) value = value.Substring(dot + 1);
            int bracket = value.IndexOf('[');
            if (bracket >= 0) value = value.Substring(0, bracket);
            return string.IsNullOrWhiteSpace(value) ? "Related" : value;
        }
    }

    internal sealed class AssetsToolsPlateUpAssetReaderBackend : IPlateUpAssetReaderBackend
    {
        private readonly string _classDataPath;

        public AssetsToolsPlateUpAssetReaderBackend(string classDataPath)
        {
            _classDataPath = classDataPath;
        }

        public IReadOnlyList<PlateUpAssetObject> Read(string gameRoot, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken)
        {
            PlateUpPlatformLayout layout = PlateUpPlatformLayout.ResolveLayout(gameRoot);
            string dataRoot = layout.DataRoot;
            string managedRoot = layout.ManagedDirectory;
            if (!File.Exists(_classDataPath))
                throw new FileNotFoundException("The editor-only AssetsTools.NET class database is missing.", _classDataPath);

            string[] assetPaths = Directory.GetFiles(dataRoot, "*.assets", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedAssetFile)
                .Concat(File.Exists(Path.Combine(dataRoot, "globalgamemanagers.assets"))
                    ? new[] { Path.Combine(dataRoot, "globalgamemanagers.assets") }
                    : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();

            var objects = new List<PlateUpAssetObject>();
            var manager = new AssetsManager();
            var monoGenerator = new MonoCecilTempGenerator(managedRoot);
            try
            {
                manager.LoadClassPackage(_classDataPath);
                manager.MonoTempGenerator = monoGenerator;
                foreach (string assetPath in assetPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadFile(manager, assetPath, progress, cancellationToken, objects);
                }
            }
            finally
            {
                manager.UnloadAll(true);
                monoGenerator.Dispose();
            }
            return objects;
        }

        private static bool IsSupportedAssetFile(string path)
        {
            string name = Path.GetFileName(path);
            return string.Equals(name, "resources.assets", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("sharedassets", StringComparison.OrdinalIgnoreCase);
        }

        private static void ReadFile(AssetsManager manager, string assetPath, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken, List<PlateUpAssetObject> output)
        {
            string relativePath = Path.GetFileName(assetPath);
            AssetsFileInstance instance = manager.LoadAssetsFile(assetPath, false);
            manager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);
            List<AssetFileInfo> infos = instance.file.GetAssetsOfType(AssetClassID.MonoBehaviour);
            for (int index = 0; index < infos.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AssetFileInfo info = infos[index];
                progress?.Report(new VanillaGdoScanProgress(relativePath, index, infos.Count));
                output.Add(ReadObject(manager, instance, info, relativePath));
            }
            manager.UnloadAssetsFile(instance);
        }

        private static PlateUpAssetObject ReadObject(AssetsManager manager, AssetsFileInstance instance, AssetFileInfo info, string relativePath)
        {
            string assemblyName = null;
            string runtimeType = null;
            try
            {
                ushort scriptIndex = info.GetScriptIndex(instance.file);
                if (scriptIndex == ushort.MaxValue || scriptIndex >= instance.file.Metadata.ScriptTypes.Count)
                    return PlateUpAssetObject.Unresolved(relativePath, info.PathId, "MonoScript index is unavailable.");

                AssetPPtr scriptPointer = instance.file.Metadata.ScriptTypes[scriptIndex];
                AssetExternal script = manager.GetExtAsset(instance, scriptPointer.FileId, scriptPointer.PathId, false, AssetReadFlags.None);
                if (script.info == null || script.baseField == null || script.baseField.IsDummy)
                    return PlateUpAssetObject.Unresolved(relativePath, info.PathId, "MonoScript reference could not be loaded.");

                string className = script.baseField["m_ClassName"].AsString;
                string typeNamespace = script.baseField["m_Namespace"].AsString;
                assemblyName = NormalizeAssemblyName(script.baseField["m_AssemblyName"].AsString);
                runtimeType = string.IsNullOrEmpty(typeNamespace) ? className : typeNamespace + "." + className;

                AssetTypeValueField fields = manager.GetBaseField(instance, info, AssetReadFlags.None);
                if (fields == null || fields.IsDummy || fields["m_Name"].IsDummy || fields["ID"].IsDummy)
                    return PlateUpAssetObject.Failed(assemblyName, runtimeType, relativePath, info.PathId, "GDO identity fields could not be read.");

                IReadOnlyList<PlateUpAssetPointer> pointers = VanillaGdoPointerCollector.Collect(
                    fields, relativePath, info.PathId, fileId => ResolveExternalFile(instance, fileId));
                return PlateUpAssetObject.Resolved(assemblyName, runtimeType, fields["m_Name"].AsString,
                    fields["ID"].AsInt, relativePath, info.PathId, pointers);
            }
            catch (Exception exception)
            {
                return assemblyName == null || runtimeType == null
                    ? PlateUpAssetObject.Unresolved(relativePath, info.PathId, exception.Message)
                    : PlateUpAssetObject.Failed(assemblyName, runtimeType, relativePath, info.PathId, exception.Message);
            }
        }

        private static string NormalizeAssemblyName(string value)
        {
            return value != null && value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - 4)
                : value;
        }

        private static string ResolveExternalFile(AssetsFileInstance instance, int fileId)
        {
            int index = fileId - 1;
            if (index < 0 || index >= instance.file.Metadata.Externals.Count) return null;
            AssetsFileExternal external = instance.file.Metadata.Externals[index];
            string path = string.IsNullOrWhiteSpace(external.PathName)
                ? external.OriginalPathName : external.PathName;
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path.Replace('\\', '/'));
        }
    }
}
