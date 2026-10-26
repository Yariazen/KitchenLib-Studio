using System.Threading;
using UnityEditor;

namespace KitchenLib.Studio.Editor
{
    [InitializeOnLoad]
    internal static class KitchenLibStudioCatalogueRegistration
    {
        static KitchenLibStudioCatalogueRegistration()
        {
            KitchenLibStudioCatalogueBridge.Register(installation =>
            {
                PlateUpCatalogImportController.ImportGdoBatch(installation, CancellationToken.None);
                PlateUpCatalogImportController.ImportSnapshotBatch(installation, CancellationToken.None);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            });
        }
    }
}
