using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public static class VanillaGdoCatalogBuilder
    {
        public const string ImporterVersion = "1";

        public static VanillaGdoCatalogData Build(PlateUpInstallation installation, VanillaGdoScanResult scan, PlateUpSchemaFingerprint schema)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            if (scan == null) throw new ArgumentNullException(nameof(scan));
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (scan.HasFatalDiagnostics)
            {
                VanillaGdoScanDiagnostic[] fatal = scan.Diagnostics
                    .Where(item => item.Severity == VanillaGdoDiagnosticSeverity.Fatal).ToArray();
                string summary = string.Join("; ", fatal.Take(3).Select(item =>
                    item.Code + (string.IsNullOrEmpty(item.RelativeAssetPath) ? string.Empty :
                        " [" + Normalize(item.RelativeAssetPath) + (item.PathId == 0 ? string.Empty : "#" + item.PathId) + "]")));
                if (fatal.Length > 3) summary += "; +" + (fatal.Length - 3) + " more";
                throw new InvalidDataException("The asset scan contains fatal diagnostics and cannot be published: " + summary);
            }
            if (scan.Entries.Count == 0) throw new InvalidDataException("The asset scan found no vanilla GDOs and cannot replace the existing catalog.");

            VanillaGdoCatalogEntryData[] entries = scan.Entries
                .GroupBy(item => item.Id)
                .Select(BuildEntry)
                .OrderBy(item => item.runtimeType, StringComparer.Ordinal)
                .ThenBy(item => item.name, StringComparer.Ordinal)
                .ThenBy(item => item.id)
                .ToArray();

            string[] assetPaths = scan.Entries.Select(item => Normalize(item.RelativeAssetPath))
                .Concat(scan.Diagnostics.Where(item => !string.IsNullOrEmpty(item.RelativeAssetPath)).Select(item => Normalize(item.RelativeAssetPath)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var assets = new List<VanillaGdoCatalogAssetData>();
            foreach (string relativePath in assetPaths)
            {
                string fullPath = Path.Combine(installation.GameRoot, "PlateUp_Data", relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)) throw new InvalidDataException("Scanned asset file is missing: " + relativePath);
                assets.Add(new VanillaGdoCatalogAssetData { relativePath = relativePath, sha256 = PlateUpSchemaFingerprint.HashFile(fullPath) });
            }

            return new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion,
                providerId = "plateup",
                providerName = "PlateUp",
                steamAppId = 1599600,
                steamBuildId = installation.BuildId,
                importerVersion = ImporterVersion,
                unityVersion = schema.UnityVersion,
                sourceAssemblyName = schema.AssemblyName,
                sourceAssemblySha256 = schema.AssemblySha256,
                schemaFingerprint = schema.SchemaFingerprint,
                assets = assets.ToArray(),
                entries = entries,
                relationships = scan.Relationships
                    .GroupBy(item => item.SourceId + "\n" + item.SourceRuntimeType + "\n" + item.FieldPath +
                        "\n" + item.TargetId + "\n" + item.TargetRuntimeType, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(item => item.SourceId)
                    .ThenBy(item => item.SourceRuntimeType, StringComparer.Ordinal)
                    .ThenBy(item => item.FieldPath, StringComparer.Ordinal)
                    .ThenBy(item => item.TargetId)
                    .ThenBy(item => item.TargetRuntimeType, StringComparer.Ordinal)
                    .Select(item => new ExternalGdoRelationshipData
                    {
                        source = new ExternalGdoIdentityData
                        {
                            providerKey = ExternalGdoCatalogRepository.VanillaProviderKey,
                            runtimeId = item.SourceId,
                            runtimeType = item.SourceRuntimeType
                        },
                        target = new ExternalGdoIdentityData
                        {
                            providerKey = ExternalGdoCatalogRepository.VanillaProviderKey,
                            runtimeId = item.TargetId,
                            runtimeType = item.TargetRuntimeType
                        },
                        fieldPath = item.FieldPath,
                        label = item.Label
                    }).ToArray(),
                diagnostics = scan.Diagnostics.Select(item => new VanillaGdoCatalogDiagnosticData
                {
                    severity = item.Severity == VanillaGdoDiagnosticSeverity.Fatal ? "fatal" : "warning",
                    code = item.Code,
                    message = item.Message,
                    relativePath = Normalize(item.RelativeAssetPath),
                    pathId = item.PathId
                }).OrderBy(item => item.relativePath ?? string.Empty, StringComparer.Ordinal)
                  .ThenBy(item => item.pathId).ThenBy(item => item.code, StringComparer.Ordinal).ToArray()
            };
        }

        private static VanillaGdoCatalogEntryData BuildEntry(IGrouping<int, VanillaGdoScanEntry> group)
        {
            VanillaGdoScanEntry first = group.First();
            if (group.Any(item => !string.Equals(item.Name, first.Name, StringComparison.Ordinal) ||
                                  !string.Equals(item.RuntimeType, first.RuntimeType, StringComparison.Ordinal) ||
                                  !string.Equals(item.AssemblyName, first.AssemblyName, StringComparison.Ordinal)))
                throw new InvalidDataException("Conflicting GDO records share ID " + group.Key + ".");
            return new VanillaGdoCatalogEntryData
            {
                id = first.Id,
                name = first.Name,
                runtimeType = first.RuntimeType,
                assemblyName = first.AssemblyName,
                locations = group.Select(item => new VanillaGdoCatalogLocationData { relativePath = Normalize(item.RelativeAssetPath), pathId = item.PathId })
                    .GroupBy(item => item.relativePath + "\0" + item.pathId, StringComparer.Ordinal).Select(item => item.First())
                    .OrderBy(item => item.relativePath, StringComparer.Ordinal).ThenBy(item => item.pathId).ToArray()
            };
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string normalized = path.Replace('\\', '/');
            const string prefix = "PlateUp_Data/";
            return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? normalized.Substring(prefix.Length) : normalized;
        }
    }
}
