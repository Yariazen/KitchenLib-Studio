[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd([char[]]@('/', '\'))
$DestinationPath = [System.IO.Path]::GetFullPath($DestinationPath)
$destinationParent = Split-Path -Parent $DestinationPath
$pathComparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }

if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot 'package.json') -PathType Leaf)) {
    throw "PackageRoot is not a Unity package: $PackageRoot"
}
if ($DestinationPath.StartsWith($PackageRoot + [System.IO.Path]::DirectorySeparatorChar, $pathComparison)) {
    throw 'DestinationPath must be outside the package checkout.'
}
if ([System.IO.Path]::GetExtension($DestinationPath) -cne '.zip') {
    throw 'DestinationPath must end in .zip.'
}
if (Test-Path -LiteralPath $DestinationPath) {
    if (-not $Force) { throw "DestinationPath already exists: $DestinationPath" }
}
if (-not (Test-Path -LiteralPath $destinationParent -PathType Container)) {
    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
}

$packageGate = Join-Path $PackageRoot 'Tools~/Verify-Package.ps1'
if (-not (Test-Path -LiteralPath $packageGate -PathType Leaf)) {
    throw 'Package verification gate is missing; no release archive was created.'
}
$powerShellExecutable = (Get-Process -Id $PID).Path
$gateOutput = @(& $powerShellExecutable -NoProfile -File $packageGate 2>&1 | ForEach-Object { $_.ToString() })
$gateExitCode = $LASTEXITCODE
foreach ($line in $gateOutput) { Write-Output $line }
if ($gateExitCode -ne 0) {
    throw "Package verification failed with exit code $gateExitCode; no release archive was created."
}

$rootAllowlist = @(
    'package.json',
    'package.json.meta',
    'README.md',
    'README.md.meta',
    'CHANGELOG.md',
    'CHANGELOG.md.meta',
    'LICENSE.md',
    'LICENSE.md.meta',
    'Third Party Notices.md',
    'Third Party Notices.md.meta'
)

function Get-RelativePath([string]$Path) {
    return $Path.Substring($PackageRoot.Length).TrimStart([char[]]@('/', '\')).Replace('\', '/')
}

$packageFiles = @(Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -Force | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; Relative = Get-RelativePath $_.FullName }
} | Where-Object {
    $_.Relative -in $rootAllowlist -or
    $_.Relative.StartsWith('Documentation~/', [System.StringComparison]::Ordinal) -or
    $_.Relative.StartsWith('Editor/', [System.StringComparison]::Ordinal)
} | Sort-Object Relative)

foreach ($required in $rootAllowlist) {
    if ($required -notin @($packageFiles.Relative)) { throw "Required release file is missing: $required" }
}
if (-not ($packageFiles | Where-Object { $_.Relative.StartsWith('Documentation~/', [System.StringComparison]::Ordinal) })) {
    throw 'Release allowlist contains no Documentation~ files.'
}
if (-not ($packageFiles | Where-Object { $_.Relative.StartsWith('Editor/', [System.StringComparison]::Ordinal) })) {
    throw 'Release allowlist contains no Editor files.'
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('KitchenLibStudio-Release-' + [System.Guid]::NewGuid().ToString('N'))
$temporaryArchive = Join-Path $destinationParent ('.' + [IO.Path]::GetFileName($DestinationPath) + '.' +
    [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    New-Item -ItemType Directory -Path $stagingRoot | Out-Null
    foreach ($file in $packageFiles) {
        $target = Join-Path $stagingRoot ($file.Relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $targetParent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $targetParent -PathType Container)) {
            New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
        }
        Copy-Item -LiteralPath $file.Source -Destination $target
    }

    Add-Type -AssemblyName System.IO.Compression
    $fixedTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $archiveStream = [IO.File]::Open($temporaryArchive, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $deterministicArchive = [IO.Compression.ZipArchive]::new(
            $archiveStream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            foreach ($file in $packageFiles) {
                $entry = $deterministicArchive.CreateEntry(
                    $file.Relative, [IO.Compression.CompressionLevel]::NoCompression)
                $entry.LastWriteTime = $fixedTimestamp
                $entry.ExternalAttributes = 0
                $stagedPath = Join-Path $stagingRoot ($file.Relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
                $input = [IO.File]::OpenRead($stagedPath)
                $output = $entry.Open()
                try { $input.CopyTo($output) }
                finally { $output.Dispose(); $input.Dispose() }
            }
        }
        finally { $deterministicArchive.Dispose() }
    }
    finally { $archiveStream.Dispose() }

    & (Join-Path $PackageRoot 'Tools~/Set-DeterministicZipMetadata.ps1') -Path $temporaryArchive | Write-Output

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($temporaryArchive)
    try {
        $actualEntries = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | ForEach-Object {
            $_.FullName.Replace('\', '/')
        } | Sort-Object)
    }
    finally {
        $archive.Dispose()
    }
    $expectedEntries = @($packageFiles.Relative | Sort-Object)
    if (($actualEntries -join '|') -cne ($expectedEntries -join '|')) {
        throw 'Release archive entries differ from the explicit package allowlist.'
    }
    [IO.File]::Move($temporaryArchive, $DestinationPath, $true)
}
finally {
    if (Test-Path -LiteralPath $temporaryArchive) {
        Remove-Item -LiteralPath $temporaryArchive -Force
    }
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Write-Output "PASS: release archive contains $($packageFiles.Count) allowlisted package file(s): $DestinationPath"
