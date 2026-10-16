using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;

// Me1Tlk: export / import / inspect BioTlkFile strings of ME1 Legendary Edition packages.
// Never writes into the game directory: all output goes to the directory given with --out.

static string Arg(string[] a, string name, string def = null)
{
    int i = Array.IndexOf(a, name);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
}

if (args.Length == 0 || args[0] is "-h" or "--help") { Usage(); return 1; }
LegendaryExplorerCoreLib.InitLib(null, _ => { }, null, new[] { MEGame.LE1 });
LoadOodle();

switch (args[0])
{
    case "export": return Export(args);
    case "import": return Import(args);
    case "inspect": return Inspect(args);
    default: Usage(); return 1;
}

static void LoadOodle()
{
    var dll = Environment.GetEnvironmentVariable("ME1_OODLE")
        ?? @"C:\Users\Berkay\Documents\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\Binaries\Win64\oo2core_8_win64.dll";
    LegendaryExplorerCore.Compression.OodleHelper.LoadOodleDll(dll);
}

static void Usage()
{
    Console.WriteLine(@"Me1Tlk <command>
  export  --game <CookedPCConsole> --out <dir> [--pkgs INT|DE] [--text INT|DE] [--only <substr>]
          Reads <pkgs> packages (*_LOC_<pkgs>.pcc, Startup_<pkgs>.pcc), writes one JSON per package into <out>.
          --text selects which language's TLK exports are read (INT = tlk/tlk_M, DE = tlk_DE/tlk_M_DE). Default: text = pkgs.
  import  --src <dir with original packages> --json <dir with translated JSON> --out <staging dir>
          [--pkgs DE|INT] [--target DE[,INT]] [--only <substr>]
          Opens COPIES of <src>/<stem>_<pkgs>.pcc, writes tr into the tlk_<target>/tlk_M_<target> exports (default DE), saves to <out>.
  inspect <package.pcc>   Lists BioTlkFile exports.");
}

static JsonSerializerOptions JOpts() => new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };


// Groups the BioTlkFile exports of a package by set. For each set returns the female/default and male exports for language `lang`.
// A plain export (tlk, tlk_M) is English (INT) when the set also has suffixed exports (tlk_DE ...);
// a set with only plain exports (Startup GlobalTlk) is in the package's own language (pkgLang).
static List<(string set, ExportEntry f, ExportEntry m)> TlkSets(IMEPackage pkg, string lang, string pkgLang)
{
    var TlkName = new System.Text.RegularExpressions.Regex(@"^(?<b>.*?tlk)(?<m>_M)?(?:_(?<l>[A-Z]{2,3}))?$");
    var res = new List<(string, ExportEntry, ExportEntry)>();
    var all = pkg.Exports.Where(e => e.ClassName == "BioTlkFile")
        .Select(e => (e, mt: TlkName.Match(e.ObjectName.Instanced))).Where(x => x.mt.Success).ToList();
    foreach (var g in all.GroupBy(x => SetPath(x.e)))
    {
        bool suffixed = g.Any(x => x.mt.Groups["l"].Success);
        ExportEntry f = null, m = null;
        foreach (var (e, mt) in g)
        {
            string l = mt.Groups["l"].Success ? mt.Groups["l"].Value : (suffixed ? "INT" : pkgLang);
            if (l != lang) continue;
            if (mt.Groups["m"].Success) m ??= e; else f ??= e;
        }
        if (f != null || m != null) res.Add((g.Key, f, m));
    }
    return res;
}

static string SetPath(ExportEntry e) => e.ParentInstancedFullPath;

static IEnumerable<string> PackagesFor(string game, string pkgs)
{
    var files = Directory.GetFiles(game, $"*_LOC_{pkgs}.pcc").Concat(Directory.GetFiles(game, $"Startup_{pkgs}.pcc"));
    return files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
}

static string Stem(string file, string pkgs)
{
    var n = Path.GetFileNameWithoutExtension(file);
    return n.EndsWith("_" + pkgs) ? n[..^(pkgs.Length + 1)] : n;
}

static long Words(string s) => s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

