using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using Me1Tlk;

// Me1Tlk: export / import / inspect BioTlkFile strings of ME1 Legendary Edition packages.
// Never writes into the game directory: all output goes to the directory given with --out.

if (args.Length == 0 || args[0] is "-h" or "--help") { Cmd.Usage(); return 1; }
LegendaryExplorerCoreLib.InitLib(null, _ => { }, null, new[] { MEGame.LE1 });
Cmd.LoadOodle();

switch (args[0])
{
    case "export": return Cmd.Export(args);
    case "import": return Cmd.Import(args);
    case "inspect": return Cmd.Inspect(args);
    case "toc": return Cmd.Toc(args);
    case "swf": return Cmd.Swf(args);
    case "scan": return Cmd.Scan(args);
    case "tlks": return Cmd.Tlks(args);
    case "props": return Cmd.Props(args);
    default: Cmd.Usage(); return 1;
}

