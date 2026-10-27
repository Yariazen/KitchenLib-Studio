[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ChangelogPath,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$DestinationPath
)

$ErrorActionPreference = 'Stop'
$lines = @(Get-Content -LiteralPath $ChangelogPath)
$heading = "## [$Version]"
$start = [Array]::IndexOf($lines, $heading)
if ($start -lt 0) { throw "Changelog section for version $Version was not found." }
$end = $lines.Count
for ($index = $start + 1; $index -lt $lines.Count; $index++) {
    if ($lines[$index].StartsWith('## ', [StringComparison]::Ordinal)) { $end = $index; break }
}
$body = @($lines[($start + 1)..($end - 1)])
while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[0])) { $body = @($body | Select-Object -Skip 1) }
while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[-1])) { $body = @($body | Select-Object -First ($body.Count - 1)) }
if ($body.Count -eq 0) { throw "Changelog section for version $Version is empty." }
$parent = Split-Path -Parent ([IO.Path]::GetFullPath($DestinationPath))
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
[IO.File]::WriteAllText([IO.Path]::GetFullPath($DestinationPath), ($body -join "`n") + "`n",
    [Text.UTF8Encoding]::new($false))
Write-Output "PASS: wrote release notes for $Version to $DestinationPath"