static Dictionary<int, string> Load(ExportEntry e)
{
    var t = new ME1TalkFile(e);
    var d = new Dictionary<int, string>();
    foreach (var s in t.StringRefs)
        if (s.Flags == 1 && s.Data != null) d[s.StringID] = s.Data;
    return d;
}

static int Export(string[] a)
{
    string game = Arg(a, "--game"), outDir = Arg(a, "--out");
    string pkgs = Arg(a, "--pkgs", "INT"), text = Arg(a, "--text", pkgs), only = Arg(a, "--only");
    if (game == null || outDir == null) { Usage(); return 1; }
    Directory.CreateDirectory(outDir);
    long strings = 0, words = 0, chars = 0, maleDiff = 0, pkgCount = 0, tlkSets = 0;
    var perPkg = new List<object>();
    var opts = JOpts();
    foreach (var file in PackagesFor(game, pkgs))
    {
        if (only != null && !Path.GetFileName(file).Contains(only, StringComparison.OrdinalIgnoreCase)) continue;
        var pkg = MEPackageHandler.OpenMEPackage(file, forceLoadFromDisk: true);
        var byParent = TlkSets(pkg, text, pkgs);
        var stem = Stem(file, pkgs);
        var entries = new JsonArray();
        long pStrings = 0, pWords = 0;
        foreach (var g in byParent)
        {
            tlkSets++;
            var (gkey, fe, me) = g;
            var f = fe == null ? new Dictionary<int, string>() : Load(fe);
            var m = me == null ? new Dictionary<int, string>() : Load(me);
            foreach (var id in f.Keys.Union(m.Keys).OrderBy(x => x))
            {
                f.TryGetValue(id, out var ft); m.TryGetValue(id, out var mt);
                string main = ft ?? mt;
                var o = new JsonObject { ["key"] = $"{stem}|{gkey}|{id}", ["en"] = main, ["tr"] = "" };
                entries.Add(o);
                pStrings++; pWords += Words(main); chars += main.Length;
                if (ft != null && mt != null && mt != ft)
                {
                    maleDiff++;
                    entries.Add(new JsonObject { ["key"] = $"{stem}|{gkey}|{id}|m", ["en"] = mt, ["tr"] = "" });
                    pStrings++; pWords += Words(mt); chars += mt.Length;
                }
                else if (ft == null && mt != null) o["only"] = "m";
            }
        }
        strings += pStrings; words += pWords; pkgCount++;
        perPkg.Add(new { package = Path.GetFileName(file), tlkSets = byParent.Count, strings = pStrings, words = pWords });
        var doc = new JsonObject
        {
            ["meta"] = new JsonObject { ["package"] = Path.GetFileName(file), ["stem"] = stem, ["textLang"] = text, ["sets"] = byParent.Count },
            ["entries"] = entries
        };
        File.WriteAllText(Path.Combine(outDir, stem + ".json"), doc.ToJsonString(opts), new UTF8Encoding(false));
        pkg.Dispose();
        Console.WriteLine($"{Path.GetFileName(file)}: sets={byParent.Count} strings={pStrings} words={pWords}");
    }
    var summary = new { packages = pkgCount, tlkSets, strings, words, chars, maleDiffEntries = maleDiff, perPackage = perPkg };
    File.WriteAllText(Path.Combine(outDir, "_summary.json"), JsonSerializer.Serialize(summary, opts), new UTF8Encoding(false));
    Console.WriteLine($"TOTAL packages={pkgCount} tlkSets={tlkSets} strings={strings} words={words} chars={chars} maleDiff={maleDiff}");
    return 0;
}

