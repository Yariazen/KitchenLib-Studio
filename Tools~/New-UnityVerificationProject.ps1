[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
$PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd([char[]]@('/', '\'))
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd([char[]]@('/', '\'))
$pathComparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }

if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot 'package.json') -PathType Leaf)) {
    throw "PackageRoot is not a Unity package: $PackageRoot"
}
if ($ProjectPath.Equals($PackageRoot, $pathComparison) -or
    $ProjectPath.StartsWith($PackageRoot + [System.IO.Path]::DirectorySeparatorChar, $pathComparison)) {
    throw 'ProjectPath must be outside the package checkout.'
}
if (Test-Path -LiteralPath $ProjectPath) {
    if (Get-ChildItem -LiteralPath $ProjectPath -Force | Select-Object -First 1) {
        throw "ProjectPath must not exist or must be empty: $ProjectPath"
    }
}

$assetsPath = Join-Path $ProjectPath 'Assets'
$packagesPath = Join-Path $ProjectPath 'Packages'
$settingsPath = Join-Path $ProjectPath 'ProjectSettings'
New-Item -ItemType Directory -Path $assetsPath, $packagesPath, $settingsPath -Force | Out-Null

$packageUri = 'file:' + $PackageRoot.Replace('\', '/')
$manifest = [ordered]@{
    dependencies = [ordered]@{
        'com.kitchenlib.studio' = $packageUri
    }
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText(
    (Join-Path $packagesPath 'manifest.json'),
    $manifestJson + "`n",
    [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText(
    (Join-Path $settingsPath 'ProjectVersion.txt'),
    "m_EditorVersion: 2020.3.48f1`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Created minimal Unity 2020.3.48f1 verification project: $ProjectPath"
