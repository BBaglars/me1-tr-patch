using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Packages;
using Me1Tlk;
using Microsoft.Win32;

namespace Me1Patcher;

// Installs / removes the Turkish text patch (INT slot). Originals are backed up once with a SHA-256 manifest
// (same format as scripts\install.ps1) and can be restored.
internal sealed class Installer
{
    public static readonly string DefaultBackup = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ME1-TR-Yama", "backup", "original");

    readonly string backupDir;
    readonly Action<string> log;
    public Installer(string backupDir, Action<string> log) { this.backupDir = backupDir; this.log = log; }

    // Accepts the Legendary Edition root, Game\ME1 or BioGame; returns the BioGame dir or null.
    public static string ResolveBioGame(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return null;
        p = p.Trim().Trim('"');
        foreach (var c in new[] { p, Path.Combine(p, @"Game\ME1\BioGame"), Path.Combine(p, @"ME1\BioGame"), Path.Combine(p, "BioGame") })
            if (Directory.Exists(Path.Combine(c, "CookedPCConsole")) && File.Exists(Path.Combine(c, "PCConsoleTOC.bin"))
                && File.Exists(Path.Combine(c, "CookedPCConsole", "Startup_INT.pcc")))
                return Path.GetFullPath(c);
        return null;
    }

    public static string FindGame()
    {
        var libs = new List<string>();
        foreach (var root in new[] { Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
                                     Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string })
        {
            if (root == null) continue;
            libs.Add(root);
            var vdf = Path.Combine(root, @"steamapps\libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }
        var cands = libs.Distinct(StringComparer.OrdinalIgnoreCase).Select(l => Path.Combine(l, @"steamapps\common\Mass Effect Legendary Edition")).ToList();
        foreach (var pf in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
        {
            if (pf == null) continue;
            cands.Add(Path.Combine(pf, @"EA Games\Mass Effect Legendary Edition"));
            cands.Add(Path.Combine(pf, @"Origin Games\Mass Effect Legendary Edition"));
            cands.Add(Path.Combine(pf, @"Epic Games\MassEffectLegendaryEdition"));
        }
        foreach (var c in cands) { var b = ResolveBioGame(c); if (b != null) return b; }
        return null;
    }

    public static bool GameRunning() =>
        Process.GetProcessesByName("MassEffect1").Length + Process.GetProcessesByName("MassEffectLauncher").Length > 0;

    static string Sha(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }

    JsonObject LoadManifest(string bio)
    {
        var path = Path.Combine(backupDir, "manifest.json");
        if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path)).AsObject();
        return new JsonObject { ["created"] = DateTime.Now.ToString("o"), ["bioGame"] = bio, ["files"] = new JsonObject() };
    }

    void SaveManifest(JsonObject m) =>
        File.WriteAllText(Path.Combine(backupDir, "manifest.json"), m.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true));

