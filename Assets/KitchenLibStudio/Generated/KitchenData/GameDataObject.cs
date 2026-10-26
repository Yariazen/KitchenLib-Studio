using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenData
{
    public abstract class GameDataObject : KitchenObject
    {
        [HideInInspector]
        public int ID;

        protected abstract void InitialiseDefaults();
    }
}