using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class GdoOverlaySerializedMember
    {
        [SerializeField] private string _name;
        [SerializeField] private int _valueNodeId;

        public GdoOverlaySerializedMember(string name, int valueNodeId)
        {
            _name = name ?? string.Empty;
            _valueNodeId = valueNodeId;
        }

        public string Name => _name;
        public int ValueNodeId => _valueNodeId;
    }

    [Serializable]
    public sealed class GdoOverlaySerializedNode
    {
        [SerializeField] private int _id;
        [SerializeField] private GdoOverlayValueKind _kind;
        [SerializeField] private string _scalar;
        [SerializeField] private bool _hasScalar;
        [SerializeField] private GdoReferenceToken _reference;
        [SerializeField] private bool _hasReference;
        [SerializeField] private List<GdoOverlaySerializedMember> _members = new List<GdoOverlaySerializedMember>();
        [SerializeField] private List<int> _valueNodeIds = new List<int>();
        [SerializeField] private List<GdoLocaleEntry> _locales = new List<GdoLocaleEntry>();

        private GdoOverlaySerializedNode() { }

        public int Id => _id;
        public GdoOverlayValueKind Kind => _kind;
        public string Scalar => _scalar;
        public bool HasScalar => _hasScalar;
        public GdoReferenceToken Reference => _reference;
        public bool HasReference => _hasReference;
        public IReadOnlyList<GdoOverlaySerializedMember> Members => _members;
        public IReadOnlyList<int> ValueNodeIds => _valueNodeIds;
        public IReadOnlyList<GdoLocaleEntry> Locales => _locales;

        public static GdoOverlaySerializedNode Null(int id) => Create(id, GdoOverlayValueKind.Null);

        public static GdoOverlaySerializedNode Collection(int id, GdoOverlayValueKind kind, IEnumerable<int> childIds)
        {
            if (kind != GdoOverlayValueKind.List && kind != GdoOverlayValueKind.Set)
                throw new ArgumentException("Collection node kind must be List or Set.", nameof(kind));
            GdoOverlaySerializedNode node = Create(id, kind);
            node._valueNodeIds = (childIds ?? throw new ArgumentNullException(nameof(childIds))).ToList();
            return node;
        }

        internal static GdoOverlaySerializedNode FromValue(
            int id,
            GdoOverlayValue value,
            IEnumerable<GdoOverlaySerializedMember> members,
            IEnumerable<int> childIds)
        {
            GdoOverlaySerializedNode node = Create(id, value.Kind);
            node._scalar = value.ScalarValue;
            node._hasScalar = value.Kind == GdoOverlayValueKind.Scalar || value.Kind == GdoOverlayValueKind.Enum || value.Kind == GdoOverlayValueKind.String;
            node._reference = value.ReferenceValue;
            node._hasReference = value.Kind == GdoOverlayValueKind.GdoReference;
            node._members = members?.ToList() ?? new List<GdoOverlaySerializedMember>();
            node._valueNodeIds = childIds?.ToList() ?? new List<int>();
            node._locales = value.Locales.ToList();
            return node;
        }

        private static GdoOverlaySerializedNode Create(int id, GdoOverlayValueKind kind) =>
            new GdoOverlaySerializedNode { _id = id, _kind = kind };
    }

    [Serializable]
    public sealed class GdoOverlaySerializedGraph
    {
        [SerializeField] private int _rootNodeId;
        [SerializeField] private List<GdoOverlaySerializedNode> _nodes = new List<GdoOverlaySerializedNode>();

        private GdoOverlaySerializedGraph() { }

        public int RootNodeId => _rootNodeId;
        public IReadOnlyList<GdoOverlaySerializedNode> Nodes => _nodes;

        public static GdoOverlaySerializedGraph Encode(GdoOverlayValue value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var nodes = new List<GdoOverlaySerializedNode>();
            int nextId = 1;
            int root = EncodeValue(value, nodes, ref nextId);
            return new GdoOverlaySerializedGraph { _rootNodeId = root, _nodes = nodes.OrderBy(node => node.Id).ToList() };
        }

        public bool TryDecode(out GdoOverlayValue value, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> diagnostics)
        {
            var issues = new List<GdoOverlayPersistenceDiagnostic>();
            value = null;
            if (_nodes == null)
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("MissingNodes", "root", "Serialized graph has no node table."));
                diagnostics = issues;
                return false;
            }

            if (_rootNodeId <= 0 || _nodes.Any(node => node != null && node.Id <= 0))
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("InvalidNodeId", "nodes", "Root and node IDs must be positive integers."));
                diagnostics = issues;
                return false;
            }
            if (_nodes.Count > GdoOverlayLimits.MaximumNodeCount)
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("MaximumNodeCountExceeded", "nodes",
                    "Serialized graph node count exceeds the maximum of " + GdoOverlayLimits.MaximumNodeCount + "."));
                diagnostics = issues;
                return false;
            }

            IGrouping<int, GdoOverlaySerializedNode> duplicate = _nodes.Where(node => node != null)
                .GroupBy(node => node.Id).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("DuplicateNodeId", "nodes", "Node ID " + duplicate.Key + " appears more than once."));
                diagnostics = issues;
                return false;
            }

            Dictionary<int, GdoOverlaySerializedNode> byId = _nodes.Where(node => node != null).ToDictionary(node => node.Id);
            var states = new Dictionary<int, byte>();
            var cache = new Dictionary<int, GdoOverlayValue>();
            if (!TryDecodeNode(_rootNodeId, "root", 0, byId, states, cache, issues, out value)) value = null;
            diagnostics = issues;
            return issues.Count == 0;
        }

        private static int EncodeValue(GdoOverlayValue value, ICollection<GdoOverlaySerializedNode> nodes, ref int nextId)
        {
            int id = nextId++;
            var members = new List<GdoOverlaySerializedMember>();
            var childIds = new List<int>();
            foreach (GdoOverlayMember member in value.Members.OrderBy(member => member.Name, StringComparer.Ordinal))
                members.Add(new GdoOverlaySerializedMember(member.Name, EncodeValue(member.Value, nodes, ref nextId)));
            foreach (GdoOverlayValue child in value.Values)
                childIds.Add(EncodeValue(child, nodes, ref nextId));
            nodes.Add(GdoOverlaySerializedNode.FromValue(id, value, members, childIds));
            return id;
        }

        private static bool TryDecodeNode(
            int id,
            string path,
            int depth,
            IReadOnlyDictionary<int, GdoOverlaySerializedNode> nodes,
            IDictionary<int, byte> states,
            IDictionary<int, GdoOverlayValue> cache,
            ICollection<GdoOverlayPersistenceDiagnostic> issues,
            out GdoOverlayValue value)
        {
            value = null;
            if (depth > GdoOverlayLimits.MaximumDepth)
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("MaximumDepthExceeded", path,
                    "Serialized graph depth exceeds the maximum of " + GdoOverlayLimits.MaximumDepth + "."));
                return false;
            }
            if (!nodes.TryGetValue(id, out GdoOverlaySerializedNode node))
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("MissingNode", path, "Node ID " + id + " does not exist."));
                return false;
            }

            if (!HasValidPayload(node))
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("InvalidNodePayload", path,
                    "Node ID " + id + " contains data that does not match kind " + node.Kind + "."));
                return false;
            }
            if (states.TryGetValue(id, out byte state))
            {
                if (state == 1)
                {
                    issues.Add(new GdoOverlayPersistenceDiagnostic("Cycle", path, "Node ID " + id + " creates a cycle."));
                    return false;
                }
                if (state == 2) { value = cache[id]; return true; }
            }

            states[id] = 1;
            try
            {
                switch (node.Kind)
                {
                    case GdoOverlayValueKind.Null: value = GdoOverlayValue.Null(); break;
                    case GdoOverlayValueKind.Scalar: value = GdoOverlayValue.Scalar(node.Scalar); break;
                    case GdoOverlayValueKind.Enum:
                        if (!long.TryParse(node.Scalar, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long number))
                            throw new InvalidOperationException("Enum scalar is invalid.");
                        value = GdoOverlayValue.Enum(number); break;
                    case GdoOverlayValueKind.String: value = GdoOverlayValue.String(node.Scalar); break;
                    case GdoOverlayValueKind.GdoReference: value = GdoOverlayValue.Reference(node.Reference); break;
                    case GdoOverlayValueKind.Struct:
                        var members = new List<GdoOverlayMember>();
                        foreach (GdoOverlaySerializedMember member in node.Members ?? Array.Empty<GdoOverlaySerializedMember>())
                        {
                            if (!TryDecodeNode(member.ValueNodeId, path + "." + member.Name, depth + 1, nodes, states, cache, issues, out GdoOverlayValue child)) return false;
                            members.Add(new GdoOverlayMember(member.Name, child));
                        }
                        value = GdoOverlayValue.Struct(members); break;
                    case GdoOverlayValueKind.List:
                    case GdoOverlayValueKind.Set:
                        var values = new List<GdoOverlayValue>();
                        int index = 0;
                        foreach (int childId in node.ValueNodeIds ?? Array.Empty<int>())
                        {
                            if (!TryDecodeNode(childId, path + "[" + index++ + "]", depth + 1, nodes, states, cache, issues, out GdoOverlayValue child)) return false;
                            values.Add(child);
                        }
                        value = node.Kind == GdoOverlayValueKind.List ? GdoOverlayValue.List(values) : GdoOverlayValue.Set(values);
                        break;
                    case GdoOverlayValueKind.LocalizationMap: value = GdoOverlayValue.LocalizationMap(node.Locales ?? Array.Empty<GdoLocaleEntry>()); break;
                    default: throw new InvalidOperationException("Unknown overlay value kind " + node.Kind + ".");
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                issues.Add(new GdoOverlayPersistenceDiagnostic("InvalidNode", path, "Node ID " + id + " is invalid: " + exception.Message));
                return false;
            }
            finally
            {
                if (value == null) states.Remove(id);
            }

            states[id] = 2;
            cache[id] = value;
            return true;
        }

        private static bool HasValidPayload(GdoOverlaySerializedNode node)
        {
            int members = node.Members?.Count ?? 0;
            int values = node.ValueNodeIds?.Count ?? 0;
            int locales = node.Locales?.Count ?? 0;
            bool scalar = node.HasScalar;
            bool reference = node.HasReference;
            switch (node.Kind)
            {
                case GdoOverlayValueKind.Null: return !scalar && !reference && members == 0 && values == 0 && locales == 0;
                case GdoOverlayValueKind.Scalar:
                case GdoOverlayValueKind.Enum:
                case GdoOverlayValueKind.String: return scalar && !reference && members == 0 && values == 0 && locales == 0;
                case GdoOverlayValueKind.GdoReference: return !scalar && reference && members == 0 && values == 0 && locales == 0;
                case GdoOverlayValueKind.Struct: return !scalar && !reference && values == 0 && locales == 0;
                case GdoOverlayValueKind.List:
                case GdoOverlayValueKind.Set: return !scalar && !reference && members == 0 && locales == 0;
                case GdoOverlayValueKind.LocalizationMap: return !scalar && !reference && members == 0 && values == 0;
                default: return false;
            }
        }
    }
}
