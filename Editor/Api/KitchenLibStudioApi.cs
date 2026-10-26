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
        /// <summary>Ordinal semantic port token for a reference to an existing authored GDO node.</summary>
        public const string GdoReferenceSemanticTypeId = "studio:gdo-reference";

        /// <summary>
        /// Registers a complete provider atomically on the Unity editor thread. Type IDs use
        /// namespace:name syntax and ordinal comparison. Register again after each domain reload.
        /// Successful registrations coalesce into one deferred refresh of open graph windows.
        /// </summary>
        /// <exception cref="ArgumentNullException">The provider is null.</exception>
        /// <exception cref="ArgumentException">Definitions or identifiers are invalid or duplicated.</exception>
        public static void RegisterGraphNodes(IStudioGraphNodeProvider provider) =>
            KitchenLib.Studio.Editor.Graph.StudioGraphNodeRegistry.Register(provider);

        internal static IReadOnlyList<StudioGraphNodeDefinition> RegisteredGraphNodes =>
            KitchenLib.Studio.Editor.Graph.StudioGraphNodeRegistry.All;
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