    public void Install(string bio, Action<int, int> progress)
    {
        if (GameRunning()) throw new Exception("Önce Mass Effect / başlatıcıyı kapatın.");
        string cooked = Path.Combine(bio, "CookedPCConsole");
        string tmp = Path.Combine(Path.GetTempPath(), "ME1-TR-" + Guid.NewGuid().ToString("N")[..8]);
        string json = Path.Combine(tmp, "tr"), stage = Path.Combine(tmp, "staging", "CookedPCConsole");
        try
        {
            Directory.CreateDirectory(json); Directory.CreateDirectory(stage);
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("tr.zip") ?? throw new Exception("Gömülü çeviri bulunamadı."))
            using (var z = new ZipArchive(s)) z.ExtractToDirectory(json);
            string oodle = Path.GetFullPath(Path.Combine(bio, @"..\Binaries\Win64\oo2core_8_win64.dll"));
            if (!File.Exists(oodle)) throw new Exception("oo2core_8_win64.dll bulunamadı: " + oodle);
            Environment.SetEnvironmentVariable("ME1_OODLE", oodle);
            LegendaryExplorerCoreLib.InitLib(null, _ => { }, null, new[] { MEGame.LE1 });
            Cmd.LoadOodle();

            log("Yamalı paketler hazırlanıyor (birkaç dakika sürebilir)...");
            if (Cmd.Import(new[] { "import", "--src", cooked, "--json", json, "--out", stage, "--pkgs", "INT", "--target", "INT" }) != 0)
                throw new Exception("Paketler hazırlanamadı.");
            string tocBase = Path.Combine(backupDir, "PCConsoleTOC.bin");
            if (!File.Exists(tocBase)) tocBase = Path.Combine(bio, "PCConsoleTOC.bin");
            string stageToc = Path.Combine(tmp, "staging", "PCConsoleTOC.bin");
            if (Cmd.Toc(new[] { "toc", "--toc", tocBase, "--staged", stage, "--out", stageToc }) != 0) throw new Exception("TOC güncellenemedi.");

            var files = Directory.GetFiles(stage, "*.pcc").Select(f => (src: f, rel: Path.Combine("CookedPCConsole", Path.GetFileName(f)))).ToList();
            files.Add((stageToc, "PCConsoleTOC.bin"));
            var manifest = LoadManifest(bio); var mf = manifest["files"].AsObject();
            long need = 0;
            foreach (var (src, rel) in files)
            {
                var dest = Path.Combine(bio, rel);
                if (!File.Exists(dest)) throw new Exception("Oyunda dosya yok: " + dest);
                if (!mf.ContainsKey(rel)) need += new FileInfo(dest).Length;
            }
            Directory.CreateDirectory(backupDir);
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(backupDir)));
            if (need > drive.AvailableFreeSpace) throw new Exception($"Yedek için yer yok: {need / 1048576} MB gerekli.");
            log($"{files.Count} dosya kurulacak, yedek için {need / 1048576} MB.");
            int i = 0;
            foreach (var (src, rel) in files)
            {
                i++;
                var dest = Path.Combine(bio, rel); var bak = Path.Combine(backupDir, rel);
                if (!mf.ContainsKey(rel) || !File.Exists(bak))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(bak));
                    string h0 = Sha(dest);
                    File.Copy(dest, bak, true);
                    if (Sha(bak) != h0) throw new Exception("Yedek doğrulanamadı: " + rel);
                    mf[rel] = new JsonObject { ["originalSha256"] = h0, ["originalSize"] = new FileInfo(bak).Length, ["patchedSha256"] = null };
                    SaveManifest(manifest);
                }
                File.Copy(src, dest, true);
                string h = Sha(src);
                if (Sha(dest) != h) throw new Exception("Kurulum doğrulanamadı: " + rel + " (\"Geri yükle\" düğmesini kullanın)");
                mf[rel]["patchedSha256"] = h; SaveManifest(manifest);
                progress(i, files.Count);
                if (i % 50 == 0 || i == files.Count) log($"  {i}/{files.Count}");
            }
            log("Kuruldu. Oyunu İngilizce dilde açın.");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    public void Uninstall(string bio, Action<int, int> progress)
    {
        if (GameRunning()) throw new Exception("Önce Mass Effect / başlatıcıyı kapatın.");
        var mp = Path.Combine(backupDir, "manifest.json");
        if (!File.Exists(mp)) throw new Exception("Yedek bulunamadı: " + mp);
        var mf = JsonNode.Parse(File.ReadAllText(mp))["files"].AsObject();
        log("Yedekler doğrulanıyor...");
        foreach (var (rel, v) in mf)
        {
            var bak = Path.Combine(backupDir, rel);
            if (!File.Exists(bak)) throw new Exception("Yedek dosyası yok: " + bak);
            if (!Sha(bak).Equals((string)v["originalSha256"], StringComparison.OrdinalIgnoreCase)) throw new Exception("Yedek bozuk: " + rel);
        }
        log($"{mf.Count} dosya geri yükleniyor...");
        int i = 0;
        foreach (var (rel, v) in mf)
        {
            i++;
            var dest = Path.Combine(bio, rel); string want = (string)v["originalSha256"];
            if (!File.Exists(dest) || !Sha(dest).Equals(want, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(Path.Combine(backupDir, rel), dest, true);
                if (!Sha(dest).Equals(want, StringComparison.OrdinalIgnoreCase)) throw new Exception("Geri yükleme doğrulanamadı: " + rel);
            }
            progress(i, mf.Count);
        }
        log("Orijinal dosyalar geri yüklendi. Yedekler yerinde duruyor.");
    }
}
