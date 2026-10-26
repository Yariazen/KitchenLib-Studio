# KitchenLib Studio

KitchenLib Studio is a Unity Editor tool for authoring PlateUp game data and building mod AssetBundles. Package identity: `com.kitchenlib.studio`, version `0.1.0`; use Unity 2020.3.

## Installation

After the release is published, open **Window → Package Manager**, choose **+ → Add package from git URL**, and enter the final repository URL pinned to `v0.1.0`:

```text
https://example.invalid/REPLACE-WITH-KITCHENLIB-STUDIO-REPOSITORY.git#v0.1.0
```

This is an intentional placeholder, not an installable repository. Replace it with the published repository URL. During migration, the original-code license is also a release-blocking placeholder; see `LICENSE.md`.

The repository root is the UPM package. It intentionally contains no Unity project, examples, samples, or internal test suite. Maintainers can audit that boundary with `pwsh -File Tools~/Verify-Package.ps1`; the gate remains red until the original-code license and `classdata.tpk` redistribution provenance are resolved.

## Generated data and source control

The consuming project's `Assets/KitchenLibStudio.Generated/` directory is owned by the generator and may be replaced completely. Add these rules to the consuming project's `.gitignore` (the package's ignore file does not configure your project):

```gitignore
/Assets/KitchenLibStudio.Generated/
/Assets/KitchenLibStudio.Generated.meta
```

Disposable fingerprints and cache state belong under `Library/KitchenLibStudio/`; ignore `Library/` as normal for Unity projects. Generated output and caches can be deleted and regenerated from the user's PlateUp installation. Keep authored bundle projects, content, and their `.meta` files in source control.

KitchenLib Studio targets the latest PlateUp Steam build tested at each package release. Generated mirrors and imported catalogs come from the user's local installation; do not commit or redistribute them with the package.

## Local mirror generation

Choose **KitchenLib Studio → Setup…**, select a PlateUp installation, and generate mirrors. The generator reads managed assembly metadata with the bundled Mono.Cecil; it never loads or executes game assemblies. It accepts the selected installation without comparing its build or version to a shipped schema. Each generation derives the required game-data types, fields, properties, enums, generic arguments and serialization attributes from that installation.

Mirrors are data-only authoring types. They preserve serialized field names/order and the base-class hierarchy, while computed properties have inert accessors and game methods are not copied. Behavioral interfaces and generic constraints remain recorded in the schema; interface method contracts are not implemented. Unsupported overlay value shapes receive field diagnostics, and their fields remain in the generated mirrors. Runtime field mappings recognize direct reads, collection copies and instance selection calls from serialized fields in the installed IL; they are never guessed from field names.

The package depends on **TextMeshPro 3.0.6** because game localization data references `TMPro.TMP_FontAsset`. It references Unity's `Unity.TextMeshPro` assembly instead of generating conflicting TMPro types. Other required game-owned support types are generated in assemblies retaining their original names.

Generation stages deterministic sources, assembly definitions, stable metadata, schema and descriptor registries in `Library/KitchenLibStudio/Staging/<id>`. Unity compiles each staged assembly in dependency order before publication, with disposable compiler files under `Library/KitchenLibStudio/Compilation/<id>`. Publication swaps the complete directory into `Assets/KitchenLibStudio.Generated`, writes its completion state, refreshes Unity and enables authoring. A recovery record and the previous directory are retained until publication succeeds. Cancellation or failure restores the previous output, state and authoring gate; interrupted publication is recovered on the next editor startup. Package and PackageCache files are never generation destinations.

Reconciliation compares the saved generator identity (format revision and package version) and source-closure fingerprint with the current package and selected installation. Changed or temporarily unreadable sources keep the authoring gate closed and reopen setup, without a supported-build check. Complete matching output is reused when Generate Mirrors is requested; its files and completion state are not rewritten. Generation-format changes must increment the generator's format revision.

See [Authoring bundle projects](authoring.md) for the authoring workflow.
