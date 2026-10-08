using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BKStudyDesk.Setup;

/// <summary>
/// Bộ cài tiếng Việt: chọn thư mục cài (mặc định %LOCALAPPDATA%\BKStudyDesk), rồi chạy Setup.exe của Velopack (nhúng trong exe này)
/// với --silent --installto. Không cần quyền admin; dữ liệu app luôn ở %LOCALAPPDATA%\BKStudyDesk.Data dù cài ở đâu.
/// Dòng lệnh (cho test, cài hàng loạt): BKStudyDesk-Setup.exe --silent [--installto &lt;thư mục&gt;].
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Cùng một exe làm bộ gỡ (Uninstall.exe trong thư mục cài), xem Uninstaller.
        if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase)) return Uninstaller.Run(args);
        var i = Array.FindIndex(args, a => a.Equals("--installto", StringComparison.OrdinalIgnoreCase));
        var dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Installer.DefaultDir;
        if (args.Contains("--silent", StringComparer.OrdinalIgnoreCase))
            return Installer.RunAsync(dir).GetAwaiter().GetResult() is null ? 0 : 1;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm(dir));   // --installto không kèm --silent: điền sẵn thư mục trong cửa sổ
        return 0;
    }
}

internal static class Installer
{
    /// <summary>Phiên bản của gói nhúng (build-all.ps1 truyền -p:Version), vd. "1.0.6".</summary>
    public static string Version => (FileVersionInfo.GetVersionInfo(Application.ExecutablePath).ProductVersion ?? "").Split('+')[0];

