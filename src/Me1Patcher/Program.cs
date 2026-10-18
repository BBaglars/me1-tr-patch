using System.Reflection;
using System.Text;

namespace Me1Patcher;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string backup = null, game = null;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--backup") backup = args[i + 1];
            if (args[i] == "--game") game = args[i + 1];
        }
        if (args.Contains("--install") || args.Contains("--uninstall"))
        {
            // headless mode for scripts/tests: ME1-TR-Yama.exe --install --game <dir> [--backup <dir>]
            AttachConsole(-1);
            var bio = Installer.ResolveBioGame(game ?? "") ?? throw new Exception("--game gerekli veya geçersiz.");
            var inst = new Installer(backup ?? Installer.DefaultBackup, Console.WriteLine);
            if (args.Contains("--install")) inst.Install(bio, (_, _) => { }); else inst.Uninstall(bio, (_, _) => { });
            return;
        }
        Application.Run(new MainForm(game, backup ?? Installer.DefaultBackup));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
}

// Window: game folder box + browse, Install / Restore buttons, progress bar and log.
sealed class MainForm : Form
{
    readonly TextBox path = new() { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    readonly Button browse = new() { Text = "Gözat...", Anchor = AnchorStyles.Top | AnchorStyles.Right };
    readonly Button install = new() { Text = "Yamayı Kur / Güncelle", Height = 36 };
    readonly Button restore = new() { Text = "Orijinali Geri Yükle", Height = 36 };
    readonly ProgressBar bar = new() { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
    readonly TextBox logBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
    readonly Label status = new() { AutoSize = true };
    readonly string backupDir;

    public MainForm(string game, string backupDir)
    {
        this.backupDir = backupDir;
        Text = "Mass Effect 1 Türkçe Yama v" + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        ClientSize = new Size(640, 430);
        MinimumSize = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        var intro = new Label { Left = 14, Top = 12, Width = 612, Height = 40, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "Mass Effect 1 Legendary Edition için Türkçe metin/altyazı yaması. Ses İngilizce kalır; oyunu İngilizce dilde başlatın." };
        var lbl = new Label { Left = 14, Top = 58, AutoSize = true, Text = "Oyunun klasörü (Mass Effect Legendary Edition):" };
        path.SetBounds(14, 82, 522, 26);
        browse.SetBounds(544, 80, 82, 30);
        install.SetBounds(14, 122, 300, 36); restore.SetBounds(326, 122, 300, 36);
        install.Anchor = AnchorStyles.Top | AnchorStyles.Left; restore.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        bar.SetBounds(14, 170, 612, 18);
        status.Location = new Point(14, 194);
        logBox.SetBounds(14, 218, 612, 198);
        Controls.AddRange(new Control[] { intro, lbl, path, browse, install, restore, bar, status, logBox });

        string found = game != null ? Installer.ResolveBioGame(game) : Installer.FindGame();
        path.Text = found ?? game ?? "";
        status.Text = found != null ? "Oyun bulundu." : "Oyun otomatik bulunamadı: klasörü yazın veya Gözat'a basın.";

        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { Description = "Mass Effect Legendary Edition klasörünü seçin" };
            if (d.ShowDialog(this) == DialogResult.OK) { path.Text = d.SelectedPath; Check(); }
        };
        path.Leave += (_, _) => Check();
        install.Click += async (_, _) => await RunAsync(true);
        restore.Click += async (_, _) => await RunAsync(false);
    }

    string Check()
    {
        var bio = Installer.ResolveBioGame(path.Text);
        status.Text = bio != null ? "Oyun bulundu: " + bio : "Bu klasörde Mass Effect 1 (Legendary Edition) bulunamadı.";
        return bio;
    }

    void Log(string s)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(s)); return; }
        logBox.AppendText(s + Environment.NewLine);
    }

    async Task RunAsync(bool doInstall)
    {
        var bio = Check();
        if (bio == null) { MessageBox.Show(this, "Geçerli bir Mass Effect Legendary Edition klasörü girin.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!doInstall && MessageBox.Show(this, "Orijinal dosyalar geri yüklenecek (yama kaldırılır). Devam edilsin mi?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (doInstall && MessageBox.Show(this, "Yama şu klasöre kurulacak:\n" + bio + "\n\nOrijinal dosyalar önce yedeklenir:\n" + backupDir + "\n\nDevam edilsin mi?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        install.Enabled = restore.Enabled = browse.Enabled = path.Enabled = false;
        bar.Value = 0; logBox.Clear();
        var writer = new GuiWriter(Log);
        Console.SetOut(writer); Console.SetError(writer);
        var inst = new Installer(backupDir, Log);
        Action<int, int> prog = (i, n) => BeginInvoke(() => { bar.Maximum = n; bar.Value = i; });
        try
        {
            await Task.Run(() => { if (doInstall) inst.Install(bio, prog); else inst.Uninstall(bio, prog); });
            status.Text = doInstall ? "Tamamlandı." : "Geri yüklendi.";
            MessageBox.Show(this, doInstall ? "Yama kuruldu. Oyunu İngilizce dilde açın." : "Orijinal dosyalar geri yüklendi.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e)
        {
            Log("HATA: " + e.Message); status.Text = "Hata oluştu.";
            MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { install.Enabled = restore.Enabled = browse.Enabled = path.Enabled = true; }
    }
}

// Routes Console output of the packaging code into the log box.
sealed class GuiWriter : TextWriter
{
    readonly Action<string> log; readonly StringBuilder sb = new();
    public GuiWriter(Action<string> log) { this.log = log; }
    public override Encoding Encoding => Encoding.UTF8;
    public override void Write(char c)
    {
        if (c == '\n') { Flush(); return; }
        if (c != '\r') sb.Append(c);
    }
    public override void Flush() { if (sb.Length == 0) return; var s = sb.ToString(); sb.Clear(); log(s); }
}
