# Filter a dumped Android logcat file down to Unity / app-process lines.
# Kept ASCII-only on purpose: Windows PowerShell 5.1 parses .ps1 as ANSI unless a BOM is present.
#
# Three modes (positive matching; everything outside the match set is dropped):
#   -Mode unity   (default) tag-based: Unity, UnityPlayer, CRASH, DEBUG, AndroidRuntime,
#                 mono, il2cpp, MonoDroid, plus the app package used as a tag.
#                 => the short list a Unity dev actually reads.
#   -Mode pid     everything the app process emitted (any tag). Use when hunting native
#                 renderer / codec / audio problems (SnapAlloc, AdrenoVK, AudioTrack...).
#   -Mode piderr  app process but only V/W/E/F, plus every Unity-tag line. Good default
#                 when the app "runs but misbehaves".
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File filter-unity-log.ps1 -Path Logs\10191847.log
#   ... -Path Logs\10191847.log -Mode piderr -Summary
#   ... -Path Logs\10191847.log -Mode pid -AppPid 30712 -Out Logs\10191847.pid.log
param(
  [Parameter(Mandatory = $true)][string]$Path,
  [ValidateSet('unity', 'pid', 'piderr')][string]$Mode = 'unity',
  [int]$AppPid = 0,
  [string]$Package = 'com.i000stea.thrudremark',
  [string[]]$Tags = @('Unity', 'UnityPlayer', 'CRASH', 'DEBUG', 'AndroidRuntime', 'mono', 'il2cpp', 'MonoDroid'),
  [switch]$Summary,
  [string]$Out = ''
)

if (-not (Test-Path -LiteralPath $Path)) { throw "Log not found: $Path" }

# threadtime / vendor variant seen on ColorOS-ish ROMs:
#   2026-10-19 18:45:13.341  2007-2027  statsd  statsd  E  message
#   2026-10-19 18:45:18.323 30712-4492  Unity   usap64  E  [HotUpdate] ...
$lineRx = '^(?<date>\d{4}-\d{2}-\d{2})\s+(?<time>\d{2}:\d{2}:\d{2}\.\d{3})\s+(?<pid>\d+)-(?<tid>\d+)\s+(?<tag>\S+)\s+(?<proc>\S+)\s+(?<level>[VDIWEFA])\s+(?<msg>.*)$'

$lines = Get-Content -LiteralPath $Path -Encoding UTF8
Write-Host "[filter] source lines: $($lines.Count)  mode: $Mode"

$parsed = New-Object System.Collections.Generic.List[object]
foreach ($line in $lines) {
  $m = [regex]::Match($line, $lineRx)
  if (-not $m.Success) {
    $parsed.Add([pscustomobject]@{ Raw = $line; Parsed = $false; Pid = 0; Tag = ''; Proc = ''; Level = ''; Message = '' })
    continue
  }
  $parsed.Add([pscustomobject]@{
    Raw     = $line
    Parsed  = $true
    Pid     = [int]$m.Groups['pid'].Value
    Tag     = $m.Groups['tag'].Value
    Proc    = $m.Groups['proc'].Value
    Level   = $m.Groups['level'].Value
    Message = $m.Groups['msg'].Value
  })
}

# --- auto-detect the app pid from the first Unity-tagged line ---------------
if ($AppPid -le 0) {
  $unityLine = $parsed | Where-Object { $_.Parsed -and $_.Tag -eq 'Unity' } | Select-Object -First 1
  if ($unityLine) {
    $AppPid = $unityLine.Pid
    Write-Host "[filter] detected app pid = $AppPid (from a Unity-tagged line)"
  }
}

$tagSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($t in $Tags) { [void]$tagSet.Add($t) }
if ($Package) { [void]$tagSet.Add($Package) }

$kept = New-Object System.Collections.Generic.List[object]
foreach ($p in $parsed) {
  if (-not $p.Parsed) { continue }
  $isAppPid = ($AppPid -gt 0 -and $p.Pid -eq $AppPid)
  $isUnityTag = $tagSet.Contains($p.Tag)

  $keep = $false
  switch ($Mode) {
    'unity'  { $keep = $isUnityTag }
    'pid'    { $keep = $isAppPid -or $isUnityTag }
    'piderr' { $keep = $isUnityTag -or ($isAppPid -and $p.Level -in @('V', 'W', 'E', 'F')) }
  }
  if ($keep) { $kept.Add($p) }
}

Write-Host "[filter] kept: $($kept.Count)  dropped: $($lines.Count - $kept.Count)"

if ($Summary) {
  Write-Host ""
  Write-Host "=== kept by tag ==="
  $kept | Group-Object Tag | Sort-Object Count -Descending |
    ForEach-Object { Write-Host ("  {0,-24} {1}" -f $_.Name, $_.Count) }
  Write-Host ""
  Write-Host "=== kept ERROR/WARN lines ==="
  $errLines = $kept | Where-Object { $_.Level -in @('E', 'F', 'W') }
  Write-Host "  count: $($errLines.Count)"
  $errLines | Select-Object -First 25 |
    ForEach-Object { Write-Host ("  [{0}] {1}: {2}" -f $_.Level, $_.Tag, $_.Message) }
  Write-Host ""
  Write-Host "=== dropped noise, top 10 tags ==="
  $keptRaw = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($k in $kept) { [void]$keptRaw.Add($k.Raw) }
  $parsed | Where-Object { $_.Parsed -and -not $keptRaw.Contains($_.Raw) } |
    Group-Object Tag | Sort-Object Count -Descending | Select-Object -First 10 |
    ForEach-Object { Write-Host ("  {0,-24} {1}" -f $_.Name, $_.Count) }
  return
}

if ($Out) {
  Set-Content -LiteralPath $Out -Value ($kept | ForEach-Object { $_.Raw }) -Encoding UTF8
  Write-Host "[filter] written: $Out  ($($kept.Count) lines)"
  return
}

$kept | ForEach-Object { $_.Raw }
