[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$Message) {
    $script:failures.Add($Message)
}

function Relative-Path([string]$Path) {
    return $Path.Substring($packageRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
}

function Require-File([string]$RelativePath) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $RelativePath) -PathType Leaf)) {
        Add-Failure "Missing required file: $RelativePath"
    }
}

$requiredFiles = @(
    'package.json',
    'package.json.meta',
    'CHANGELOG.md',
    'CHANGELOG.md.meta',
    'LICENSE.md',
    'LICENSE.md.meta',
    'Third Party Notices.md',
    'Third Party Notices.md.meta',
    'Documentation~/index.md',
    'Editor/KitchenLib.Studio.Editor.asmdef',
    'Editor/Bootstrap/KitchenLib.Studio.Bootstrap.asmdef',
    'Editor/ThirdParty/AssetsTools.NET/LICENSE-AssetsTools.NET.txt',
    'Editor/ThirdParty/AssetsTools.NET/LICENSE-Mono.Cecil.txt'
)
$requiredFiles | ForEach-Object { Require-File $_ }

$allFiles = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Force | Where-Object {
    $_.FullName -notlike (Join-Path $packageRoot '.git\*')
})
$relativeFiles = @($allFiles | ForEach-Object { Relative-Path $_.FullName })

$forbiddenRoots = @('Assets', 'Packages', 'ProjectSettings', 'docs', 'Samples~', 'Tests', '.agents', '.codex', '.superpowers', '.worktrees')
foreach ($root in $forbiddenRoots) {
    if ($relativeFiles | Where-Object { $_ -eq $root -or $_.StartsWith($root + '/', [System.StringComparison]::OrdinalIgnoreCase) }) {
        Add-Failure "Forbidden repository/package root: $root"
    }
}

$forbiddenNames = @('AGENTS.md', 'CLAUDE.md', '.collabignore')
foreach ($name in $forbiddenNames) {
    if ($relativeFiles | Where-Object { [System.IO.Path]::GetFileName($_).Equals($name, [System.StringComparison]::OrdinalIgnoreCase) }) {
        Add-Failure "Forbidden agent/tool file: $name"
    }
}

foreach ($path in $relativeFiles) {
    if ($path -match '(?i)(^|/)(test|tests|proof|sample|samples|fixtures?)(/|$)' -or
        [System.IO.Path]::GetFileNameWithoutExtension($path) -match '(?i)(test|proof|fixture|sample)') {
        Add-Failure "Internal test/proof/sample content is forbidden: $path"
    }
    if ([System.IO.Path]::GetExtension($path) -match '(?i)^\.(asset|prefab|unity)$') {
        Add-Failure "Imported/generated Unity content is forbidden: $path"
    }
    if ($path -match '(?i)(plateup-schema\.json|catalog(?:ue)?\.json|KitchenLibStudio\.Generated)') {
        Add-Failure "Generated/imported game data is forbidden: $path"
    }
}

$textExtensions = @('.cs', '.json', '.md', '.txt', '.asmdef', '.ps1', '.yml', '.yaml', '.uss', '.meta', '.gitignore', '.gitattributes')
foreach ($file in $allFiles) {
    $relative = Relative-Path $file.FullName
    if ($relative -eq 'Tools~/Verify-Package.ps1') { continue }
    $extension = [System.IO.Path]::GetExtension($file.Name).ToLowerInvariant()
    if ($file.Name -notin @('.gitignore', '.gitattributes') -and $extension -notin $textExtensions) { continue }
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match '(?i)\b(?:unity[- ]?editor[- ]?mcp|mcp[-_. ]?for[-_. ]?unity)\b') {
        Add-Failure "MCP dependency/configuration is forbidden: $relative"
    }
    if ($content -match '(?i)(?:[A-Z]:\\Users\\|/Users/[^/]+/|/home/[^/]+/)') {
        Add-Failure "Machine-local absolute path is forbidden: $relative"
    }
    if ($content -match '(?i)(?:docs/superpowers|\.agents/|\.codex/|\.superpowers/|\.worktrees/)') {
        Add-Failure "Agentic/planning reference is forbidden: $relative"
    }
}

try {
    $package = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
    if ($package.name -cne 'com.kitchenlib.studio') { Add-Failure "package.json name must be com.kitchenlib.studio." }
    if ($package.version -cne '0.1.0') { Add-Failure "package.json version must be 0.1.0." }
    if ($package.displayName -cne 'KitchenLib Studio') { Add-Failure "package.json displayName must be KitchenLib Studio." }
    if ($package.unity -cne '2020.3') { Add-Failure "package.json unity must be 2020.3." }
    if ($package.license -cne 'SEE LICENSE IN LICENSE.md') { Add-Failure "package.json must point to LICENSE.md." }
    $expectedDependencies = [ordered]@{
        'com.unity.textmeshpro' = '3.0.6'
        'com.unity.modules.assetbundle' = '1.0.0'
        'com.unity.modules.imageconversion' = '1.0.0'
        'com.unity.modules.imgui' = '1.0.0'
        'com.unity.modules.jsonserialize' = '1.0.0'
        'com.unity.modules.physics' = '1.0.0'
        'com.unity.modules.uielements' = '1.0.0'
    }
    $actualDependencies = @($package.dependencies.psobject.Properties)
    if ($actualDependencies.Count -ne $expectedDependencies.Count) {
        Add-Failure "package.json must contain only the audited production dependencies."
    }
    foreach ($dependency in $expectedDependencies.GetEnumerator()) {
        $property = $package.dependencies.psobject.Properties[$dependency.Key]
        if ($null -eq $property -or $property.Value -cne $dependency.Value) {
            Add-Failure "Missing or invalid dependency: $($dependency.Key)@$($dependency.Value)"
        }
    }
}
catch {
    Add-Failure "package.json is invalid: $($_.Exception.Message)"
}

