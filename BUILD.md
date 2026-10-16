# Build notes

LegendaryExplorerCore (master) targets net10.0; the machine SDK is 9.0.100, so a local
.NET 10 SDK is installed into `tools/dotnet10` (gitignored, via dotnet-install.ps1 -Channel 10.0).

Setup:
    git clone --depth 1 https://github.com/ME3Tweaks/LegendaryExplorer.git tools/LegendaryExplorer
    powershell dotnet-install.ps1 -Channel 10.0 -InstallDir tools/dotnet10

Build core (use the full path to the local dotnet, PATH override is not picked up):
    tools/dotnet10/dotnet.exe build -c Release tools/LegendaryExplorer/LegendaryExplorer/LegendaryExplorerCore

## Me1Tlk tool (src/Me1Tlk)

    tools/dotnet10/dotnet.exe build -c Release src/Me1Tlk
    T=src/Me1Tlk/bin/Release/net10.0/Me1Tlk.dll
    tools/dotnet10/dotnet.exe $T export --game "<CookedPCConsole>" --out data/en            # EN (INT) -> JSON, read-only on game dir
    tools/dotnet10/dotnet.exe $T export --game "<CookedPCConsole>" --pkgs DE --out data/de  # German text of DE packages
    tools/dotnet10/dotnet.exe $T import --src "<CookedPCConsole>" --json translated --out staging [--pkgs DE|INT] [--target DE[,INT]]
    tools/dotnet10/dotnet.exe $T inspect <package.pcc>

Oodle: loaded from the game's Binaries\Win64\oo2core_8_win64.dll (override with env ME1_OODLE). Import refuses an --out inside --src.
JSON: {meta, entries:[{key,en,tr}]}, key = `<pkg stem w/o _INT>|<tlk set path>|<stringId>[|m]`; `|m` entries only where tlk_M differs from tlk. Empty tr = keep original.
