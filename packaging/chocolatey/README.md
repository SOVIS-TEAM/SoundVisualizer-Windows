# Chocolatey package

This folder is the source of the [`soundvisualizer`](https://community.chocolatey.org/packages/soundvisualizer) package on the Chocolatey Community Repository. The package installs the official portable zip from GitHub Releases. It does not embed the binaries.

- `soundvisualizer.nuspec`: package metadata
- `tools/chocolateyinstall.ps1`: downloads the release zip, checks its SHA-256, unzips it into the package folder and adds a Start menu shortcut
- `tools/chocolateyuninstall.ps1`: removes the Start menu shortcut

## Updating for a new release

1. In `soundvisualizer.nuspec`, set `<version>` and the `<releaseNotes>` link to the new release.
2. In `tools/chocolateyinstall.ps1`, set `url64bit` to the new zip and `checksum64` to its SHA-256 (`Get-FileHash .\SoundVisualizer_vX.Y.Z.zip -Algorithm SHA256`).
3. From this folder: `choco pack`, then `choco push soundvisualizer.X.Y.Z.nupkg --source https://push.chocolatey.org/`.

Every new version is reviewed by Chocolatey moderators before it appears.
