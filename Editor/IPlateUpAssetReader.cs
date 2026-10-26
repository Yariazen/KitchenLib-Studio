using System;
using System.Collections.Generic;
using System.Threading;

namespace KitchenLib.Studio.Editor
{
    public interface IPlateUpAssetReader
    {
        VanillaGdoScanResult Scan(string gameRoot, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken);
    }

    public interface IPlateUpAssetReaderBackend
    {
        IReadOnlyList<PlateUpAssetObject> Read(string gameRoot, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken);
    }
}
