using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Graph;

namespace KitchenLib.Studio.Editor.Api
{
    /// <summary>Editor-only entry point for declarative Studio extensions.</summary>
    public static class KitchenLibStudioApi
    {
        private static readonly object RegistrationLock = new object();
        private static readonly SortedDictionary<string, StudioGraphNodeDefinition> Definitions =
            new SortedDictionary<string, StudioGraphNodeDefinition>(StringComparer.Ordinal);

        /// <summary>
        /// Registers an entire provider atomically, rejecting duplicate ordinal type IDs.
        /// This alpha facade records definitions for the Studio registry; registration alone
        /// does not implement node rendering, persistence, validation scheduling, or builds.
        /// Registrations last until the next editor domain reload.
        /// </summary>
        /// <exception cref="ArgumentNullException">The provider is null.</exception>
        /// <exception cref="ArgumentException">Definitions are null or contain null entries or duplicate IDs.</exception>
        public static void RegisterGraphNodes(IStudioGraphNodeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var supplied = provider.GetDefinitions();
            if (supplied == null) throw new ArgumentException("The provider must return definitions.", nameof(provider));
            var pending = new SortedDictionary<string, StudioGraphNodeDefinition>(StringComparer.Ordinal);
            foreach (var definition in supplied)
            {
                if (definition == null) throw new ArgumentException("Definitions must not contain null entries.", nameof(provider));
                if (pending.ContainsKey(definition.TypeId))
                    throw new ArgumentException("Duplicate graph node type ID: " + definition.TypeId, nameof(provider));
                pending.Add(definition.TypeId, definition);
            }
            lock (RegistrationLock)
            {
                foreach (var id in pending.Keys)
                    if (Definitions.ContainsKey(id))
                        throw new ArgumentException("Duplicate graph node type ID: " + id, nameof(provider));
                foreach (var entry in pending) Definitions.Add(entry.Key, entry.Value);
            }
        }

        // Minimal registry seam. Production discovery/refresh integration is deliberately separate.
        internal static IReadOnlyList<StudioGraphNodeDefinition> RegisteredGraphNodes
        {
            get { lock (RegistrationLock) return Array.AsReadOnly(Definitions.Values.ToArray()); }
        }
    }

    internal static class StudioApiGuard
    {
        internal static string Required(string value, string parameter)
        {
            if (value == null) throw new ArgumentNullException(parameter);
            if (value.Length == 0) throw new ArgumentException("A non-empty value is required.", parameter);
            return value;
        }

        internal static T EnumValue<T>(T value, string parameter) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(parameter);
            return value;
        }

        internal static IReadOnlyList<T> List<T>(IEnumerable<T> values, string parameter) where T : class
        {
            if (values == null) throw new ArgumentNullException(parameter);
            var copy = values.ToArray();
            if (copy.Any(value => value == null)) throw new ArgumentException("Null entries are not supported.", parameter);
            return Array.AsReadOnly(copy);
        }

        internal static IReadOnlyDictionary<string, StudioAuthoringValue> Values(
            IEnumerable<KeyValuePair<string, StudioAuthoringValue>> values, string parameter)
        {
            if (values == null) throw new ArgumentNullException(parameter);
            var copy = new SortedDictionary<string, StudioAuthoringValue>(StringComparer.Ordinal);
            foreach (var entry in values)
            {
                Required(entry.Key, parameter);
                if (entry.Value == null) throw new ArgumentException("Use StudioAuthoringValue.Null for authored null values.", parameter);
                if (copy.ContainsKey(entry.Key)) throw new ArgumentException("Duplicate value key: " + entry.Key, parameter);
                copy.Add(entry.Key, entry.Value);
            }
            return new ReadOnlyDictionary<string, StudioAuthoringValue>(copy);
        }

        internal static void Unique(IEnumerable<string> keys, string parameter)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
                if (!seen.Add(key)) throw new ArgumentException("Duplicate key: " + key, parameter);
        }
    }
}
