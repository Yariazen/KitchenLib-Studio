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
    if ($package.version -cne '0.1.0-alpha.1') { Add-Failure "package.json version must be 0.1.0-alpha.1." }
    if ($package.displayName -cne 'KitchenLib Studio') { Add-Failure "package.json displayName must be KitchenLib Studio." }
    if ($package.unity -cne '2020.3') { Add-Failure "package.json unity must be 2020.3." }
    if ($package.license -cne 'MIT') { Add-Failure 'package.json license must be MIT.' }
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

# Source-level public API audit uses the C# parser bundled with PowerShell 7, not regex visibility guesses.
# Signature fingerprints include imports, type/base declarations, public/protected members, parameters,
# accessors, enum values and constants, but exclude implementation bodies and documentation trivia.
function Get-SignatureTokens($Node, [int]$End = [int]::MaxValue) {
    return (@($Node.DescendantTokens() | Where-Object { $_.SpanStart -lt $End } | ForEach-Object { $_.Text }) -join ' ')
}

function Get-PublicTypes([string]$Source, [string]$Path) {
    $syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($Source).GetRoot()
    $symbols = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($trivia in $syntax.DescendantTrivia($null, $true)) {
        if (-not $trivia.HasStructure) { continue }
        $directive = $trivia.GetStructure()
        if ($directive.GetType().Name -notin @('IfDirectiveTriviaSyntax', 'ElifDirectiveTriviaSyntax')) { continue }
        foreach ($token in $directive.Condition.DescendantTokens()) {
            if ($token.RawKind -eq [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::IdentifierToken) {
                [void]$symbols.Add($token.ValueText)
            }
        }
    }
    # Parse each actual configuration. Flattening branches can change type nesting and hide a leak.
    # Fail closed rather than silently dropping configurations when the bounded audit is exceeded.
    if ($symbols.Count -gt 8) { Add-Failure "C# conditional audit exceeds eight symbols: $Path"; return }
    $names = @($symbols)
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($mask = 0; $mask -lt [math]::Pow(2, $names.Count); $mask++) {
        [string[]]$enabled = @(for ($index = 0; $index -lt $names.Count; $index++) {
            if ($mask -band (1 -shl $index)) { $names[$index] }
        })
        $options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithPreprocessorSymbols($enabled)
        $syntax = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($Source, $options).GetRoot()
        if (@($syntax.GetDiagnostics() | Where-Object { $_.Severity.ToString() -eq 'Error' }).Count -gt 0) {
            Add-Failure "Invalid C# syntax in a conditional configuration: $Path"; continue
        }
        foreach ($type in Get-PublicTypesForSyntax $syntax $Path) {
            if ($seen.Add($type.Name + "`0" + $type.Hash + "`0" + $type.Position)) { $type }
        }
    }
}

function Get-PublicTypesForSyntax($syntax, [string]$Path) {
    foreach ($node in $syntax.DescendantNodes()) {
        if ($node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] -and
            $node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax]) { continue }
        $ancestors = @($node.Ancestors())
        $containers = @($ancestors | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] })
        $visible = $true
        foreach ($part in @($node) + $containers) {
            $modifiers = @($part.Modifiers | ForEach-Object { $_.ValueText })
            if ('public' -notin $modifiers -and 'protected' -notin $modifiers -and
                $part.Parent -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax]) { $visible = $false }
        }
        if (-not $visible) { continue }
        $namespaces = @($ancestors | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax] } | ForEach-Object { $_.Name.ToString() })
        [array]::Reverse($namespaces)
        $parents = @($containers | ForEach-Object { $_.Identifier.ValueText })
        [array]::Reverse($parents)
        $name = (@($namespaces) + @($parents) + @($node.Identifier.ValueText)) -join '.'
        $parts = [System.Collections.Generic.List[string]]::new()
        foreach ($using in $syntax.DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax] }) {
            $parts.Add((Get-SignatureTokens $using))
        }
        if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax]) {
            $parts.Add((Get-SignatureTokens $node))
        }
        else {
            $parts.Add((Get-SignatureTokens $node $node.OpenBraceToken.SpanStart))
            foreach ($member in $node.Members) {
                if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax]) {
                    $parts.Add((Get-SignatureTokens $member)); continue
                }
                $mods = @($member.Modifiers | ForEach-Object { $_.ValueText })
                if ('public' -notin $mods -and 'protected' -notin $mods -and
                    $node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax]) { continue }
                if ($member -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax]) { continue }
                if ($member -is [Microsoft.CodeAnalysis.CSharp.Syntax.BasePropertyDeclarationSyntax]) {
                    $end = if ($null -ne $member.AccessorList) { $member.AccessorList.SpanStart } else { $member.ExpressionBody.SpanStart }
                    $parts.Add((Get-SignatureTokens $member $end))
                    if ($null -ne $member.AccessorList) {
                        foreach ($accessor in $member.AccessorList.Accessors) {
                            $parts.Add((@($accessor.Modifiers | ForEach-Object { $_.Text }) + @($accessor.Keyword.Text)) -join ' ')
                        }
                    } else { $parts.Add('get') }
                }
                elseif ($member -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseMethodDeclarationSyntax]) {
                    $end = if ($null -ne $member.Body) { $member.Body.SpanStart } elseif ($null -ne $member.ExpressionBody) { $member.ExpressionBody.SpanStart } else { $member.SemicolonToken.SpanStart }
                    $parts.Add((Get-SignatureTokens $member $end))
                }
                else { $parts.Add((Get-SignatureTokens $member)) }
            }
        }
        $signature = $parts -join "`n"
        $hasher = [System.Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($signature))).Replace('-', '') }
        finally { $hasher.Dispose() }
        [pscustomobject]@{ Name = $name; Hash = $hash; Path = $Path; Position = $node.SpanStart }
    }
}

