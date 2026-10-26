using System;
using System.Collections.Generic;
using System.Threading;

using KitchenLib.Studio.Editor.Catalogs.Vanilla;

namespace KitchenLib.Studio.Editor.Infrastructure
{
    internal interface IPlateUpAssetReader
    {
        VanillaGdoScanResult Scan(string gameRoot, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken);
    }

    internal interface IPlateUpAssetReaderBackend
    {
        IReadOnlyList<PlateUpAssetObject> Read(string gameRoot, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken);
    }
}
