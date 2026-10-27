[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Path)

$ErrorActionPreference = 'Stop'
$Path = [IO.Path]::GetFullPath($Path)
$bytes = [IO.File]::ReadAllBytes($Path)
function UInt16([int]$Offset) { return [BitConverter]::ToUInt16($bytes, $Offset) }
function UInt32([int]$Offset) { return [BitConverter]::ToUInt32($bytes, $Offset) }

$eocd = -1
for ($index = $bytes.Length - 22; $index -ge [Math]::Max(0, $bytes.Length - 65557); $index--) {
    if ((UInt32 $index) -eq 0x06054b50) { $eocd = $index; break }
}
if ($eocd -lt 0) { throw 'ZIP end-of-central-directory record was not found.' }
$entryCount = UInt16 ($eocd + 10)
$centralSize = UInt32 ($eocd + 12)
$centralOffset = UInt32 ($eocd + 16)
if ($centralOffset + $centralSize -gt $eocd) { throw 'ZIP central directory bounds are invalid.' }
$cursor = [int]$centralOffset
for ($entryIndex = 0; $entryIndex -lt $entryCount; $entryIndex++) {
    if ($cursor + 46 -gt $bytes.Length -or (UInt32 $cursor) -ne 0x02014b50) {
        throw "ZIP central directory entry $entryIndex is invalid."
    }
    $bytes[$cursor + 5] = 0
    $nameLength = UInt16 ($cursor + 28)
    $extraLength = UInt16 ($cursor + 30)
    $commentLength = UInt16 ($cursor + 32)
    $cursor += 46 + $nameLength + $extraLength + $commentLength
}
if ($cursor -ne $centralOffset + $centralSize) { throw 'ZIP central directory length is inconsistent.' }
[IO.File]::WriteAllBytes($Path, $bytes)
Write-Output "PASS: normalized ZIP creator platform metadata: $Path"
