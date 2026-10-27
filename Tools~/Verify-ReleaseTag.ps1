[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [Parameter(Mandatory = $true)][string]$Tag
)

$ErrorActionPreference = 'Stop'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
if (-not $Tag.StartsWith('v', [StringComparison]::Ordinal)) {
    throw "Release tag must start with v: $Tag"
}
$version = $Tag.Substring(1)
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Release tag version is empty.' }
$package = Get-Content -Raw -LiteralPath (Join-Path $PackageRoot 'package.json') | ConvertFrom-Json
if ($Tag -cne "v$($package.version)") {
    throw "Release tag $Tag does not match package version $($package.version)."
}
$readme = Get-Content -Raw -LiteralPath (Join-Path $PackageRoot 'README.md')
$installUrl = "$($package.repository.url)#$Tag"
if (-not $readme.Contains($installUrl)) {
    throw "README.md does not contain the release installation URL $installUrl."
}
$changelog = Get-Content -Raw -LiteralPath (Join-Path $PackageRoot 'CHANGELOG.md')
if (-not $changelog.Contains("## [$version]")) {
    throw "CHANGELOG.md does not contain a section for release tag $Tag."
}
Write-Output "PASS: release tag $Tag matches package metadata, README, and changelog."
