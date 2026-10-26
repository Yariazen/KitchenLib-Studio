using System.Collections.Generic;
using System.Linq;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal sealed class VanillaGdoCatalogEntry
    {
        internal VanillaGdoCatalogEntry(VanillaGdoCatalogEntryData data) { Id = data.id; Name = data.name; RuntimeType = data.runtimeType; AssemblyName = data.assemblyName; Locations = data.locations ?? new VanillaGdoCatalogLocationData[0]; }
        public int Id { get; }
        public string Name { get; }
        public string RuntimeType { get; }
        public string AssemblyName { get; }
        public IReadOnlyList<VanillaGdoCatalogLocationData> Locations { get; }
    }

    internal sealed class VanillaGdoCatalog
    {
        internal VanillaGdoCatalog(VanillaGdoCatalogData data)
        {
            Data = data;
            Entries = data.entries.Select(item => new VanillaGdoCatalogEntry(item)).ToArray();
        }
        internal VanillaGdoCatalogData Data { get; }
        public IReadOnlyList<VanillaGdoCatalogEntry> Entries { get; }
    }
}
