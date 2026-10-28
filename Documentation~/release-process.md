# Release process

KitchenLib Studio is a UPM package at the repository root. Release tags use `v<package.json version>`; the current candidate is `v0.1.0-alpha.6`. Publishing a tag or GitHub release requires a separate human release decision.

## Manual `0.x` releases

1. Update `package.json` and `CHANGELOG.md` together. Record incompatible supported C# API changes during `0.x`; retain stable serialized identities and field/port keys. Check that the recommended Git installation URL in `README.md` matches the repository URL and the proposed tag.
2. Review the package boundary and third-party inventory in [Third Party Notices](../Third%20Party%20Notices.md). A changed bundled binary, type-tree payload, upstream license, or provenance requires a renewed audit. The first-party [MIT license](../LICENSE.md) does not relicense third-party material.
3. Run `pwsh -File Tools~/Verify-Package.ps1`, then `pwsh -File Tools~/Verify-CleanUnityImport.ps1 -PackageRoot . -UnityPath <Unity-2020.3.48f1-executable> -RetainedLogPath <external-import-log-path>` from the repository root. Choose a log path outside the checkout; the clean import uses a disposable Unity project outside the checkout.
4. Run `pwsh -File Tools~/New-ReleaseArchive.ps1 -PackageRoot . -DestinationPath <external-path>.zip`. This requires the package gate and includes only root package metadata, README, changelog, licenses/notices, `Documentation~/`, and `Editor/`. Verify the archive entries, then compute SHA-256 and publish a matching `.sha256` file alongside the zip.
5. Check Markdown links, `git diff --check`, `git fsck --full`, and a clean working tree. Review the changelog, archive checksum, retained import log at `<external-import-log-path>`, and license inventory before approving the versioned tag and release. Keep candidate archives and logs outside the checkout.

Do not include a Unity project, examples, internal tests, generated game data, or agent files in the package or archive. Do not publish the candidate zip merely because local verification passed.

## Tag-driven draft releases

The native Unity 2020.3 clean import in step 3 is a required maintainer pre-tag check until hosted Unity licensing is configured. Preserve its external log with the release evidence; GitHub does not repeat this licensed-editor check.

After the reviewed release-preparation PR merges, create an annotated `v<package.json version>` tag at the verified `main` commit and push only that tag. `.github/workflows/release.yml` runs entirely on GitHub-hosted infrastructure: it validates the tag against package metadata, the README installation URL, and the matching changelog section; runs the package/license gate; and builds and inspects the allowlisted archive and checksum.

When every gate succeeds, the workflow uses GitHub's scoped token to create a **draft prerelease** targeting the tagged commit and attaches the ZIP plus its `.sha256` file. A human must inspect the target commit, notes, artifacts, and checksum before choosing **Publish release** in GitHub. A failed tag is immutable: correct the problem under a new version and tag instead of moving or replacing the failed tag.