# Intentional supported contracts. The three serialized roots remain internal and need no public exemption.
$supportedPublicTypes = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
foreach ($entry in @{
    'KitchenLib.Studio.Editor.Api.Authoring.StudioAuthoringValue' = '8599F7EBA3E095AD20B856607D81DC2455727F4C426432A89D4015302EDD32AE'
    'KitchenLib.Studio.Editor.Api.Authoring.StudioAuthoringValueKind' = '7D873C9859D2C74DE14055B89F83032CA4ABE99034BFE4A3D85C363B9C68014E'
    'KitchenLib.Studio.Editor.Api.Build.IStudioBuildProcessor' = '372F445B2001E2275F850A4B010E30F28FCFEE7E977B244D4F2200BB00DEB942'
    'KitchenLib.Studio.Editor.Api.Build.IStudioGraphNodeProjector' = '95AA647463C091A85A27AD6A0D0C702B0EAD06A88CDE4670B094C70ACBA2B724'
    'KitchenLib.Studio.Editor.Api.Build.StudioBuildContext' = '6273C91F57B83516E6D16C5F82F422A9F7A3E43BE7C504C72D39E1518A98CAC1'
    'KitchenLib.Studio.Editor.Api.Build.StudioBuildFieldChange' = 'AAA3BB2C411CB2E773405C62D2C9379EEA5041697C4E852CCE417BA9C1E220CC'
    'KitchenLib.Studio.Editor.Api.Build.StudioBuildProcessorResult' = '33AA7ADF4013618B855EC75E261300EF529E3044D85DC021E5F628D3EB0DF481'
    'KitchenLib.Studio.Editor.Api.Build.StudioBuildProjection' = 'A938807C297DE532CA290001F08C478E62B1CECFAFC43A6508740252C57D803D'
    'KitchenLib.Studio.Editor.Api.Graph.IStudioGraphNodeProvider' = 'B4FAC62F771D59F2B7BACED076EEE213D4FFE31E7A84C739F7EAED42768C4638'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphConnection' = '0EB79715617A3CF23D8E88E66DFAC217EF7CB6CE722B0D1558ABEDBB52F4DDBC'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphEndpoint' = '475A01007AAD58A386063647E33EDEBCB416B09CB87F54B4A229BFDD576BDFC3'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphFieldDefinition' = '40DD852ACCD3A2DA2C2DA580C01AF339159AE4DCB16F81DB841EAA41358D3C39'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphNodeData' = '09623423F689A61D2A0D43F32297E884783B759B191487C39531D7EE0E2AA7A2'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphNodeDefinition' = '917F7E94D15695A99D54D81693579EC9BE2538FE98D0CBEBF99622EAD42FF375'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphPortCardinality' = '77DE78C3A5C046F66A289497B0453520FBDAFEE161FD74D7246E2DFD2F6DBB4E'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphPortDefinition' = '9380667FD25D8BA03FDFC13EABBE75DC11B357018F3DE34026EC095FB79D0A96'
    'KitchenLib.Studio.Editor.Api.Graph.StudioGraphPortDirection' = '97C5C49414F58CA5DB23B74E5631D6B7E76604CCF46D75D515457A9CEE20252C'
    'KitchenLib.Studio.Editor.Api.KitchenLibStudioApi' = 'B786B5E87A97B91C49D0AED9D13250E5C6B69343EDDA3AE26544C26D53D38702'
    'KitchenLib.Studio.Editor.Api.Validation.IStudioGraphNodeValidator' = 'C38C8A1EA8B07E42DDAA745CE06FD19D9B602B0E92E5945AA5CAA93192535217'
    'KitchenLib.Studio.Editor.Api.Validation.StudioValidationContext' = '925F5D628AC71EFEE861989C427734289E3990AB0C4E9B24CB89030F19D9111E'
    'KitchenLib.Studio.Editor.Api.Validation.StudioValidationFinding' = '83CD430F75947C5B0380D959B87BBC7952B816FC9A1196A5745ED58220950988'
    'KitchenLib.Studio.Editor.Api.Validation.StudioValidationSeverity' = 'CD6339E3B9E8EF39F41D4280DC8E876FD96612E3C4E59B105382A71D20B74473'
}.GetEnumerator()) { $supportedPublicTypes.Add($entry.Key, $entry.Value) }
try {
    $publicTypes = @(foreach ($file in $allFiles | Where-Object {
        $_.Extension -ieq '.cs' -and -not (Relative-Path $_.FullName).StartsWith('Editor/Generation/Templates~/', [StringComparison]::Ordinal)
    }) {
        $path = Relative-Path $file.FullName
        if (-not $path.StartsWith('Editor/', [StringComparison]::Ordinal)) {
            Add-Failure "C# source is outside the authorized Editor tree: $path"
        }
        Get-PublicTypes (Get-Content -LiteralPath $file.FullName -Raw) $path
    })
    foreach ($type in $publicTypes) {
        if (-not $supportedPublicTypes.ContainsKey($type.Name) -or -not $type.Path.StartsWith('Editor/Api/', [StringComparison]::Ordinal)) {
            Add-Failure "Unsupported public implementation type: $($type.Name) ($($type.Path))"
        } elseif ($supportedPublicTypes[$type.Name] -cne $type.Hash) {
            Add-Failure "Supported API signature changed without an explicit surface audit: $($type.Name)"
        }
    }
    foreach ($name in $supportedPublicTypes.Keys) {
        if (@($publicTypes | Where-Object { $_.Name -ceq $name }).Count -ne 1) {
            Add-Failure "Supported API type must occur exactly once: $name"
        }
    }
}
catch { Add-Failure "Public API audit failed (PowerShell 7 with its bundled C# parser is required): $($_.Exception.Message)" }

