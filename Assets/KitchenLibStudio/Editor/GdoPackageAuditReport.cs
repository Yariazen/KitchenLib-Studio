using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class GdoPackageAuditFinding
    {
        [SerializeField] private ValidationSeverity _severity;
        [SerializeField] private string _category;
        [SerializeField] private string _path;
        [SerializeField] private string _message;

        public GdoPackageAuditFinding(ValidationSeverity severity, string category, string path, string message)
        {
            _severity = severity;
            _category = category ?? string.Empty;
            _path = path ?? string.Empty;
            _message = message ?? string.Empty;
        }

        public ValidationSeverity Severity => _severity;
        public string Category => _category;
        public string Path => _path;
        public string Message => _message;
    }

    [Serializable]
    public sealed class GdoPackageAuditReport
    {
        [SerializeField] private int _managedBinaryCount;
        [SerializeField] private int _serializedScriptCount;
        [SerializeField] private List<GdoPackageAuditFinding> _findings;

        public GdoPackageAuditReport(int managedBinaryCount, int serializedScriptCount, IEnumerable<GdoPackageAuditFinding> findings)
        {
            _managedBinaryCount = managedBinaryCount;
            _serializedScriptCount = serializedScriptCount;
            _findings = (findings ?? Array.Empty<GdoPackageAuditFinding>())
                .OrderBy(finding => finding.Category, StringComparer.Ordinal)
                .ThenBy(finding => finding.Path, StringComparer.Ordinal)
                .ThenBy(finding => finding.Message, StringComparer.Ordinal)
                .ToList();
        }

        public int ManagedBinaryCount => _managedBinaryCount;
        public int SerializedScriptCount => _serializedScriptCount;
        public IReadOnlyList<GdoPackageAuditFinding> Findings => _findings;
        public bool HasFatalFindings => _findings.Any(finding => finding.Severity == ValidationSeverity.Fatal);
        public string ToJson() => JsonUtility.ToJson(this, true);
    }
}
