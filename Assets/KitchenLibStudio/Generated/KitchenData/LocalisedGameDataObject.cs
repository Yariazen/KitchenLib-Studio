namespace KitchenData
{
    [System.Serializable]
    public abstract class Localisation { public Locale Locale = Locale.English; }
    public enum Locale { Default = 0, English = 1 }

    [System.Serializable]
    public sealed class UnlockInfo : Localisation
    {
        public string Name;
        [UnityEngine.TextArea(7, 10)] public string FlavourText;
        [UnityEngine.TextArea(7, 10)] public string Description;
    }

    [System.Serializable]
    public sealed class LocalisationObject<T> where T : Localisation { }

    public abstract class LocalisedGameDataObject<T> : GameDataObject where T : Localisation
    {
        #region Serialized Authoring State
        public LocalisationObject<T> Info;
        #endregion

        #region Runtime-Derived State
        // PlateUp selects Info into Localisation during Localise().
        [System.NonSerialized, UnityEngine.HideInInspector] public T Localisation;
        #endregion
    }
}