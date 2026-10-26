using System;
using System.Collections.Generic;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class GdoLinkManifest
    {
        public int schemaVersion;
        public string mirrorFingerprint;
        public List<GdoLinkTarget> targets = new List<GdoLinkTarget>();
    }

    [Serializable]
    public sealed class GdoLinkTarget
    {
        public int gdoId;
        public string gdoType;
        public List<GdoLinkField> links = new List<GdoLinkField>();
    }

    [Serializable]
    public sealed class GdoLinkField
    {
        public string field;
        public string kind;
        public string elementType;
        public List<int> ids = new List<int>();
    }
}
