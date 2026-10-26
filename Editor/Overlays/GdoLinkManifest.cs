using System;
using System.Collections.Generic;

namespace KitchenLib.Studio.Editor.Overlays
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoLinkManifest")]
    internal sealed class GdoLinkManifest
    {
        public int schemaVersion;
        public string mirrorFingerprint;
        public List<GdoLinkTarget> targets = new List<GdoLinkTarget>();
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoLinkTarget")]
    internal sealed class GdoLinkTarget
    {
        public int gdoId;
        public string gdoType;
        public List<GdoLinkField> links = new List<GdoLinkField>();
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoLinkField")]
    internal sealed class GdoLinkField
    {
        public string field;
        public string kind;
        public string elementType;
        public List<int> ids = new List<int>();
    }
}
