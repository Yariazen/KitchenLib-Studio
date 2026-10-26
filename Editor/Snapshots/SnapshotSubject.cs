using System;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public enum SnapshotSubjectKind { VanillaGameObjectKey, LocalGameObject, CustomGdo }

    public sealed class SnapshotSubject
    {
        private SnapshotSubject(SnapshotSubjectKind kind, string key, GameObject gameObject, GameDataObject gdo, string fieldPath)
        { Kind = kind; VanillaKey = key; GameObject = gameObject; Gdo = gdo; PrefabFieldPath = fieldPath; }

        public SnapshotSubjectKind Kind { get; }
        public string VanillaKey { get; }
        public GameObject GameObject { get; }
        public GameDataObject Gdo { get; }
        public string PrefabFieldPath { get; }
        public bool CanUsePersistentCache
        {
            get
            {
                if (Kind == SnapshotSubjectKind.VanillaGameObjectKey) return !string.IsNullOrEmpty(VanillaKey);
                UnityEngine.Object value = Kind == SnapshotSubjectKind.LocalGameObject ? (UnityEngine.Object)GameObject : Gdo;
                return value != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value));
            }
        }

        public static SnapshotSubject VanillaGameObjectKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Vanilla snapshot key is required.", nameof(key));
            return new SnapshotSubject(SnapshotSubjectKind.VanillaGameObjectKey, key, null, null, null);
        }

        public static SnapshotSubject LocalGameObject(GameObject gameObject)
        {
            if (gameObject == null) throw new ArgumentNullException(nameof(gameObject));
            return new SnapshotSubject(SnapshotSubjectKind.LocalGameObject, null, gameObject, null, null);
        }

        public static SnapshotSubject CustomGdo(GameDataObject gdo, string optionalPrefabField = null)
        {
            if (gdo == null) throw new ArgumentNullException(nameof(gdo));
            return new SnapshotSubject(SnapshotSubjectKind.CustomGdo, null, null, gdo, optionalPrefabField);
        }
    }
}
