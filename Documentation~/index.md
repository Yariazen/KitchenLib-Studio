# KitchenLib Studio

Start with the [root README](../README.md) for installation and first-run setup. Maintainers can follow the [release process](release-process.md) for the manual alpha release checks and planned v1 workflow.

KitchenLib Studio is a Unity Editor tool for authoring PlateUp game data and building mod AssetBundles. Package identity: `com.kitchenlib.studio`, version `0.1.0-alpha.2`; use Unity 2020.3.

## Installation

Open **Window → Package Manager**, choose **+ → Add package from git URL**, and enter the repository URL pinned to this release tag:

```text
https://github.com/Yariazen/KitchenLib-Studio.git#v0.1.0-alpha.2
```

For an internal local checkout, choose **+ → Add package from disk** and select its root `package.json`. The MIT grant in `LICENSE.md` covers original KitchenLib Studio code and documentation only; exact provenance and acknowledgments for bundled third-party material are recorded in `Third Party Notices.md`.

The repository root is the UPM package. It intentionally contains no Unity project, examples, samples, or internal test suite. Maintainers can audit that boundary with `pwsh -File Tools~/Verify-Package.ps1` using PowerShell 7 and its bundled C# parser. The gate inspects every non-template C# source, rejects placement outside `Editor`, and compares public/protected signature fingerprints against the explicit ordinal public-type allowlist, including imports, parameters, accessors, enums and constants. Each conditional symbol configuration is parsed independently, with at most eight symbols per file; exceeding this bound or encountering invalid syntax fails the audit. It rejects implementation leaks and unreviewed contract changes.

Repository verification creates its Unity project outside the checkout and discards it after import. Run a local clean import with:

```powershell
pwsh -File Tools~/Verify-CleanUnityImport.ps1 -PackageRoot . -UnityPath <path-to-Unity-2020.3.48f1>
```

`Tools~/New-ReleaseArchive.ps1` first requires the production-only package/license gate to pass, then builds the distributable zip from an explicit allowlist containing package metadata, documentation, licenses, and `Editor/**`. A red gate produces no archive. The archive excludes `.github`, `Tools~`, Git metadata, and every non-package path. The GitHub Unity job is enabled only on a licensed self-hosted runner labeled `unity-2020.3`, with repository variable `KITCHENLIB_STUDIO_UNITY_IMPORT=enabled` and `UNITY_2020_3_PATH` pointing to Unity. It imports the package and never runs an internal test suite.

## Generated data and source control

The consuming project's `Assets/KitchenLibStudio.Generated/` directory is owned by the generator and may be replaced completely. Add these rules to the consuming project's `.gitignore` (the package's ignore file does not configure your project):

```gitignore
/Assets/KitchenLibStudio.Generated/
/Assets/KitchenLibStudio.Generated.meta
```

Disposable fingerprints and cache state belong under `Library/KitchenLibStudio/`; ignore `Library/` as normal for Unity projects. Generated output and caches can be deleted and regenerated from the user's PlateUp installation. Keep authored bundle projects, content, and their `.meta` files in source control.

KitchenLib Studio targets the latest PlateUp Steam build tested at each package release. Generated mirrors and imported catalogs come from the user's local installation; do not commit or redistribute them with the package.

## Local mirror generation

Choose **KitchenLib Studio → Setup…** and generate mirrors. Studio automatically selects and remembers a valid standard installation or a single installation found through Steam discovery. Manual folder selection is used when neither route produces one automatic choice, including when discovery finds multiple installations. The generator reads managed assembly metadata with the bundled Mono.Cecil; it never loads or executes game assemblies. It accepts the selected installation without comparing its build or version to a shipped schema. Each generation derives the required game-data types, fields, properties, enums, generic arguments and serialization attributes from that installation.

Mirrors are data-only authoring types. They preserve serialized field names/order and the base-class hierarchy, while computed properties have inert accessors and game methods are not copied. Behavioral interfaces and generic constraints remain recorded in the schema; interface method contracts are not implemented. Unsupported overlay value shapes receive field diagnostics, and their fields remain in the generated mirrors. Runtime field mappings recognize direct reads, collection copies and instance selection calls from serialized fields in the installed IL; they are never guessed from field names.

The package depends on **TextMeshPro 3.0.6** because game localization data references `TMPro.TMP_FontAsset`. It references Unity's `Unity.TextMeshPro` assembly instead of generating conflicting TMPro types. Other required game-owned support types are generated in assemblies retaining their original names.

Generation stages deterministic sources, assembly definitions, stable metadata, schema and descriptor registries in `Library/KitchenLibStudio/Staging/<id>`. Unity compiles each staged assembly in dependency order before publication, with disposable compiler files under `Library/KitchenLibStudio/Compilation/<id>`. Publication swaps the complete directory into `Assets/KitchenLibStudio.Generated`, writes its completion state, refreshes Unity and enables authoring. A recovery record and the previous directory are retained until publication succeeds. Cancellation or failure restores the previous output, state and authoring gate; interrupted publication is recovered on the next editor startup. Package and PackageCache files are never generation destinations.

Reconciliation compares the saved generator identity (format revision and package version) and source-closure fingerprint with the current package and selected installation. Changed or temporarily unreadable sources keep the authoring gate closed and reopen setup, without a supported-build check. Complete matching output is reused when Generate Mirrors is requested; its files and completion state are not rewritten. Generation-format changes must increment the generator's format revision.

See [Authoring bundle projects](authoring.md) for the authoring workflow.

Supported editor extensions live in `KitchenLib.Studio.Editor.Api` and its `Authoring`, `Graph`, `Validation` and `Build` namespaces, within the existing editor-only authoring assembly. Register declarative graph providers and pre-build processors explicitly each domain. Studio owns rendering, persistence, validation, temporary build projection, audit and final bundle verification. [The authoring reference](authoring.md#pre-build-processors) lists every supported type and its bounded pre-build semantics; implementation windows, repositories, readers and builders are internal. Runtime mod APIs are outside this surface.

During `0.x`, incompatible C# API changes are documented in the changelog. Strict semantic-version compatibility starts at `1.0.0`; later breaking API changes require a major version. Serialized node identities, field/port keys and preservation of unknown data are protected independently from the first alpha.
