using System;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Overlays
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoReferenceToken")]
    internal sealed class GdoReferenceToken
    {
        [SerializeField] private GameDataObject _asset;
        [SerializeField] private int _id;
        [SerializeField] private string _expectedType;
        [SerializeField] private string _providerKey;

        public GameDataObject Asset => _asset;
        public int Id => _id;
        public string ExpectedType => _expectedType;
        public string ProviderKey => _asset != null ? null : _providerKey;

        private GdoReferenceToken() { }

        public static GdoReferenceToken ForAsset(GameDataObject asset, string expectedType)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            Validate(asset.ID, expectedType);
            Type type = asset.GetType();
            while (type != null && RuntimeName(type) != expectedType) type = type.BaseType;
            if (type == null) throw new ArgumentException("Asset type is incompatible with the expected GDO type.", nameof(asset));
            return new GdoReferenceToken { _asset = asset, _id = asset.ID, _expectedType = expectedType };
        }

        private static string RuntimeName(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;

        public static GdoReferenceToken Create(string providerKey, int id, string expectedType)
        {
            Validate(id, expectedType);
            if (string.IsNullOrWhiteSpace(providerKey))
                throw new ArgumentException("Provider key is required.", nameof(providerKey));
            return new GdoReferenceToken { _id = id, _expectedType = expectedType,
                _providerKey = providerKey.Trim() };
        }

        private static void Validate(int id, string expectedType)
        {
            if (id == 0) throw new ArgumentException("GDO reference IDs must be non-zero.", nameof(id));
            if (string.IsNullOrWhiteSpace(expectedType)) throw new ArgumentException("Expected type is required.", nameof(expectedType));
        }
    }
}
