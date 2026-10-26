# KitchenLib Studio

KitchenLib Studio is a Unity Editor package for authoring PlateUp game data and building mod AssetBundles. This `0.1.0-alpha.1` package targets Unity 2020.3 and the latest PlateUp Steam build tested for this alpha. PlateUp is installed separately; the package does not contain game assemblies or game data.

## Install

Use Unity 2020.3.48f1 for this alpha. Once the `v0.1.0-alpha.1` tag is published, open **Window → Package Manager**, choose **+ → Add package from git URL**, and enter:

```text
https://github.com/Yariazen/KitchenLib-Studio.git#v0.1.0-alpha.1
```

The tag is not published yet. For an authorized local checkout, choose **+ → Add package from disk** and select the checkout's root `package.json`.

For a release zip, extract it outside the Unity project's `Assets` directory. The extracted package root is the folder containing `package.json`, `Editor/`, and `Documentation~/`; choose **+ → Add package from disk** and select that `package.json`. Do not copy the package into `Assets`.

## First run

In Unity, choose **KitchenLib Studio → Setup…**, select your local PlateUp Steam installation, and generate mirrors. The generated mirrors and catalogs are disposable output derived from your installation. Regenerate them after changing the PlateUp installation or package version. See [setup and generated data](Documentation~/index.md#local-mirror-generation) for details.

Open **KitchenLib Studio → GDO Bundle Graph** to create a bundle project. The [authoring guide](Documentation~/authoring.md) covers graph editing, builds, and supported extension points. The Studio API is editor-only; it is not a runtime mod API. Alpha releases may change supported C# contracts, with incompatible `0.x` changes recorded in the [changelog](CHANGELOG.md).

## Package and licenses

This repository is the Unity Package Manager package root. It ships no Unity project, examples, internal tests, or generated PlateUp data. Original KitchenLib Studio code and documentation use the [MIT license](LICENSE.md). Bundled third-party binaries and type-tree data retain separate license and provenance boundaries; read [Third Party Notices](Third%20Party%20Notices.md) and the included upstream license texts before redistribution. No KitchenLib Studio MIT grant is claimed for those third-party materials.

See the [package documentation](Documentation~/index.md) for installation and maintenance details and the [release process](Documentation~/release-process.md) for maintainer release checks. No public alpha tag or release archive has been authorized yet.
