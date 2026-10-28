using System;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal sealed class VanillaSnapshotBatchSummary
    {
        public VanillaSnapshotBatchSummary(int total, int eligible, int resolved, int rendered,
            int sourceCandidates)
        {
            if (total < 0 || eligible < 0 || resolved < 0 || rendered < 0 || sourceCandidates < 0 ||
                eligible > total || resolved > eligible || rendered > resolved || sourceCandidates > total)
                throw new ArgumentOutOfRangeException(nameof(total), "Snapshot batch counts are inconsistent.");
            Total = total;
            Eligible = eligible;
            Resolved = resolved;
            Rendered = rendered;
            SourceCandidates = sourceCandidates;
        }

        public int Total { get; }
        public int Eligible { get; }
        public int Resolved { get; }
        public int Rendered { get; }
        public int SourceCandidates { get; }
        public int Unsupported => Total - Eligible;
        public int Failed => Eligible - Rendered;

        public override string ToString() => "total=" + Total + ", eligible=" + Eligible +
            ", resolved=" + Resolved + ", rendered=" + Rendered + ", sourceCandidates=" +
            SourceCandidates + ", unsupported=" + Unsupported +
            ", failed=" + Failed;
    }

    internal static class VanillaSnapshotPublicationGuard
    {
        public static void RequireSafeToPublish(VanillaSnapshotBatchSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (summary.SourceCandidates > 0 && summary.Rendered == 0)
                throw new InvalidOperationException("Vanilla snapshot generation produced no PNGs despite " +
                    "resolvable candidates; the previous catalogue was preserved. " + summary + ".");
        }
    }
}
