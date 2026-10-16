<#
 Restores the original files recorded in <BackupDir>\manifest.json. DRY-RUN by default; pass -Apply to restore.
 Each restored file is verified against the SHA-256 recorded at backup time.
 Fallback if the backup is lost: Steam > Mass Effect Legendary Edition > Properties > Installed Files > Verify integrity.
#>
param(
    [string]$BackupDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'backup\original'),
    [string]$BioGame = 'C:\Users\Berkay\Documents\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame',
    [switch]$Apply
)
. "$PSScriptRoot\common.ps1"
$manifestPath = Join-Path $BackupDir 'manifest.json'
if (-not (Test-Path $manifestPath)) { throw "No backup manifest at $manifestPath" }
if ($Apply -and (Test-GameRunning)) { throw 'Close Mass Effect / the launcher first.' }
$m = Get-Content $manifestPath -Raw | ConvertFrom-Json
$entries = @($m.files.PSObject.Properties)
# verify every backup before touching anything
foreach ($e in $entries) {
    $bak = Join-Path $BackupDir $e.Name
    if (-not (Test-Path $bak)) { throw "Backup file missing: $bak" }
    if ((Get-Sha256 $bak) -ne $e.Value.originalSha256) { throw "Backup corrupt: $($e.Name)" }
}
$mode = if ($Apply) { 'APPLY' } else { 'DRY-RUN (no changes; pass -Apply)' }
Write-Host "== uninstall [$mode] $($entries.Count) files =="
$i = 0
foreach ($e in $entries) {
    $i++
    $dest = Join-Path $BioGame $e.Name
    $cur = if (Test-Path $dest) { Get-Sha256 $dest } else { '' }
    $state = if ($cur -eq $e.Value.originalSha256) { 'already original' } else { 'restore' }
    if (-not $Apply) { Write-Host ("[{0}/{1}] {2} ({3})" -f $i, $entries.Count, $e.Name, $state); continue }
    if ($state -eq 'restore') {
        Copy-Item -LiteralPath (Join-Path $BackupDir $e.Name) -Destination $dest -Force
        if ((Get-Sha256 $dest) -ne $e.Value.originalSha256) { throw "Restore verify failed: $($e.Name)" }
    }
}
if ($Apply) { Write-Host 'Restored. Backups kept in place.' }
