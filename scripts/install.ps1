<#
 Installs the staged patch into the game. DRY-RUN by default: prints what would happen. Pass -Apply to really copy.
 Before overwriting a file, the ORIGINAL is copied to <BackupDir>\ (once; an existing backup is never overwritten, so
 repeated installs keep the pristine originals) and recorded with SHA-256 in <BackupDir>\manifest.json.
   .\scripts\install.ps1                 # dry run
   .\scripts\install.ps1 -Apply          # back up + install
#>
param(
    [string]$Staging = (Join-Path (Split-Path -Parent $PSScriptRoot) 'staging'),
    [string]$BackupDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'backup\original'),
    [string]$BioGame = 'C:\Users\Berkay\Documents\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame',
    [switch]$Apply
)
. "$PSScriptRoot\common.ps1"
$files = @(Get-StagedFiles $Staging)
if ($files.Count -eq 0) { throw "No staged files in $Staging (run build-staging.ps1 first)." }
if (-not (Test-Path (Join-Path $Staging 'PCConsoleTOC.bin'))) { throw 'Staging has no PCConsoleTOC.bin: refusing, the TOC sizes would not match.' }
if ($Apply -and (Test-GameRunning)) { throw 'Close Mass Effect / the launcher first.' }

$manifestPath = Join-Path $BackupDir 'manifest.json'
$manifest = @{ created = (Get-Date -Format o); bioGame = $BioGame; files = @{} }
if (Test-Path $manifestPath) {
    $m = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.created = $m.created
    foreach ($p in $m.files.PSObject.Properties) {
        $manifest.files[$p.Name] = @{ originalSha256 = $p.Value.originalSha256; originalSize = $p.Value.originalSize; patchedSha256 = $p.Value.patchedSha256 }
    }
}

# preflight: every target must exist, backup space must suffice
$need = 0
foreach ($f in $files) {
    $rel = Get-TargetRelPath $f.Name
    $dest = Join-Path $BioGame $rel
    if (-not (Test-Path $dest)) { throw "Original missing in game dir: $dest (the patch only replaces existing files)" }
    if (-not $manifest.files.ContainsKey($rel)) { $need += (Get-Item $dest).Length }
}
New-Item -ItemType Directory -Force $BackupDir | Out-Null
$driveLetter = (Resolve-Path $BackupDir).Path.Substring(0, 1)
$free = (Get-PSDrive -Name $driveLetter).Free
if ($need -gt $free) { throw "Not enough free space for backup: need $need, free $free" }

$mode = if ($Apply) { 'APPLY' } else { 'DRY-RUN (no changes; pass -Apply)' }
Write-Host "== install [$mode] $($files.Count) files, backup needs $([math]::Round($need/1MB,1)) MB =="
$i = 0
foreach ($f in $files) {
    $i++
    $rel = Get-TargetRelPath $f.Name
    $dest = Join-Path $BioGame $rel
    $bak = Join-Path $BackupDir $rel
    $hasBackup = $manifest.files.ContainsKey($rel) -and (Test-Path $bak)
    $action = if ($hasBackup) { 'backup exists' } else { 'backup + replace' }
    if (-not $Apply) { Write-Host ("[{0}/{1}] {2}  ({3})" -f $i, $files.Count, $rel, $action); continue }
    if (-not $hasBackup) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $bak) | Out-Null
        $srcHash = Get-Sha256 $dest
        Copy-Item -LiteralPath $dest -Destination $bak
        if ((Get-Sha256 $bak) -ne $srcHash) { throw "Backup verify failed: $rel" }
        $manifest.files[$rel] = @{ originalSha256 = $srcHash; originalSize = (Get-Item $bak).Length; patchedSha256 = $null }
        # persist after every file so an interruption never loses track of a backed-up original
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    }
    Copy-Item -LiteralPath $f.FullName -Destination $dest -Force
    $h = Get-Sha256 $f.FullName
    if ((Get-Sha256 $dest) -ne $h) { throw "Install verify failed: $rel (run uninstall.ps1 -Apply)" }
    $manifest.files[$rel].patchedSha256 = $h
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    if ($i % 25 -eq 0) { Write-Host "  $i/$($files.Count)" }
}
if ($Apply) { Write-Host "Installed. Backup: $BackupDir (restore with scripts\uninstall.ps1 -Apply)" }
