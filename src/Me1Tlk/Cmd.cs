using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;

namespace Me1Tlk;

internal static class Cmd
{
    internal static string Arg(string[] a, string name, string def = null)
    {
        int i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
    }

    internal static void LoadOodle()
    {
        var dll = Environment.GetEnvironmentVariable("ME1_OODLE")
            ?? @"C:\Users\Berkay\Documents\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\Binaries\Win64\oo2core_8_win64.dll";
        LegendaryExplorerCore.Compression.OodleHelper.LoadOodleDll(dll);
    }

    internal static void Usage()
    {
        Console.WriteLine(@"Me1Tlk <command>
      export  --game <CookedPCConsole> --out <dir> [--pkgs INT|DE] [--text INT|DE] [--only <substr>]
              Reads <pkgs> packages (*_LOC_<pkgs>.pcc, Startup_<pkgs>.pcc), writes one JSON per package into <out>.
              --text selects which language's TLK exports are read (INT = tlk/tlk_M, DE = tlk_DE/tlk_M_DE). Default: text = pkgs.
      import  --src <dir with original packages> --json <dir with translated JSON> --out <staging dir>
              [--pkgs INT|DE] [--extra <list of non-LOC package stems>] [--target <default: same as --pkgs>[,..]] [--only <substr>]
              Opens COPIES of <src>/<stem>_<pkgs>.pcc, writes tr into the tlk_<target>/tlk_M_<target> exports (default: same language as --pkgs, INT = plain tlk/tlk_M), saves to <out>.
      inspect <package.pcc>   Lists BioTlkFile exports.
      toc     --toc <original PCConsoleTOC.bin> --staged <dir with patched packages> --out <new PCConsoleTOC.bin>
              Copies the TOC, setting the size of every entry whose file name exists in <staged> to that file's length.
              With no --staged, only checks that the TOC re-serializes byte-identical.");
    }

    internal static JsonSerializerOptions JOpts() => new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };


    // Groups the BioTlkFile exports of a package by set. For each set returns the female/default and male exports for language `lang`.
    // A plain export (tlk, tlk_M) is English (INT) when the set also has suffixed exports (tlk_DE ...);
    // a set with only plain exports (Startup GlobalTlk) is in the package's own language (pkgLang).
    internal static List<(string set, ExportEntry f, ExportEntry m)> TlkSets(IMEPackage pkg, string lang, string pkgLang)
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

    internal static string SetPath(ExportEntry e) => e.ParentInstancedFullPath;

    // extra: optional text file listing non-LOC package stems (cinematic / DSG packages that carry their own TLK sets); only used for INT.
    internal static IEnumerable<string> PackagesFor(string game, string pkgs, string extra = null)
    {
        var files = Directory.GetFiles(game, $"*_LOC_{pkgs}.pcc").Concat(Directory.GetFiles(game, $"Startup_{pkgs}.pcc"));
        if (extra != null && pkgs == "INT")
            files = files.Concat(File.ReadAllLines(extra).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => Path.Combine(game, l + ".pcc")).Where(File.Exists));
        return files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    internal static string Stem(string file, string pkgs)
    {
        var n = Path.GetFileNameWithoutExtension(file);
        return n.EndsWith("_" + pkgs) ? n[..^(pkgs.Length + 1)] : n;
    }

    internal static long Words(string s) => s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

    internal static Dictionary<int, string> Load(ExportEntry e)
    {
        var t = new ME1TalkFile(e);
        var d = new Dictionary<int, string>();
        foreach (var s in t.StringRefs)
            if (s.Flags == 1 && s.Data != null) d[s.StringID] = s.Data;
        return d;
    }

