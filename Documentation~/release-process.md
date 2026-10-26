# Release process

KitchenLib Studio is a UPM package at the repository root. Release tags use `v<package.json version>`; the current candidate is `v0.1.0-alpha.1`. Publishing a tag or GitHub release requires a separate human release decision.

## Manual `0.x` releases

1. Update `package.json` and `CHANGELOG.md` together. Record incompatible supported C# API changes during `0.x`; retain stable serialized identities and field/port keys. Check that the recommended Git installation URL in `README.md` matches the repository URL and the proposed tag.
2. Review the package boundary and third-party inventory in [Third Party Notices](../Third%20Party%20Notices.md). A changed bundled binary, type-tree payload, upstream license, or provenance requires a renewed audit. The first-party [MIT license](../LICENSE.md) does not relicense third-party material.
3. Run `pwsh -File Tools~/Verify-Package.ps1`, then `pwsh -File Tools~/Verify-CleanUnityImport.ps1 -PackageRoot . -UnityPath <Unity-2020.3.48f1-executable>` from the repository root. The clean import uses a disposable Unity project outside the checkout.
4. Run `pwsh -File Tools~/New-ReleaseArchive.ps1 -PackageRoot . -DestinationPath <external-path>.zip`. This requires the package gate and includes only root package metadata, README, changelog, licenses/notices, `Documentation~/`, and `Editor/`. Verify the archive entries, then compute SHA-256 and publish a matching `.sha256` file alongside the zip.
5. Check Markdown links, `git diff --check`, `git fsck --full`, and a clean working tree. Review the changelog, archive checksum, import log, and license inventory before approving the versioned tag and release. Keep candidate archives and logs outside the checkout.

Do not include a Unity project, examples, internal tests, generated game data, or agent files in the package or archive. Do not publish the candidate zip merely because local verification passed.

## Planned v1 release automation

At v1, tag-driven automation should validate that the tag matches `package.json` and the repository installation URL; run the package and license/provenance gate; perform a clean Unity import; verify changelog coverage; build the allowlisted archive and checksum; and create a **draft** GitHub release. A human reviews and approves the draft before publication. This workflow is a plan, not an implemented release trigger.