$allowedBinaries = @(
    'Editor/ThirdParty/AssetsTools.NET/AssetsTools.NET.dll',
    'Editor/ThirdParty/AssetsTools.NET/AssetsTools.NET.MonoCecil.dll',
    'Editor/ThirdParty/AssetsTools.NET/Mono.Cecil.dll',
    'Editor/ThirdParty/AssetsTools.NET/Mono.Cecil.Rocks.dll'
)
$actualBinaries = @($relativeFiles | Where-Object { [System.IO.Path]::GetExtension($_) -match '(?i)^\.(dll|exe|so|dylib)$' })
foreach ($binary in $actualBinaries) {
    if ($binary -notin $allowedBinaries) { Add-Failure "Forbidden binary: $binary" }
}
foreach ($binary in $allowedBinaries) {
    if ($binary -notin $relativeFiles) { Add-Failure "Missing audited binary: $binary" }
}

$expectedHashes = [ordered]@{
    'Editor/ThirdParty/AssetsTools.NET/AssetsTools.NET.dll' = '6B03D589163623B67620FBBC6F425DCB47F856584BD3AB1A34C896B2666372E4'
    'Editor/ThirdParty/AssetsTools.NET/AssetsTools.NET.MonoCecil.dll' = 'D9358259FACAF85356ED3BDEC5AEBFD9C026ECA05EF28E6413B85935770ACE67'
    'Editor/ThirdParty/AssetsTools.NET/Mono.Cecil.dll' = '831DCA77470D85CB6FFBEA3072DAA7A3DF5B7C9FCFD9C3F43674A9BE99D4BFCF'
    'Editor/ThirdParty/AssetsTools.NET/Mono.Cecil.Rocks.dll' = 'BF992F3DCE364EBCC3200FA7832EF07E20B4E2DBC3A8A6213CE44E3D239DB984'
    'Editor/ThirdParty/AssetsTools.NET/classdata.tpk' = '3FAB18495D13E559E0FB9926575AFBD682A7AE780FE1569E351A6123E0059150'
}
foreach ($entry in $expectedHashes.GetEnumerator()) {
    $path = Join-Path $packageRoot $entry.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Failure "Missing audited third-party payload: $($entry.Key)"
        continue
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $entry.Value) {
        Add-Failure "Third-party payload hash changed without a completed audit: $($entry.Key)"
    }
}

try {
    $editorAssembly = Get-Content -LiteralPath (Join-Path $packageRoot 'Editor/KitchenLib.Studio.Editor.asmdef') -Raw | ConvertFrom-Json
    $bootstrapAssembly = Get-Content -LiteralPath (Join-Path $packageRoot 'Editor/Bootstrap/KitchenLib.Studio.Bootstrap.asmdef') -Raw | ConvertFrom-Json
    $expectedEditorPrecompiled = @('AssetsTools.NET.dll', 'AssetsTools.NET.MonoCecil.dll', 'Mono.Cecil.dll', 'Mono.Cecil.Rocks.dll')
    if ((@($editorAssembly.precompiledReferences | Sort-Object) -join '|') -cne (@($expectedEditorPrecompiled | Sort-Object) -join '|')) {
        Add-Failure 'Editor asmdef precompiled references differ from the audited set.'
    }
    if (@($bootstrapAssembly.precompiledReferences) -join '|' -cne 'Mono.Cecil.dll') {
        Add-Failure 'Bootstrap asmdef must reference only Mono.Cecil.dll as a precompiled dependency.'
    }
    foreach ($assembly in @($editorAssembly, $bootstrapAssembly)) {
        if (@($assembly.includePlatforms) -join '|' -cne 'Editor') { Add-Failure "$($assembly.name) must be Editor-only." }
        if (-not $assembly.overrideReferences) { Add-Failure "$($assembly.name) must use explicit precompiled references." }
    }
}
catch {
    Add-Failure "Assembly definition audit failed: $($_.Exception.Message)"
}

$licenseText = if (Test-Path -LiteralPath (Join-Path $packageRoot 'LICENSE.md')) { Get-Content -LiteralPath (Join-Path $packageRoot 'LICENSE.md') -Raw } else { '' }
if ($licenseText -match '(?i)release-blocking placeholder|all rights reserved') {
    Add-Failure 'Original KitchenLib Studio license is still the all-rights-reserved release-blocking placeholder.'
}

$notices = if (Test-Path -LiteralPath (Join-Path $packageRoot 'Third Party Notices.md')) { Get-Content -LiteralPath (Join-Path $packageRoot 'Third Party Notices.md') -Raw } else { '' }
foreach ($component in @('AssetsTools.NET 3.0.5', 'AssetsTools.NET.MonoCecil 3.0.4', 'Mono.Cecil 0.11.6')) {
    if ($notices -notmatch [regex]::Escape($component)) { Add-Failure "Third-party notice is incomplete: $component" }
}
if ($notices -notmatch '(?i)classdata\.tpk[\s\S]*redistribution terms.*unresolved') {
    Add-Failure 'classdata.tpk provenance/license notice must explicitly identify unresolved redistribution terms.'
}
Add-Failure 'classdata.tpk redistribution license and immutable upstream revision are unresolved; public distribution is blocked.'

if ($failures.Count -gt 0) {
    foreach ($failure in $failures | Sort-Object -Unique) { Write-Error "FAIL: $failure" -ErrorAction Continue }
    Write-Error "FAIL: package verification found $($failures.Count) issue(s)." -ErrorAction Continue
    exit 1
}

Write-Output 'PASS: com.kitchenlib.studio@0.1.0 package and repository are production-only.'
exit 0
