[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [Parameter(Mandatory = $true)][string]$Tag,
    [switch]$RequireMainProvenance,
    [string]$MainRef = 'origin/main',
    [string]$ExpectedCommit
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
if ($RequireMainProvenance) {
    if ([string]::IsNullOrWhiteSpace($ExpectedCommit)) {
        throw 'ExpectedCommit is required when main provenance is required.'
    }
    function Invoke-Git([string[]]$Arguments) {
        $output = @(& git -C $PackageRoot @Arguments 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $($output -join ' ')" }
        return ($output -join "`n").Trim()
    }
    $tagRef = "refs/tags/$Tag"
    $peeled = Invoke-Git @('rev-parse', "$tagRef^{}")
    $head = Invoke-Git @('rev-parse', 'HEAD')
    $main = Invoke-Git @('rev-parse', $MainRef)
    & git -C $PackageRoot merge-base --is-ancestor $ExpectedCommit $MainRef
    $isOnMain = $LASTEXITCODE -eq 0
    if ($peeled -cne $ExpectedCommit -or $head -cne $ExpectedCommit -or -not $isOnMain) {
        throw "Release provenance mismatch: tag=$peeled HEAD=$head $MainRef=$main expected=$ExpectedCommit."
    }
}
Write-Output "PASS: release tag $Tag matches package metadata, README, and changelog."
