<#
 Builds the patched packages into a staging dir (never touches the game dir) and rewrites PCConsoleTOC.bin sizes.
   staging\CookedPCConsole\*.pcc   patched copies
   staging\PCConsoleTOC.bin        original TOC with updated file sizes
 Example: .\scripts\build-staging.ps1 -Json translated -Staging staging [-Pkgs DE] [-Target DE]
 Use -Pkgs INT -Target DE to patch the tlk_DE exports inside the *_LOC_INT.pcc files; -Target DE,INT patches both text exports.
#>
param(
    [Parameter(Mandatory)][string]$Json,
    [string]$Staging = (Join-Path (Split-Path -Parent $PSScriptRoot) 'staging'),
    [string]$BioGame = 'C:\Users\Berkay\Documents\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame',
    [string]$Pkgs = 'DE',
    [string]$Target = 'DE',
    [string]$Only
)
. "$PSScriptRoot\common.ps1"
$dotnet = Join-Path $Root 'tools\dotnet10\dotnet.exe'
$dll = Join-Path $Root 'src\Me1Tlk\bin\Release\net10.0\Me1Tlk.dll'
if (-not (Test-Path $dll)) { throw "Build the tool first: $dotnet build -c Release src\Me1Tlk" }
$cooked = Join-Path $Staging 'CookedPCConsole'
New-Item -ItemType Directory -Force $cooked | Out-Null
$a = @($dll, 'import', '--src', (Join-Path $BioGame 'CookedPCConsole'), '--json', $Json, '--out', $cooked, '--pkgs', $Pkgs, '--target', $Target)
if ($Only) { $a += @('--only', $Only) }
& $dotnet @a
if ($LASTEXITCODE -ne 0) { throw 'import failed' }
& $dotnet $dll toc --toc (Join-Path $BioGame 'PCConsoleTOC.bin') --staged $cooked --out (Join-Path $Staging 'PCConsoleTOC.bin')
if ($LASTEXITCODE -ne 0) { throw 'toc update failed' }
Write-Host "Staging ready: $Staging"
