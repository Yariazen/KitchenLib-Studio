using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class GdoOverlayField
    {
        [SerializeField] private string _declaringType;
        [SerializeField] private string _fieldName;
        [SerializeField] private GdoOverlayValue _value;
        public string DeclaringType => _declaringType;
        public string FieldName => _fieldName;
        public GdoOverlayValue Value => _value;
        public GdoOverlayField(string declaringType, string fieldName, GdoOverlayValue value)
        { _declaringType = declaringType; _fieldName = fieldName; _value = value; }
    }

    [Serializable]
    public sealed class GdoOverlaySerializedField
    {
        [SerializeField] private string _declaringType;
        [SerializeField] private string _fieldName;
        [SerializeField] private GdoOverlaySerializedGraph _graph;

        public GdoOverlaySerializedField(string declaringType, string fieldName, GdoOverlaySerializedGraph graph)
        {
            _declaringType = declaringType;
            _fieldName = fieldName;
            _graph = graph;
        }

        public string DeclaringType => _declaringType;
        public string FieldName => _fieldName;
        public GdoOverlaySerializedGraph Graph => _graph;
    }

    public sealed class GdoOverlayAuthoringAsset : ScriptableObject
    {
        [SerializeField] private GameDataObject _target;
        [SerializeField] private string _descriptorFingerprint;
        [SerializeField] private List<GdoOverlaySerializedField> _serializedFields = new List<GdoOverlaySerializedField>();
        public GameDataObject Target => _target;
        public string DescriptorFingerprint => _descriptorFingerprint;
        public IReadOnlyList<GdoOverlayField> Fields
        {
            get
            {
                if (!TryReadFields(out IReadOnlyList<GdoOverlayField> fields, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> diagnostics))
                    throw new InvalidOperationException("Overlay persistence is invalid: " + string.Join("; ", diagnostics.Select(value => value.ToString())));
                return fields;
            }
        }

        public void Initialise(GameDataObject target)
        {
            _target = target ? target : throw new ArgumentNullException(nameof(target));
            _descriptorFingerprint = GdoDescriptorCatalog.ForTarget(target).DescriptorFingerprint;
        }

        public void AcceptCurrentDescriptor()
        {
            if (!_target) throw new InvalidOperationException("Overlay companion has no target.");
            _descriptorFingerprint = GdoDescriptorCatalog.ForTarget(_target).DescriptorFingerprint;
        }

        public void SetField(string declaringType, string fieldName, GdoOverlayValue value)
        {
            if (_serializedFields.Any(field => field.DeclaringType == declaringType && field.FieldName == fieldName))
                throw new InvalidOperationException("An overlay value already exists for this field.");
            _serializedFields.Add(EncodeField(declaringType, fieldName, value));
        }

        public void UpsertField(string declaringType, string fieldName, GdoOverlayValue value)
        {
            if (string.IsNullOrWhiteSpace(declaringType)) throw new ArgumentException("Declaring type is required.", nameof(declaringType));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("Field name is required.", nameof(fieldName));
            if (value == null) throw new ArgumentNullException(nameof(value));
            int index = _serializedFields.FindIndex(field => field.DeclaringType == declaringType && field.FieldName == fieldName);
            GdoOverlaySerializedField replacement = EncodeField(declaringType, fieldName, value);
            if (index < 0) _serializedFields.Add(replacement); else _serializedFields[index] = replacement;
        }

        public bool TryGetField(string declaringType, string fieldName, out GdoOverlayField field)
        {
            GdoOverlaySerializedField stored = _serializedFields.FirstOrDefault(item => item.DeclaringType == declaringType && item.FieldName == fieldName);
            if (stored == null) { field = null; return false; }
            if (!stored.Graph.TryDecode(out GdoOverlayValue value, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> diagnostics))
                throw new InvalidOperationException("Overlay persistence is invalid: " + string.Join("; ", diagnostics.Select(item => item.ToString())));
            field = new GdoOverlayField(stored.DeclaringType, stored.FieldName, value);
            return true;
        }

        public bool RemoveField(string declaringType, string fieldName)
        {
            int index = _serializedFields.FindIndex(field => field.DeclaringType == declaringType && field.FieldName == fieldName);
            if (index < 0) return false;
            _serializedFields.RemoveAt(index);
            return true;
        }

        public bool TryReadFields(out IReadOnlyList<GdoOverlayField> fields, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> diagnostics)
        {
            var decoded = new List<GdoOverlayField>();
            var issues = new List<GdoOverlayPersistenceDiagnostic>();
            foreach (GdoOverlaySerializedField stored in _serializedFields ?? new List<GdoOverlaySerializedField>())
            {
                if (stored == null || stored.Graph == null)
                {
                    issues.Add(new GdoOverlayPersistenceDiagnostic("MissingGraph", "fields", "Serialized field has no graph."));
                    continue;
                }
                if (!stored.Graph.TryDecode(out GdoOverlayValue value, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> graphIssues))
                {
                    foreach (GdoOverlayPersistenceDiagnostic issue in graphIssues)
                        issues.Add(new GdoOverlayPersistenceDiagnostic(issue.Category, stored.DeclaringType + "." + stored.FieldName + "." + issue.Path, issue.Message));
                    continue;
                }
                decoded.Add(new GdoOverlayField(stored.DeclaringType, stored.FieldName, value));
            }
            fields = decoded;
            diagnostics = issues;
            return issues.Count == 0;
        }

        private static GdoOverlaySerializedField EncodeField(string declaringType, string fieldName, GdoOverlayValue value)
        {
            if (string.IsNullOrWhiteSpace(declaringType)) throw new ArgumentException("Declaring type is required.", nameof(declaringType));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("Field name is required.", nameof(fieldName));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (!GdoOverlayLimits.TryValidate(new[] { value }, out GdoOverlayPersistenceDiagnostic issue))
                throw new InvalidOperationException(issue.Category + " at " + issue.Path + ": " + issue.Message);
            return new GdoOverlaySerializedField(declaringType, fieldName, GdoOverlaySerializedGraph.Encode(value));
        }
    }
}
