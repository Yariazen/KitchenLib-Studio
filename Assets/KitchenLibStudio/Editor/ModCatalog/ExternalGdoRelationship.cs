using System;
using System.Collections.Generic;
using System.IO;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class ExternalGdoIdentityData
    {
        public string providerKey;
        public int runtimeId;
        public string runtimeType;
    }

    [Serializable]
    public sealed class ExternalGdoRelationshipData
    {
        public ExternalGdoIdentityData source;
        public ExternalGdoIdentityData target;
        public string fieldPath;
        public string label;
    }

    public readonly struct ExternalGdoIdentity : IEquatable<ExternalGdoIdentity>
    {
        public ExternalGdoIdentity(string providerKey, int runtimeId, string runtimeType)
        {
            ProviderKey = providerKey;
            RuntimeId = runtimeId;
            RuntimeType = runtimeType;
        }

        public string ProviderKey { get; }
        public int RuntimeId { get; }
        public string RuntimeType { get; }
        public bool Equals(ExternalGdoIdentity other) => RuntimeId == other.RuntimeId &&
            string.Equals(ProviderKey, other.ProviderKey, StringComparison.Ordinal) &&
            string.Equals(RuntimeType, other.RuntimeType, StringComparison.Ordinal);
        public override bool Equals(object value) => value is ExternalGdoIdentity other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ProviderKey == null ? 0 : ProviderKey.GetHashCode();
                hash = (hash * 397) ^ RuntimeId;
                return (hash * 397) ^ (RuntimeType == null ? 0 : RuntimeType.GetHashCode());
            }
        }
    }

    public sealed class ExternalGdoRelationship
    {
        public ExternalGdoRelationship(ExternalGdoRelationshipData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Source = new ExternalGdoIdentity(data.source.providerKey, data.source.runtimeId, data.source.runtimeType);
            Target = new ExternalGdoIdentity(data.target.providerKey, data.target.runtimeId, data.target.runtimeType);
            FieldPath = data.fieldPath;
            Label = data.label;
        }

        public ExternalGdoIdentity Source { get; }
        public ExternalGdoIdentity Target { get; }
        public string FieldPath { get; }
        public string Label { get; }
    }

    internal static class ExternalGdoRelationshipSchema
    {
        internal static void Validate(IEnumerable<ExternalGdoRelationshipData> relationships)
        {
            if (relationships == null) throw new InvalidDataException("The GDO catalogue has no relationship collection.");
            var rows = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExternalGdoRelationshipData relationship in relationships)
            {
                if (relationship == null || !Valid(relationship.source) || !Valid(relationship.target) ||
                    string.IsNullOrWhiteSpace(relationship.fieldPath) || string.IsNullOrWhiteSpace(relationship.label))
                    throw new InvalidDataException("The GDO catalogue contains an invalid relationship.");
                string key = Identity(relationship.source) + "\n" + relationship.fieldPath + "\n" +
                    Identity(relationship.target);
                if (!rows.Add(key)) throw new InvalidDataException("The GDO catalogue contains a duplicate relationship.");
            }
        }

        private static bool Valid(ExternalGdoIdentityData identity) => identity != null && identity.runtimeId != 0 &&
            !string.IsNullOrWhiteSpace(identity.providerKey) && !string.IsNullOrWhiteSpace(identity.runtimeType);
        private static string Identity(ExternalGdoIdentityData identity) =>
            identity.providerKey + "\n" + identity.runtimeId + "\n" + identity.runtimeType;
    }
}
