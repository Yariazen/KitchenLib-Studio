using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sirenix.Serialization
{
    // Metadata marker only. Runtime Odin serialization is supplied by the game, not this authoring mirror.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class OdinSerializeAttribute : Attribute { }

    public enum DataFormat { Binary, Nodes, JSON }
    public enum EntryType { Invalid }

    [Serializable]
    public struct SerializationNode
    {
        public string Name;
        public EntryType Entry;
        public string Data;
    }

    [Serializable]
    public struct SerializationData
    {
        public DataFormat SerializedFormat;
        public byte[] SerializedBytes;
        public List<UnityEngine.Object> ReferencedUnityObjects;
        public string SerializedBytesString;
        public UnityEngine.Object Prefab;
        public List<UnityEngine.Object> PrefabModificationsReferencedUnityObjects;
        public List<string> PrefabModifications;
        public List<SerializationNode> SerializationNodes;
    }
}

namespace Sirenix.OdinInspector
{
    using Sirenix.Serialization;

    public abstract class SerializedScriptableObject : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private SerializationData serializationData;
    }
}
