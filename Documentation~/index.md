# KitchenLib Studio

KitchenLib Studio is a Unity Editor tool for authoring PlateUp game data and building mod AssetBundles. Package identity: `com.kitchenlib.studio`, version `0.1.0`; use Unity 2020.3.

## Installation

After the release is published, open **Window → Package Manager**, choose **+ → Add package from git URL**, and enter the final repository URL pinned to `v0.1.0`:

```text
https://example.invalid/REPLACE-WITH-KITCHENLIB-STUDIO-REPOSITORY.git#v0.1.0
```

This is an intentional placeholder, not an installable repository. Replace it with the published repository URL. During migration, the original-code license is also a release-blocking placeholder; see `LICENSE.md`.

## Generated data and source control

The consuming project's `Assets/KitchenLibStudio.Generated/` directory is owned by the generator and may be replaced completely. Add these rules to the consuming project's `.gitignore` (the package's ignore file does not configure your project):

```gitignore
/Assets/KitchenLibStudio.Generated/
/Assets/KitchenLibStudio.Generated.meta
```

Disposable fingerprints and cache state belong under `Library/KitchenLibStudio/`; ignore `Library/` as normal for Unity projects. Generated output and caches can be deleted and regenerated from the user's PlateUp installation. Keep authored bundle projects, content, and their `.meta` files in source control.

KitchenLib Studio targets the latest PlateUp Steam build tested at each package release. Generated mirrors and imported catalogs come from the user's local installation; do not commit or redistribute them with the package.

See [Authoring bundle projects](authoring.md) for the authoring workflow.
