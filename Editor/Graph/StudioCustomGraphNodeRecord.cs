using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Graph;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Graph
{
    // Wire storage is deliberately independent of extension CLR types and API implementations.
    // Never normalize these lists on load: unknown discriminants, payloads and keys belong to the author.
    [Serializable]
    internal sealed class StudioCustomGraphFieldRecord
    {
        [SerializeField] private string _key;
        [SerializeField] private int _kind;
        [SerializeField] private string _payload;
        internal string Key => _key;
        internal static StudioCustomGraphFieldRecord Create(string key, StudioAuthoringValue value) =>
            new StudioCustomGraphFieldRecord { _key = key, _kind = (int)value.Kind, _payload = Encode(value) };

        internal bool TryRead(out StudioAuthoringValue value)
        {
            value = null;
            switch ((StudioAuthoringValueKind)_kind)
            {
                case StudioAuthoringValueKind.Null: value = StudioAuthoringValue.Null; break;
                case StudioAuthoringValueKind.Boolean:
                    if (!bool.TryParse(_payload, out bool flag)) return false;
                    value = StudioAuthoringValue.FromBoolean(flag); break;
                case StudioAuthoringValueKind.Integer:
                    if (!long.TryParse(_payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
                    value = StudioAuthoringValue.FromInteger(integer); break;
                case StudioAuthoringValueKind.Number:
                    if (!double.TryParse(_payload, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
                        double.IsNaN(number) || double.IsInfinity(number)) return false;
                    value = StudioAuthoringValue.FromNumber(number); break;
                case StudioAuthoringValueKind.String:
                    if (_payload == null) return false;
                    value = StudioAuthoringValue.FromString(_payload); break;
                case StudioAuthoringValueKind.Reference:
                    if (string.IsNullOrEmpty(_payload)) return false;
                    value = StudioAuthoringValue.FromReference(_payload); break;
                default: return false;
            }
            return true;
        }

        private static string Encode(StudioAuthoringValue value)
        {
            switch (value.Kind)
            {
                case StudioAuthoringValueKind.Boolean: return value.AsBoolean() ? "true" : "false";
                case StudioAuthoringValueKind.Integer: return value.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StudioAuthoringValueKind.Number: return value.AsNumber().ToString("R", CultureInfo.InvariantCulture);
                case StudioAuthoringValueKind.String: return value.AsString();
                case StudioAuthoringValueKind.Reference: return value.AsReference();
                default: return string.Empty;
            }
        }
    }

    [Serializable]
    internal sealed class StudioCustomGraphNodeRecord
    {
        [SerializeField] private string _id;
        [SerializeField] private string _typeId;
        [SerializeField] private Rect _position;
        [SerializeField] private List<StudioCustomGraphFieldRecord> _fields = new List<StudioCustomGraphFieldRecord>();

        public string Id => _id;
        public string TypeId => _typeId;
        public Rect Position => _position;
        internal IReadOnlyList<StudioCustomGraphFieldRecord> Fields => _fields.ToArray();
        internal void SetPosition(Rect value) => _position = value;
        internal static StudioCustomGraphNodeRecord Create(string id, StudioGraphNodeDefinition definition, Rect position) =>
            new StudioCustomGraphNodeRecord { _id = id, _typeId = definition.TypeId, _position = position,
                _fields = definition.Fields.Select(field => StudioCustomGraphFieldRecord.Create(field.Key, field.DefaultValue)).ToList() };

        internal void SetValue(string key, StudioAuthoringValue value)
        {
            int index = _fields.FindIndex(field => string.Equals(field.Key, key, StringComparison.Ordinal));
            var replacement = StudioCustomGraphFieldRecord.Create(key, value);
            if (index < 0) _fields.Add(replacement); else _fields[index] = replacement;
        }

        internal StudioGraphNodeData Snapshot(GdoBundleGraphAsset graph)
        {
            var values = new List<KeyValuePair<string, StudioAuthoringValue>>();
            foreach (StudioCustomGraphFieldRecord field in _fields)
            {
                if (field == null || !field.TryRead(out StudioAuthoringValue value))
                    throw new InvalidOperationException("A stored field has an unknown or invalid value encoding. Its raw data was retained.");
                values.Add(new KeyValuePair<string, StudioAuthoringValue>(field.Key, value));
            }
            return new StudioGraphNodeData(Id, TypeId, values, Connections(graph, Id));
        }

        internal static IEnumerable<StudioGraphConnection> Connections(GdoBundleGraphAsset graph, string id)
        {
            foreach (GdoGraphEdgeRecord edge in graph.Edges)
            {
                if (edge.InputNodeId == id) yield return new StudioGraphConnection(edge.InputPortPath,
                    new StudioGraphEndpoint(edge.OutputNodeId, edge.OutputPortPath));
                if (edge.OutputNodeId == id) yield return new StudioGraphConnection(edge.OutputPortPath,
                    new StudioGraphEndpoint(edge.InputNodeId, edge.InputPortPath));
            }
        }
    }
}