$licenseText = if (Test-Path -LiteralPath (Join-Path $packageRoot 'LICENSE.md')) { Get-Content -LiteralPath (Join-Path $packageRoot 'LICENSE.md') -Raw } else { '' }
$normalizedLicense = [regex]::Replace($licenseText, '\s+', ' ').Trim()
$mitGrant = 'Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:'
$mitCondition = 'The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.'
$mitWarranty = 'THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.'
if ($normalizedLicense -cnotmatch [regex]::Escape('MIT License') -or
    $normalizedLicense -cnotmatch [regex]::Escape('Copyright (c) 2026 Yariazen') -or
    -not $normalizedLicense.Contains($mitGrant) -or
    -not $normalizedLicense.Contains($mitCondition) -or
    -not $normalizedLicense.Contains($mitWarranty)) {
    Add-Failure 'LICENSE.md must contain the canonical 2026 Yariazen MIT permission, condition, and warranty text.'
}
if ($normalizedLicense -notmatch '(?i)only to original KitchenLib Studio code and documentation' -or
    $normalizedLicense -notmatch '(?i)Third Party Notices\.md') {
    Add-Failure 'LICENSE.md must scope MIT to original KitchenLib Studio code and documentation and refer to Third Party Notices.md.'
}

$notices = if (Test-Path -LiteralPath (Join-Path $packageRoot 'Third Party Notices.md')) { Get-Content -LiteralPath (Join-Path $packageRoot 'Third Party Notices.md') -Raw } else { '' }
if ($notices -notmatch '(?i)not relicensed under the KitchenLib Studio MIT license') {
    Add-Failure 'Third Party Notices.md must say bundled third-party material is not relicensed under the KitchenLib Studio MIT license.'
}
foreach ($entry in $expectedHashes.GetEnumerator()) {
    $payload = [System.IO.Path]::GetFileName($entry.Key)
    if ($notices -notmatch "(?m)^\| $([regex]::Escape($payload)) \|[^`r`n]*$($entry.Value)[^`r`n]*\|") {
        Add-Failure "Third-party notice must inventory the exact audited payload and SHA-256: $payload"
    }
}
$payloadRows = @($notices -split "`r?`n" | Where-Object { $_ -match '^\| (?:[^|]+\.(?:dll|tpk)) \|' })
if ($payloadRows.Count -ne $expectedHashes.Count) {
    Add-Failure 'Third Party Notices.md must inventory exactly five audited payloads.'
}
foreach ($component in @('AssetsTools.NET 3.0.5', 'AssetsTools.NET.MonoCecil 3.0.4', 'Mono.Cecil 0.11.6')) {
    if ($notices -notmatch [regex]::Escape($component)) { Add-Failure "Third-party notice is incomplete: $component" }
}
foreach ($provenance in @(
    '94549d10ee0fbb90392e45e555861031ff560359',
    '39096267408',
    '12124827219',
    '20ba06e00e0625de0e49a502c6868d6cc86d0f0b')) {
    if ($notices -notmatch [regex]::Escape($provenance)) { Add-Failure "classdata.tpk provenance is incomplete: $provenance" }
}
if ($notices -notmatch '(?i)TypeTreeDumps[\s\S]*no declared repository license[\s\S]*Exact source provenance and contributor acknowledgment are preserved') {
    Add-Failure 'classdata.tpk notice must acknowledge TypeTreeDumps and preserve its exact source provenance.'
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures | Sort-Object -Unique) { Write-Error "FAIL: $failure" -ErrorAction Continue }
    Write-Error "FAIL: package verification found $($failures.Count) issue(s)." -ErrorAction Continue
    exit 1
}

Write-Output 'PASS: com.kitchenlib.studio@0.1.0-alpha.1 package and repository are production-only.'
exit 0
