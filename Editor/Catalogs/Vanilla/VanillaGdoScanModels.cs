using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal enum VanillaGdoDiagnosticSeverity { Warning, Fatal }

    internal sealed class VanillaGdoScanProgress
    {
        public VanillaGdoScanProgress(string relativeAssetPath, int completedObjects, int totalObjects)
        {
            RelativeAssetPath = relativeAssetPath;
            CompletedObjects = completedObjects;
            TotalObjects = totalObjects;
        }

        public string RelativeAssetPath { get; }
        public int CompletedObjects { get; }
        public int TotalObjects { get; }
    }

    internal sealed class VanillaGdoScanEntry
    {
        public VanillaGdoScanEntry(int id, string name, string runtimeType, string assemblyName, string relativeAssetPath, long pathId)
        {
            Id = id;
            Name = name;
            RuntimeType = runtimeType;
            AssemblyName = assemblyName;
            RelativeAssetPath = relativeAssetPath;
            PathId = pathId;
        }

        public int Id { get; }
        public string Name { get; }
        public string RuntimeType { get; }
        public string AssemblyName { get; }
        public string RelativeAssetPath { get; }
        public long PathId { get; }
    }

    internal sealed class PlateUpAssetPointer
    {
        public PlateUpAssetPointer(string fieldPath, string targetRelativeAssetPath, long targetPathId)
            : this(fieldPath, targetRelativeAssetPath, targetPathId, null) { }

        private PlateUpAssetPointer(string fieldPath, string targetRelativeAssetPath, long targetPathId, string readError)
        {
            FieldPath = fieldPath;
            TargetRelativeAssetPath = targetRelativeAssetPath;
            TargetPathId = targetPathId;
            ReadError = readError;
        }

        public string FieldPath { get; }
        public string TargetRelativeAssetPath { get; }
        public long TargetPathId { get; }
        public string ReadError { get; }
        public static PlateUpAssetPointer Failed(string fieldPath, string readError)
            => new PlateUpAssetPointer(fieldPath, null, 0, readError);
    }

    internal sealed class VanillaGdoScanRelationship
    {
        public VanillaGdoScanRelationship(int sourceId, string sourceRuntimeType, int targetId,
            string targetRuntimeType, string fieldPath, string label)
        {
            SourceId = sourceId;
            SourceRuntimeType = sourceRuntimeType;
            TargetId = targetId;
            TargetRuntimeType = targetRuntimeType;
            FieldPath = fieldPath;
            Label = label;
        }

        public int SourceId { get; }
        public string SourceRuntimeType { get; }
        public int TargetId { get; }
        public string TargetRuntimeType { get; }
        public string FieldPath { get; }
        public string Label { get; }
    }

    internal sealed class VanillaGdoScanDiagnostic
    {
        public VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity severity, string code, string message, string relativeAssetPath = null, long pathId = 0)
        {
            Severity = severity;
            Code = code;
            Message = message;
            RelativeAssetPath = relativeAssetPath;
            PathId = pathId;
        }

        public VanillaGdoDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string RelativeAssetPath { get; }
        public long PathId { get; }
    }

    internal sealed class VanillaGdoScanResult
    {
        public VanillaGdoScanResult(IEnumerable<VanillaGdoScanEntry> entries,
            IEnumerable<VanillaGdoScanDiagnostic> diagnostics,
            IEnumerable<VanillaGdoScanRelationship> relationships = null)
        {
            Entries = (entries ?? Enumerable.Empty<VanillaGdoScanEntry>()).ToArray();
            Relationships = (relationships ?? Enumerable.Empty<VanillaGdoScanRelationship>()).ToArray();
            Diagnostics = (diagnostics ?? Enumerable.Empty<VanillaGdoScanDiagnostic>())
                .OrderBy(item => item.RelativeAssetPath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.PathId)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<VanillaGdoScanEntry> Entries { get; }
        public IReadOnlyList<VanillaGdoScanRelationship> Relationships { get; }
        public IReadOnlyList<VanillaGdoScanDiagnostic> Diagnostics { get; }
        public bool HasFatalDiagnostics => Diagnostics.Any(item => item.Severity == VanillaGdoDiagnosticSeverity.Fatal);
    }

    internal sealed class PlateUpAssetObject
    {
        private PlateUpAssetObject(string assemblyName, string runtimeType, string name, int id,
            string relativeAssetPath, long pathId, string readError, IEnumerable<PlateUpAssetPointer> pointers)
        {
            AssemblyName = assemblyName;
            RuntimeType = runtimeType;
            Name = name;
            Id = id;
            RelativeAssetPath = relativeAssetPath;
            PathId = pathId;
            ReadError = readError;
            Pointers = (pointers ?? Enumerable.Empty<PlateUpAssetPointer>()).ToArray();
        }

        public string AssemblyName { get; }
        public string RuntimeType { get; }
        public string Name { get; }
        public int Id { get; }
        public string RelativeAssetPath { get; }
        public long PathId { get; }
        public string ReadError { get; }
        public IReadOnlyList<PlateUpAssetPointer> Pointers { get; }
        public bool ScriptResolved => !string.IsNullOrEmpty(AssemblyName) && !string.IsNullOrEmpty(RuntimeType);

        public static PlateUpAssetObject Resolved(string assemblyName, string runtimeType, string name, int id,
            string relativeAssetPath, long pathId, IEnumerable<PlateUpAssetPointer> pointers = null)
            => new PlateUpAssetObject(assemblyName, runtimeType, name, id, relativeAssetPath, pathId, null, pointers);

        public static PlateUpAssetObject Failed(string assemblyName, string runtimeType, string relativeAssetPath, long pathId, string error)
            => new PlateUpAssetObject(assemblyName, runtimeType, null, 0, relativeAssetPath, pathId, error, null);

        public static PlateUpAssetObject Unresolved(string relativeAssetPath, long pathId, string error)
            => new PlateUpAssetObject(null, null, null, 0, relativeAssetPath, pathId, error, null);
    }
}
