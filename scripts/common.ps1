# Shared helpers for build/install/uninstall. Dot-source: . "$PSScriptRoot\common.ps1"
$ErrorActionPreference = 'Stop'
$script:Root = Split-Path -Parent $PSScriptRoot

function Test-GameRunning {
    $p = Get-Process -Name 'MassEffect1', 'MassEffectLauncher' -ErrorAction SilentlyContinue
    return [bool]$p
}

function Get-Sha256([string]$Path) { (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash }

# Maps a staged file to its location inside BioGame. *.pcc -> CookedPCConsole, PCConsoleTOC.bin -> BioGame root.
function Get-TargetRelPath([string]$Name) {
    if ($Name -ieq 'PCConsoleTOC.bin') { return 'PCConsoleTOC.bin' }
    return (Join-Path 'CookedPCConsole' $Name)
}

function Get-StagedFiles([string]$Staging) {
    $cooked = Join-Path $Staging 'CookedPCConsole'
    $files = @()
    if (Test-Path $cooked) { $files += Get-ChildItem -LiteralPath $cooked -Filter *.pcc -File }
    $toc = Join-Path $Staging 'PCConsoleTOC.bin'
    if (Test-Path $toc) { $files += Get-Item -LiteralPath $toc }
    return $files
}
