using System;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class GdoGraphPrefabProjectionState
    {
        [SerializeField] private string _ownerNodeId;
        [SerializeField] private string _fieldPath;
        [SerializeField] private GameObject _previousValue;

        private GdoGraphPrefabProjectionState() { }
        public string OwnerNodeId => _ownerNodeId;
        public string FieldPath => _fieldPath;
        public GameObject PreviousValue => _previousValue;

        internal static GdoGraphPrefabProjectionState Create(string ownerNodeId, string fieldPath,
            GameObject previousValue) => new GdoGraphPrefabProjectionState
            { _ownerNodeId = ownerNodeId, _fieldPath = fieldPath, _previousValue = previousValue };
    }
}
