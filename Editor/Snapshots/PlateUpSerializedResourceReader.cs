using System;
using System.IO;
using System.Threading;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal static class PlateUpSerializedResourceReader
    {
        public const int MaximumReadBytes = 256 * 1024 * 1024;

        public static byte[] Read(string dataRoot, string relativePath, long offset, int size,
            CancellationToken cancellationToken)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (size < 0 || size > MaximumReadBytes) throw new ArgumentOutOfRangeException(nameof(size));
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Resource path is required.", nameof(relativePath));
            string root = Path.GetFullPath(dataRoot ?? string.Empty);
            string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Resource path must remain beneath the PlateUp data directory.", nameof(relativePath));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (offset > stream.Length || size > stream.Length - offset) throw new EndOfStreamException();
                stream.Position = offset;
                var result = new byte[size];
                int read = 0;
                while (read < size)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = stream.Read(result, read, size - read);
                    if (count == 0) throw new EndOfStreamException();
                    read += count;
                }
                return result;
            }
        }
    }
}
