using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KitchenLib.Studio.Editor.Api.Graph;
using UnityEditor;

namespace KitchenLib.Studio.Editor.Graph
{
    [InitializeOnLoad]
    internal static class StudioGraphNodeRegistry
    {
        private static readonly SortedDictionary<string, StudioGraphNodeDefinition> Definitions =
            new SortedDictionary<string, StudioGraphNodeDefinition>(StringComparer.Ordinal);
        private static bool refreshPending;
        internal static event Action Changed;

        static StudioGraphNodeRegistry() => AssemblyReloadEvents.beforeAssemblyReload += Reset;

        internal static IReadOnlyList<StudioGraphNodeDefinition> All => Definitions.Values.ToArray();
        internal static StudioGraphNodeDefinition Find(string typeId) => typeId != null &&
            Definitions.TryGetValue(typeId, out StudioGraphNodeDefinition definition) ? definition : null;

        internal static void Register(IStudioGraphNodeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var supplied = provider.GetDefinitions() ?? throw new ArgumentException("The provider must return definitions.", nameof(provider));
            var pending = new SortedDictionary<string, StudioGraphNodeDefinition>(StringComparer.Ordinal);
            foreach (StudioGraphNodeDefinition definition in supplied)
            {
                if (definition == null) throw new ArgumentException("Definitions must not contain null entries.", nameof(provider));
                if (!Regex.IsMatch(definition.TypeId, @"\A[A-Za-z0-9][A-Za-z0-9._-]*:[A-Za-z0-9][A-Za-z0-9._/-]*\z"))
                    throw new ArgumentException("Node type IDs must be namespaced identifiers: namespace:name.", nameof(provider));
                if (pending.ContainsKey(definition.TypeId) || Definitions.ContainsKey(definition.TypeId))
                    throw new ArgumentException("Duplicate graph node type ID: " + definition.TypeId, nameof(provider));
                ValidateKeys(definition.Fields.Select(field => field.Key));
                ValidateKeys(definition.Ports.Select(port => port.Key));
                foreach (StudioGraphPortDefinition port in definition.Ports) ValidateKey(port.SemanticTypeId);
                pending.Add(definition.TypeId, definition);
            }
            // Enumerating or validating a provider can fail; publish only the complete snapshot.
            foreach (string id in pending.Keys)
                if (Definitions.ContainsKey(id)) throw new ArgumentException("Duplicate graph node type ID: " + id, nameof(provider));
            foreach (var pair in pending) Definitions.Add(pair.Key, pair.Value);
            if (pending.Count == 0 || refreshPending) return;
            refreshPending = true;
            EditorApplication.delayCall += NotifyChanged;
        }

        private static void ValidateKeys(IEnumerable<string> keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                ValidateKey(key);
                if (!seen.Add(key)) throw new ArgumentException("Duplicate ordinal key: " + key);
            }
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsControl))
                throw new ArgumentException("Keys must be non-blank and contain no control characters.");
        }

        private static void NotifyChanged()
        {
            refreshPending = false;
            if (Changed == null) return;
            foreach (Action handler in Changed.GetInvocationList())
                try { handler(); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }

        private static void Reset()
        {
            EditorApplication.delayCall -= NotifyChanged;
            refreshPending = false;
            Definitions.Clear();
            Changed = null;
        }
    }
}
