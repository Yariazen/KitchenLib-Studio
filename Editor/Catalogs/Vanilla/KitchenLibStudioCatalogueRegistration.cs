using System.Threading;
using UnityEditor;

using KitchenLib.Studio.Editor.Bootstrap;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    [InitializeOnLoad]
    internal static class KitchenLibStudioCatalogueRegistration
    {
        static KitchenLibStudioCatalogueRegistration()
        {
            KitchenLibStudioCatalogueBridge.Register(installation =>
            {
                PlateUpCatalogImportController.ImportCombinedBatch(installation, CancellationToken.None);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            });
        }
    }
}