    /// <summary>Pack id của gói nhúng (build-all.ps1 truyền -p:PackId, mặc định BKStudyDesk).</summary>
    public static string PackId => typeof(Installer).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "PackId")?.Value ?? InstallTarget.AppFolder;

    /// <summary>Pack id của bản đã cài trong thư mục (thẻ &lt;id&gt; trong current\sq.version).</summary>
    public static string? InstalledPackId(string dir)
    {
        try
        {
            var f = Path.Combine(dir, "current", "sq.version");
            var m = File.Exists(f) ? System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f), "<id>([A-Za-z0-9._-]+)</id>") : null;
            return m is { Success: true } ? m.Groups[1].Value : null;
        }
        catch (IOException) { return null; }
    }

    private static (string? Dir, string? Error) Target(string dir) =>
        InstallTarget.Resolve(dir, Directory.Exists, d => Directory.EnumerateFileSystemEntries(d), File.Exists, PackId, InstalledPackId);

    public static string DefaultDir => Existing() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InstallTarget.AppFolder);

    /// <summary>Thư mục của bản đã cài (khóa gỡ cài đặt Velopack ghi trong HKCU), để cài lại đúng chỗ.</summary>
    public static string? Existing()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + PackId);
        return k?.GetValue("InstallLocation") is string s && s.Length > 0 ? s : null;
    }

    /// <summary>Thư mục cài thật của lần cài gần nhất (sau khi thêm thư mục con BKStudyDesk).</summary>
    public static string? Installed { get; private set; }

    /// <summary>Mở app vừa cài (Setup.exe chạy --silent không tự mở).</summary>
    public static void OpenApp()
    {
        var exe = Installed is null ? null : Path.Combine(Installed, "current", "BKStudyDesk.exe");
        if (exe is not null && File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
    }

    /// <summary>Chạy Setup.exe nhúng sẵn. Trả null nếu xong, không thì lý do lỗi.</summary>
    public static async Task<string?> RunAsync(string dir)
    {
        var (target, error) = Target(dir);
        if (target is null) return error;
        try
        {
            Directory.CreateDirectory(target);
            var probe = Path.Combine(target, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Không ghi được vào {target} (thư mục này cần quyền quản trị). Hãy chọn thư mục khác, ví dụ trong ổ D:.";
        }
        var tmp = Path.Combine(Path.GetTempPath(), "BKStudyDesk-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var setup = Path.Combine(tmp, "Setup.exe");
        try
        {
            using (var res = typeof(Installer).Assembly.GetManifestResourceStream("setup.exe"))
            {
                if (res is null) return "Bộ cài bị thiếu dữ liệu, hãy tải lại.";
                using var f = File.Create(setup);
                await res.CopyToAsync(f);
            }
            var psi = new ProcessStartInfo(setup) { UseShellExecute = false, CreateNoWindow = true, Arguments = $"--silent --installto \"{target}\"" };
            using var p = Process.Start(psi)!;
            await Task.Run(p.WaitForExit);
            if (p.ExitCode != 0) return $"Cài đặt không thành công (mã lỗi {p.ExitCode}).";
            Installed = target;
            try { Uninstaller.Register(target, PackId); }   // bộ gỡ tiếng Việt thay trình gỡ mặc định
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return null;
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

}

/// <summary>Đổi kích thước viết theo 96 DPI sang pixel thật của màn hình (DPI đọc qua Graphics, xem UninstallForm.Px).</summary>
internal static class Dpi
{
    public static int Px(Control c, int v)
    {
        using var g = c.CreateGraphics();
        return (int)(v * g.DpiX / 96f);
    }
}

/// <summary>
/// Màu của bộ cài theo chế độ sáng, tối của Windows (WinForms trên .NET Framework không tự theo; đọc AppsUseLightTheme của tài khoản).
/// Nút chính màu xanh BK như màu nhấn mặc định của app (DefaultConfig app.accent).
/// </summary>
internal sealed class Palette
{
    public bool Dark { get; }
    public Color Back => Dark ? Color.FromArgb(0x1E, 0x23, 0x2A) : Color.White;
    public Color Text => Dark ? Color.FromArgb(0xE6, 0xED, 0xF3) : Color.FromArgb(0x1F, 0x23, 0x28);
    public Color Sub => Dark ? Color.FromArgb(0x9A, 0xA4, 0xAF) : Color.FromArgb(0x59, 0x63, 0x6E);
    public Color Link => Dark ? Color.FromArgb(0x6C, 0xB6, 0xFF) : Color.FromArgb(0x04, 0x4C, 0xC8);
    public Color Danger => Dark ? Color.FromArgb(0xFF, 0x6B, 0x7A) : Color.FromArgb(0xC4, 0x31, 0x4B);
    public Color Accent => Color.FromArgb(0x02, 0x77, 0x9E);

    /// <param name="forced">"dark" hay "light" (cờ --theme= khi chụp kiểm tra); null là theo Windows.</param>
    public Palette(string? forced)
    {
        if (forced is "dark" or "light") { Dark = forced == "dark"; return; }
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        Dark = k?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Thanh tiêu đề tối (DWMWA_USE_IMMERSIVE_DARK_MODE = 20, Windows 10 2004 trở lên; bản cũ hơn bỏ qua, vẫn chạy).</summary>
    public void TitleBar(Form f)
    {
        var on = Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int));
    }
}

/// <summary>
/// Cửa sổ cài đặt, mặc định một chạm: bấm Cài đặt là cài vào thư mục mặc định (hay thư mục của bản đã cài), xong thì mở app và tự đóng.
/// Muốn cài chỗ khác thì bấm Đổi thư mục (hộp chọn thư mục của Windows), không cần dòng lệnh. Đang cài thì thanh tiến độ ngay trong cửa sổ.
/// </summary>
internal sealed class SetupForm : Form
{
    private readonly Palette _c = new(Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--theme=", StringComparison.Ordinal))?.Substring(8));
    private readonly Label _where = new() { AutoSize = true, AutoEllipsis = true };
    private readonly LinkLabel _change = new() { Text = "Đổi thư mục...", AutoSize = true };
    private readonly Label _note = new() { AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Visible = false };
    private readonly ProgressBar _bar = new() { Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly Button _ok = new() { Text = "Cài đặt", FlatStyle = FlatStyle.Flat };
    private string _dir;

    public SetupForm(string dir)
    {
        _dir = dir;
        Text = $"Cài đặt BK Study Desk {Installer.Version}";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleDimensions = new SizeF(96f, 96f);   // kích thước trong code tính theo 96 DPI, WinForms tự nhân theo màn hình
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape && _ok.Enabled) Close(); };
        AcceptButton = _ok;
        BackColor = _c.Back;
        ForeColor = _c.Text;
        Padding = new Padding(Dpi.Px(this, 24));
        var width = Dpi.Px(this, 460);

        var logo = new PictureBox { Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(Dpi.Px(this, 40), Dpi.Px(this, 40)), Margin = new Padding(0, 0, Dpi.Px(this, 12), 0) };
        var name = new Label { Text = "BK Study Desk", AutoSize = true, Font = new Font("Segoe UI Semibold", 15f) };
        var tagline = new Label { Text = "Tài liệu, hạn nộp, lịch học của sinh viên Bách Khoa, lưu sẵn trên máy để xem cả khi không có mạng.",
            AutoSize = true, MaximumSize = new Size(width - Dpi.Px(this, 52), 0), ForeColor = _c.Sub };
        var names = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        names.Controls.AddRange([name, tagline]);
        var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, Dpi.Px(this, 20)) };
        head.Controls.AddRange([logo, names]);

        _ok.Size = new Size(width, Dpi.Px(this, 40));
        _ok.BackColor = _c.Accent;
        _ok.ForeColor = Color.White;
        _ok.Font = new Font("Segoe UI Semibold", 11f);
        _ok.FlatAppearance.BorderSize = 0;
        _ok.FlatAppearance.BorderColor = _c.Accent;   // nút mặc định (AcceptButton) vẫn vẽ viền 1 px màu chữ nếu không đặt
        _ok.Margin = new Padding(0, 0, 0, Dpi.Px(this, 10));

        _where.ForeColor = _change.ForeColor = _c.Sub;
        _where.MaximumSize = new Size(width - Dpi.Px(this, 110), 0);
        _change.LinkColor = _change.ActiveLinkColor = _change.VisitedLinkColor = _c.Link;
        _change.Margin = new Padding(Dpi.Px(this, 8), _where.Margin.Top, 0, 0);   // cùng dòng chữ với "Cài vào"
        var whereRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        whereRow.Controls.AddRange([_where, _change]);

        _note.MaximumSize = _status.MaximumSize = new Size(width, 0);
        _note.ForeColor = _c.Sub;
        _note.Margin = new Padding(0, Dpi.Px(this, 10), 0, 0);
        _note.Text = Installer.Existing() is not null
            ? "Đã có bản cài ở thư mục này: cài lại sẽ cập nhật, dữ liệu và tài liệu vẫn giữ nguyên."
            : "Không cần quyền quản trị. Thư mục lưu tài liệu chọn khi mở app lần đầu.";
        _bar.Size = new Size(width, Dpi.Px(this, 6));
        _bar.Margin = new Padding(0, Dpi.Px(this, 4), 0, Dpi.Px(this, 6));

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, BackColor = _c.Back };
        foreach (Control c in new Control[] { head, _ok, _bar, _status, whereRow, _note }) layout.Controls.Add(c);
        Controls.Add(layout);
        ShowWhere();

        HandleCreated += (_, _) => _c.TitleBar(this);
        _change.LinkClicked += (_, _) =>
        {
            using var d = new FolderBrowserDialog { Description = "Chọn thư mục để cài BK Study Desk (app sẽ nằm trong thư mục con BKStudyDesk)", ShowNewFolderButton = true };
            if (Directory.Exists(_dir)) d.SelectedPath = _dir;
            if (d.ShowDialog(this) != DialogResult.OK) return;
            _dir = d.SelectedPath;
            ShowWhere();
        };
        _ok.Click += async (_, _) => await OnOkAsync();
    }

    private void ShowWhere() => _where.Text = "Cài vào: " + _dir;

    private async Task OnOkAsync()
    {
        _ok.Enabled = _change.Enabled = false;
        _ok.Text = "Đang cài đặt...";
        _bar.Visible = true;
        _status.Visible = false;
        var error = await Installer.RunAsync(_dir);
        _bar.Visible = false;
        if (error is not null)
        {
            _status.Text = error;
            _status.ForeColor = _c.Danger;
            _status.Visible = true;
            _ok.Text = "Cài đặt";
            _ok.Enabled = _change.Enabled = true;
            return;
        }
        // Cài xong: mở app rồi đóng bộ cài, khỏi bấm thêm (lối tắt có ở Desktop và Start menu).
        Installer.OpenApp();
        Close();
    }
}