    internal static int Export(string[] a)
    {
        string game = Arg(a, "--game"), outDir = Arg(a, "--out");
        string pkgs = Arg(a, "--pkgs", "INT"), text = Arg(a, "--text", pkgs), only = Arg(a, "--only");
        if (game == null || outDir == null) { Usage(); return 1; }
        Directory.CreateDirectory(outDir);
        long strings = 0, words = 0, chars = 0, maleDiff = 0, pkgCount = 0, tlkSets = 0;
        var perPkg = new List<object>();
        var opts = JOpts();
        foreach (var file in PackagesFor(game, pkgs, Arg(a, "--extra")))
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

    internal static int Import(string[] a)
    {
        string src = Arg(a, "--src"), json = Arg(a, "--json"), outDir = Arg(a, "--out");
        string pkgs = Arg(a, "--pkgs", "INT"), target = Arg(a, "--target", pkgs), only = Arg(a, "--only");
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
            if (!File.Exists(srcFile) && pkgs == "INT" && File.Exists(Path.Combine(src, stem + ".pcc"))) srcFile = Path.Combine(src, stem + ".pcc");
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

    internal static int Inspect(string[] a)
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

    internal static int Toc(string[] a)
    {
        string toc = Arg(a, "--toc"), staged = Arg(a, "--staged"), outp = Arg(a, "--out");
        if (toc == null) { Usage(); return 1; }
        var orig = File.ReadAllBytes(toc);
        var tbf = new LegendaryExplorerCore.Unreal.TOCBinFile(toc);
        if (staged == null)
        {
            var re = tbf.Save().ToArray();
            Console.WriteLine($"entries={tbf.GetAllEntries().Count} identicalAfterResave={re.AsSpan().SequenceEqual(orig)} (orig {orig.Length}, resaved {re.Length})");
            return 0;
        }
        if (outp == null) { Usage(); return 1; }
        var files = Directory.GetFiles(staged).ToDictionary(f => Path.GetFileName(f), f => new FileInfo(f).Length, StringComparer.OrdinalIgnoreCase);
        int changed = 0, same = 0, notInToc = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in tbf.GetAllEntries())
        {
            var n = Path.GetFileName(e.name.Replace((char)92, '/'));
            if (!files.TryGetValue(n, out var len)) continue;
            seen.Add(n);
            if (e.size == (int)len) same++; else { Console.WriteLine($"  {e.name}: {e.size} -> {len}"); e.size = (int)len; changed++; }
        }
        foreach (var f in files.Keys) if (!seen.Contains(f)) { notInToc++; Console.Error.WriteLine($"  warning: {f} has no TOC entry"); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outp))!);
        File.WriteAllBytes(outp, tbf.Save().ToArray());
        Console.WriteLine($"TOC written: sizeChanged={changed} sizeUnchanged={same} stagedFilesWithoutEntry={notInToc}");
        return notInToc > 0 ? 3 : 0;
    }

    // swf <package.pcc> <outDir>: lists class counts and dumps RawData of SwfMovie/GFxMovieInfo exports (read-only).
    internal static int Swf(string[] a)
    {
        var p = MEPackageHandler.OpenMEPackage(a[1], forceLoadFromDisk: true);
        Directory.CreateDirectory(a[2]);
        foreach (var g in p.Exports.GroupBy(e => e.ClassName).OrderByDescending(g => g.Count()).Take(12)) Console.WriteLine($"class {g.Key}: {g.Count()}");
        int n = 0;
        foreach (var e in p.Exports.Where(x => x.ClassName is "GFxMovieInfo" or "SwfMovie"))
        {
            var raw = e.GetProperty<LegendaryExplorerCore.Unreal.ImmutableByteArrayProperty>("RawData");
            if (raw == null) continue;
            var bytes = raw.Bytes;
            var file = Path.Combine(a[2], e.InstancedFullPath.Replace('.', '_') + ".swf");
            File.WriteAllBytes(file, bytes);
            Console.WriteLine($"  {e.InstancedFullPath}: {bytes.Length} bytes -> {file}");
            n++;
        }
        return 0;
    }

    // scan --game <dir> --find <text> [--out <file>]: searches the decompressed export data of every package (UTF-16LE and ASCII). Read-only.
    internal static int Scan(string[] a)
    {
        string game = Arg(a, "--game"), find = Arg(a, "--find");
        if (game == null || find == null) { Usage(); return 1; }
        var u16 = Encoding.Unicode.GetBytes(find); var a8 = Encoding.ASCII.GetBytes(find);
        var files = Directory.GetFiles(game, "*.pcc", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
        int hits = 0;
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f =>
        {
            try
            {
                using var p = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                foreach (var e in p.Exports)
                {
                    var d = e.DataReadOnly.ToArray();
                    bool w = d.AsSpan().IndexOf(u16) >= 0, n = d.AsSpan().IndexOf(a8) >= 0;
                    if (w || n) { Interlocked.Increment(ref hits); Console.WriteLine($"HIT {Path.GetFileName(f)} #{e.UIndex} {e.ClassName} {e.InstancedFullPath} utf16={w} ascii={n}"); }
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"ERR {Path.GetFileName(f)}: {ex.Message}"); }
        });
        Console.WriteLine($"scan done files={files.Length} hits={hits}");
        return 0;
    }

    // tlks --game <dir> --find <text>: lists every BioTlkFile export in every package under <dir> (recursive) and searches decoded strings. Read-only.
    internal static int Tlks(string[] a)
    {
        string game = Arg(a, "--game"), find = Arg(a, "--find");
        var files = Directory.GetFiles(game, "*.pcc", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
        var lines = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f =>
        {
            try
            {
                using var p = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                foreach (var e in p.Exports.Where(x => x.ClassName == "BioTlkFile"))
                {
                    int n = 0, hit = 0;
                    try { var t = new ME1TalkFile(e); n = t.StringRefs.Count(s => s.Flags == 1 && s.Data != null); if (find != null) hit = t.StringRefs.Count(s => s.Data != null && s.Data.Contains(find, StringComparison.OrdinalIgnoreCase)); } catch { n = -1; }
                    lines.Add($"TLK	{Path.GetFileName(f)}	{e.InstancedFullPath}	{n}	{hit}");
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"ERR {Path.GetFileName(f)}: {ex.Message}"); }
        });
        foreach (var l in lines.OrderBy(x => x)) Console.WriteLine(l);
        return 0;
    }

    // props --game <dir>: counts StrProperty values (recursing structs/arrays) per class|property over every package under <dir>. Read-only.
    internal static int Props(string[] a)
    {
        string game = Arg(a, "--game");
        var files = Directory.GetFiles(game, "*.pcc", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
        var stats = new System.Collections.Concurrent.ConcurrentDictionary<string, (int n, int pk, string sample, string pkg)>();
        void Walk(LegendaryExplorerCore.Unreal.Property pr, string cls, string pkg, string path)
        {
            switch (pr)
            {
                case LegendaryExplorerCore.Unreal.StrProperty sp:
                    if (!string.IsNullOrWhiteSpace(sp.Value)) stats.AddOrUpdate($"{cls}|{path}{pr.Name}", (1, 1, sp.Value, pkg), (k, v) => (v.n + 1, v.pk, v.sample, v.pkg));
                    break;
                case LegendaryExplorerCore.Unreal.StructProperty st: foreach (var c in st.Properties) Walk(c, cls, pkg, path + pr.Name + "."); break;
                case LegendaryExplorerCore.Unreal.ArrayProperty<LegendaryExplorerCore.Unreal.StructProperty> ar: foreach (var x in ar) Walk(x, cls, pkg, path + pr.Name + "[]."); break;
                case LegendaryExplorerCore.Unreal.ArrayProperty<LegendaryExplorerCore.Unreal.StrProperty> ar2: foreach (var x in ar2) if (!string.IsNullOrWhiteSpace(x.Value)) stats.AddOrUpdate($"{cls}|{path}{pr.Name}[]", (1, 1, x.Value, pkg), (k, v) => (v.n + 1, v.pk, v.sample, v.pkg)); break;
            }
        }
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f =>
        {
            try
            {
                using var p = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                string pkg = Path.GetFileName(f);
                foreach (var e in p.Exports)
                {
                    if (e.ClassName is "BioTlkFile" or "GFxMovieInfo" or "SwfMovie" or "WwiseBank" or "SoundNodeWave" or "Texture2D" or "Bio2DA" or "StaticMesh" or "SkeletalMesh") continue;
                    try { foreach (var pr in e.GetProperties()) Walk(pr, e.ClassName, pkg, ""); } catch { }
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"ERR {Path.GetFileName(f)}: {ex.Message}"); }
        });
        foreach (var kv in stats.OrderByDescending(k => k.Value.n)) { var sm = new string(kv.Value.sample.Select(c => c < 32 ? ' ' : c).ToArray()); Console.WriteLine($"{kv.Value.n}	{kv.Key}	{kv.Value.pkg}	{sm[..Math.Min(80, sm.Length)]}"); }
        return 0;
    }

}
