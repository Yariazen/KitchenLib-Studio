# Vanilla Snapshot Batch Performance Design

## Intent

Reduce alpha.7 vanilla catalogue generation from an hours-long snapshot phase to a bounded, observable batch operation without changing catalogue contents, snapshot selection, visual output, cancellation semantics, or atomic publication.

## Goals

- Validate installed PlateUp source fingerprints once before snapshot rendering.
- Reuse one disposable AssetsTools decoding session for the complete snapshot batch.
- Open source containers lazily and retain them until batch disposal.
- Reuse decoded mesh, material, texture, and sprite data across snapshots by source identity.
- Keep Unity reconstruction and 128x128 PNG rendering sequential on the editor thread.
- Report `Rendering snapshot X of Y` progress and preserve cancellation between snapshots.
- Record elapsed time separately for validation, decoding, Unity reconstruction/rendering, and publication.
- Dispose decoding resources on success, cancellation, or failure.

## Non-goals

- No persistent decoded visual cache.
- No parallel Unity rendering.
- No stale PlateUp source compatibility.
- No changes to source resolution, eligibility, PNG dimensions, or atomic publication.

## Approaches

1. Validate only once: smallest change, but retains repeated AssetsTools/class database/container setup.
2. Reusable on-demand batch session: validate once, retain readers and lazy shared decoded resources, then dispose at the batch boundary. Selected because it removes both established multipliers without a new persistent format.
3. Predecode all visual assets: potentially fastest later reads, but wastes memory and work on unused assets and requires a versioned persistent cache.

## Selected architecture

`AssetsToolsPlateUpPreviewDecoder` will expose a disposable batch session. Session creation validates the catalogue once and initializes one `AssetsManager`; each decode reuses loaded containers and source-identity resource caches. `GameObjectSnapshotService` will accept the session for batch rendering and measure decode separately from Unity reconstruction/rendering. `VanillaSnapshotBatchRenderer` will report item-level progress and retain sequential execution. `PlateUpCatalogImportService` owns the session lifetime, aggregates phase timings, and leaves `VanillaCatalogPublication.Publish` unchanged.

The existing one-shot decoder API remains for interactive snapshot requests and implements its operation through a short-lived session. Alpha.7 does not support older generated mirrors.

## Verification

External Unity editor tests will prove one validation per batch, one session shared across snapshots, cross-snapshot resource reuse, ordered progress, cancellation, disposal on every exit, and unchanged publication behavior. Repository package verification will continue to run without local Unity execution.
