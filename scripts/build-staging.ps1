<#
 Builds the patched packages into a staging dir (never touches the game dir) and rewrites PCConsoleTOC.bin sizes.
   staging\CookedPCConsole\*.pcc   patched copies
   staging\PCConsoleTOC.bin        original TOC with updated file sizes
 Example: .\scripts\build-staging.ps1 -Json translated -Staging staging [-Pkgs INT] [-Target INT]
 Default (decision after the in-game test): the INT slot, -Pkgs INT -Target INT (plain tlk/tlk_M, Startup_INT GlobalTlk). The DE slot (-Pkgs DE) switches voice-over to German.
#>
param(
    [Parameter(Mandatory)][string]$Json,
    [string]$Staging = (Join-Path (Split-Path -Parent $PSScriptRoot) 'staging'),
    [string]$BioGame = 'C:\Users\Berkay\Documents\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame',
    [string]$Pkgs = 'INT',
    [string]$Target = '',
    [string]$Only
)
. "$PSScriptRoot\common.ps1"
$dotnet = Join-Path $Root 'tools\dotnet10\dotnet.exe'
$dll = Join-Path $Root 'src\Me1Tlk\bin\Release\net10.0\Me1Tlk.dll'
if (-not (Test-Path $dll)) { throw "Build the tool first: $dotnet build -c Release src\Me1Tlk" }
$cooked = Join-Path $Staging 'CookedPCConsole'
New-Item -ItemType Directory -Force $cooked | Out-Null
$a = @($dll, 'import', '--src', (Join-Path $BioGame 'CookedPCConsole'), '--json', $Json, '--out', $cooked, '--pkgs', $Pkgs, '--target', $(if ($Target) { $Target } else { $Pkgs }))
if ($Only) { $a += @('--only', $Only) }
& $dotnet @a
if ($LASTEXITCODE -ne 0) { throw 'import failed' }
# base TOC: the pristine backup if an install happened before, else the game's TOC
$toc = Join-Path $Root 'backup\original\PCConsoleTOC.bin'
if (-not (Test-Path $toc)) { $toc = Join-Path $BioGame 'PCConsoleTOC.bin' }
& $dotnet $dll toc --toc $toc --staged $cooked --out (Join-Path $Staging 'PCConsoleTOC.bin')
if ($LASTEXITCODE -ne 0) { throw 'toc update failed' }
Write-Host "Staging ready: $Staging"
