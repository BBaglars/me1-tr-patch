# Build notes

LegendaryExplorerCore (master) targets net10.0; the machine SDK is 9.0.100, so a local
.NET 10 SDK is installed into `tools/dotnet10` (gitignored, via dotnet-install.ps1 -Channel 10.0).

Setup:
    git clone --depth 1 https://github.com/ME3Tweaks/LegendaryExplorer.git tools/LegendaryExplorer
    powershell dotnet-install.ps1 -Channel 10.0 -InstallDir tools/dotnet10

Build core (use the full path to the local dotnet, PATH override is not picked up):
    tools/dotnet10/dotnet.exe build -c Release tools/LegendaryExplorer/LegendaryExplorer/LegendaryExplorerCore