static int Import(string[] a)
{
    string src = Arg(a, "--src"), json = Arg(a, "--json"), outDir = Arg(a, "--out");
    string pkgs = Arg(a, "--pkgs", "DE"), target = Arg(a, "--target", "DE"), only = Arg(a, "--only");
    if (src == null || json == null || outDir == null) { Usage(); return 1; }
    string fs = Path.GetFullPath(src).TrimEnd('\\'), fo = Path.GetFullPath(outDir).TrimEnd('\\');
    if (fo.Equals(fs, StringComparison.OrdinalIgnoreCase) || fo.StartsWith(fs + "\\", StringComparison.OrdinalIgnoreCase))
    { Console.Error.WriteLine("Refusing: --out must not be inside or equal to --src."); return 2; }
    Directory.CreateDirectory(outDir);
    int done = 0; long replaced = 0, added = 0, skippedEmpty = 0, missing = 0;
    foreach (var jf in Directory.GetFiles(json, "*.json").OrderBy(x => x))
    {
        if (Path.GetFileName(jf).StartsWith("_")) continue;
        if (only != null && !Path.GetFileName(jf).Contains(only, StringComparison.OrdinalIgnoreCase)) continue;
        var root = JsonNode.Parse(File.ReadAllText(jf, Encoding.UTF8));
        var entries = (root is JsonObject ro ? ro["entries"] : root) as JsonArray;
        string stem = Path.GetFileNameWithoutExtension(jf);
        string srcFile = Path.Combine(src, $"{stem}_{pkgs}.pcc");
        if (!File.Exists(srcFile)) { Console.Error.WriteLine($"skip {stem}: {srcFile} not found"); continue; }
        var plan = new Dictionary<string, Dictionary<int, (string f, string m)>>();
        foreach (var en in entries!)
        {
            var parts = en["key"]!.GetValue<string>().Split('|');
            string tr = en["tr"]?.GetValue<string>() ?? "";
            if (tr.Length == 0) { skippedEmpty++; continue; }
            bool male = parts.Length > 3 && parts[3] == "m";
            string setp = parts[1]; int id = int.Parse(parts[2]);
            if (!plan.TryGetValue(setp, out var d)) plan[setp] = d = new();
            d.TryGetValue(id, out var cur);
            d[id] = male ? (cur.f, tr) : (tr, cur.m);
        }
        var pkg = MEPackageHandler.OpenMEPackage(srcFile, forceLoadFromDisk: true);
        bool wasCompressed = pkg.IsCompressed;
        foreach (var (setp, ids) in plan)
        {
            bool foundSet = false;
            foreach (var lang in target.Split(','))
            {
            var tl = TlkSets(pkg, lang, pkgs).Where(x => x.set == setp).ToList();
            if (tl.Count == 0) continue;
            foundSet = true;
            var (_, fe, me) = tl[0];
            foreach (var (ex, isMale) in new[] { (fe, false), (me, true) })
            {
                if (ex == null) continue;
                var t = new ME1TalkFile(ex);
                bool any = false;
                foreach (var (id, v) in ids)
                {
                    string text = isMale ? (v.m ?? v.f) : (v.f ?? v.m);
                    if (text == null) continue;
                    if (t.ReplaceString(id, text, true)) replaced++; else added++; any = true;
                }
                if (!any) continue;
                var h = new HuffmanCompression();
                h.LoadInputData(t.StringRefs);
                h.SerializeTalkfileToExport(ex);
            }
            }
            if (!foundSet) { missing += ids.Count; Console.Error.WriteLine($"  {stem}: set {setp} has no TLK for {target}"); }
        }
        pkg.Save(Path.Combine(outDir, Path.GetFileName(srcFile)), compress: wasCompressed);
        pkg.Dispose();
        done++;
        Console.WriteLine($"{Path.GetFileName(srcFile)} written (compressed={wasCompressed})");
    }
    Console.WriteLine($"DONE packages={done} replaced={replaced} addedMissingIds={added} notFound={missing} emptyTrSkipped={skippedEmpty}");
    return 0;
}

static int Inspect(string[] a)
{
    var p = MEPackageHandler.OpenMEPackage(a[1], forceLoadFromDisk: true);
    Console.WriteLine($"game={p.Game} loc={p.Localization} exports={p.ExportCount} compressed={p.IsCompressed}");
    foreach (var e in p.Exports.Where(x => x.ClassName == "BioTlkFile"))
    {
        var t = new ME1TalkFile(e);
        var f = t.StringRefs.FirstOrDefault(s => s.Flags == 1 && s.Data is { Length: > 8 });
        Console.WriteLine($"{e.UIndex} {e.InstancedFullPath} refs={t.StringRefs.Count} first={f?.StringID} [{f?.Data}]");
    }
    return 0;
}
