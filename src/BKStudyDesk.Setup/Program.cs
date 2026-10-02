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

/// <summary>Cửa sổ cài đặt: một bước chọn thư mục, một bước chờ, một bước xong.</summary>
internal sealed class SetupForm : Form
{
    private readonly TextBox _folder = new() { Width = 400 };
    private readonly Button _browse = new() { Text = "Chọn…", AutoSize = true };
    private readonly Label _note = new() { AutoSize = true, MaximumSize = new Size(500, 0), ForeColor = SystemColors.GrayText };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(500, 0) };
    private readonly ProgressBar _bar = new() { Style = ProgressBarStyle.Marquee, Width = 500, Visible = false };
    private readonly Button _ok = new() { Text = "Cài đặt", AutoSize = true, MinimumSize = new Size(100, 0) };
    private readonly Button _cancel = new() { Text = "Hủy", AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
    private bool _done;

    public SetupForm(string dir)
    {
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
        Padding = new Padding(20);
        AcceptButton = _ok;
        CancelButton = _cancel;

        _folder.Width = Dpi.Px(this, 400);
        Padding = new Padding(Dpi.Px(this, 20));
        _note.MaximumSize = _status.MaximumSize = new Size(Dpi.Px(this, 500), 0);
        _bar.Width = Dpi.Px(this, 500);
        _folder.Text = dir;
        _note.Text = Installer.Existing() is { } old
            ? $"BK Study Desk đã được cài ở {old}. Cài lại vào đây sẽ cập nhật bản này, dữ liệu vẫn giữ nguyên."
            : "Không cần quyền quản trị. Phiên đăng nhập và dữ liệu của app lưu riêng cho tài khoản Windows của bạn, "
              + "không nằm trong thư mục này. Thư mục lưu tài liệu môn học chọn sau, lúc mở app lần đầu.";

        var title = new Label { Text = $"Cài BK Study Desk {Installer.Version} vào thư mục:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 6) };
        row.Controls.AddRange([_folder, _browse]);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.AddRange([_cancel, _ok]);
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        foreach (Control c in new Control[] { title, row, _note, _bar, _status, buttons }) layout.Controls.Add(c);
        Controls.Add(layout);

        _browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { Description = "Chọn thư mục để cài BK Study Desk (app sẽ nằm trong thư mục con BKStudyDesk)", ShowNewFolderButton = true };
            if (Directory.Exists(_folder.Text)) d.SelectedPath = _folder.Text;
            if (d.ShowDialog(this) == DialogResult.OK) _folder.Text = d.SelectedPath;
        };
        _ok.Click += async (_, _) => await OnOkAsync();
    }

    private async Task OnOkAsync()
    {
        if (_done) { Close(); return; }
        _ok.Enabled = _cancel.Enabled = _browse.Enabled = _folder.Enabled = false;
        _bar.Visible = true;
        _status.Text = "Đang cài đặt, vui lòng đợi…";
        var error = await Installer.RunAsync(_folder.Text);
        _bar.Visible = false;
        if (error is not null)
        {
            _status.Text = error;
            _status.ForeColor = Color.Firebrick;
            _ok.Enabled = _cancel.Enabled = _browse.Enabled = _folder.Enabled = true;
            return;
        }
        _done = true;
        Installer.OpenApp();
        _status.ForeColor = SystemColors.ControlText;
        _status.Text = "Đã cài xong và đã mở BK Study Desk. Lối tắt có ở Desktop và Start menu.";
        _ok.Text = "Xong";
        _ok.Enabled = true;
    }
}
