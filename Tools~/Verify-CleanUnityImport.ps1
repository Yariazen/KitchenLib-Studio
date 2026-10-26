[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [string]$UnityPath = $env:UNITY_PATH,

    [string]$WorkingRoot = ([System.IO.Path]::GetTempPath())
)

$ErrorActionPreference = 'Stop'
$PackageRoot = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd([char[]]@('/', '\'))
$WorkingRoot = [System.IO.Path]::GetFullPath($WorkingRoot).TrimEnd([char[]]@('/', '\'))
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    throw 'UnityPath is required (or set UNITY_PATH) and must point to Unity 2020.3.48f1.'
}
$UnityPath = [System.IO.Path]::GetFullPath($UnityPath)
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable does not exist: $UnityPath"
}

function Get-PackageSnapshot {
    $snapshot = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -Force | Where-Object {
        $_.FullName -cne (Join-Path $PackageRoot '.git') -and
        $_.FullName -notlike (Join-Path $PackageRoot '.git\*')
    }) {
        $relative = $file.FullName.Substring($PackageRoot.Length).TrimStart([char[]]@('/', '\')).Replace('\', '/')
        $snapshot[$relative] = "$($file.Length):$($file.LastWriteTimeUtc.Ticks):$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
    }
    return $snapshot
}

function Compare-Snapshots($Before, $After) {
    $beforeJson = $Before | ConvertTo-Json -Compress
    $afterJson = $After | ConvertTo-Json -Compress
    if ($beforeJson -cne $afterJson) {
        $allPaths = @($Before.Keys) + @($After.Keys) | Sort-Object -Unique
        $changes = @($allPaths | Where-Object { $Before[$_] -cne $After[$_] })
        throw "Unity wrote into the package checkout: $($changes -join ', ')"
    }
}

function Remove-RunRoot([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $lastError = $null
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds 500
        }
    }
    throw "Could not remove the external verification directory: $Path. $($lastError.Exception.Message)"
}

$runRoot = Join-Path $WorkingRoot ('KitchenLibStudio-Import-' + [System.Guid]::NewGuid().ToString('N'))
$projectPath = Join-Path $runRoot 'Project'
$logPath = Join-Path $runRoot 'unity.log'
$before = Get-PackageSnapshot
try {
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    & (Join-Path $PSScriptRoot 'New-UnityVerificationProject.ps1') -PackageRoot $PackageRoot -ProjectPath $projectPath

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $UnityPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in @('-batchmode', '-nographics', '-quit', '-projectPath', $projectPath, '-logFile', $logPath)) {
        $startInfo.ArgumentList.Add($argument)
    }
    $unityProcess = [System.Diagnostics.Process]::Start($startInfo)
    if (-not $unityProcess.WaitForExit(600000)) {
        $unityProcess.Kill()
        $unityProcess.WaitForExit()
        throw 'Unity import timed out after 10 minutes.'
    }
    $unityExitCode = $unityProcess.ExitCode
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        throw 'Unity did not produce an import log.'
    }
    $log = Get-Content -LiteralPath $logPath -Raw
    if ($unityExitCode -ne 0) { throw "Unity import failed with exit code $unityExitCode.`n$log" }
    if ($log -notmatch "Version is '2020\.3\.48f1") {
        throw 'Unity executable is not version 2020.3.48f1.'
    }

    $failurePatterns = @(
        '(?im)^.*\berror CS\d+\b.*$',
        '(?im)^.*\bwarning CS\d+\b.*$',
        '(?im)^.*Scripts have compiler errors.*$',
        '(?im)^.*Compilation failed.*$',
        '(?im)^.*Failed to resolve packages.*$',
        '(?im)^.*An error occurred while resolving packages.*$',
        '(?im)^.*Cannot resolve package.*$',
        '(?im)^.*Assembly [^\r\n]* (?:not found|could not be resolved).*$'
    )
    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($pattern in $failurePatterns) {
        foreach ($match in [regex]::Matches($log, $pattern)) {
            $problems.Add($match.Value.Trim())
        }
    }
    if ($problems.Count -gt 0) {
        throw "Unity import log contains compiler/package failures:`n$(@($problems | Sort-Object -Unique) -join "`n")"
    }
}
finally {
    try {
        $after = Get-PackageSnapshot
        Compare-Snapshots $before $after
    }
    finally {
        Remove-RunRoot $runRoot
    }
}

Write-Output 'PASS: clean Unity 2020.3.48f1 package import completed with no package checkout writes or compiler/package errors.'
