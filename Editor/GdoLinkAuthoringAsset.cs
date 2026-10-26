using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoLinkAuthoringAsset : ScriptableObject
    {
        [SerializeField]
        private GameDataObject _target;

        [SerializeField]
        private List<int> _minimumIngredientIds = new List<int>();

        public GameDataObject Target => _target;
        public IReadOnlyList<int> MinimumIngredientIds => _minimumIngredientIds;

        public void Initialise(GameDataObject target)
        {
            _target = target ? target : throw new ArgumentNullException(nameof(target));
        }

        public void SetMinimumIngredientIds(IEnumerable<int> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            int[] materialized = ids.ToArray();
            if (materialized.Any(id => id == 0))
            {
                throw new ArgumentException("GDO link IDs must be non-zero.", nameof(ids));
            }

            _minimumIngredientIds = materialized.ToList();
        }
    }
}
