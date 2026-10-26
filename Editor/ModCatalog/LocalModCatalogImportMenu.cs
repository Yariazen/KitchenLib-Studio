using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class LocalModCatalogImportMenu
    {
        [MenuItem("KitchenLib Studio/Catalogues/Import Local Mod Catalogue…")]
        public static void Import()
        {
            string packagePath = EditorUtility.OpenFilePanel("Import Local Mod Catalogue", string.Empty, "klscatalog");
            if (string.IsNullOrEmpty(packagePath)) return;
            try
            {
                string projectRoot = KitchenLibStudioGeneratedPaths.ProjectRoot;
                LocalModCatalogImportResult result = LocalModCatalogPackage.Import(packagePath,
                    LocalModCatalogPackage.DefaultCacheRoot(projectRoot));
                EditorUtility.DisplayDialog("Mod Catalogue Imported",
                    "Imported " + result.ProviderKey + ".\n\nThe catalogue is stored in the project's local Library cache and will not be included in asset bundles.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Import Failed", exception.Message, "OK");
            }
        }
    }
}
