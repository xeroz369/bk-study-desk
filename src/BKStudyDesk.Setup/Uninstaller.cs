using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BKStudyDesk.Setup;

/// <summary>
/// Bộ gỡ cài đặt tiếng Việt (Uninstall.exe trong thư mục cài, đăng ký thay trình gỡ mặc định của Velopack).
/// Hỏi có xóa dữ liệu app không (mặc định giữ), đóng app nếu đang chạy, gọi Update.exe --uninstall --silent của Velopack
/// (xóa app, lối tắt, mục tự chạy), rồi dọn phần còn sót: thư mục cài, khóa gỡ cài đặt, log. Không bao giờ xóa tài liệu môn học.
/// Dòng lệnh: Uninstall.exe --uninstall [--silent] [--delete-data].
/// </summary>
internal static class Uninstaller
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
    public const string FileName = "Uninstall.exe";

    /// <summary>Chép Uninstall.exe (nhúng trong bộ cài) vào thư mục cài và đăng ký nó trong Cài đặt → Ứng dụng.</summary>
    public static void Register(string root, string packId)
    {
        using (var res = typeof(Uninstaller).Assembly.GetManifestResourceStream("uninstall.exe"))
        {
            if (res is null) return;   // bộ cài build không kèm bộ gỡ: giữ trình gỡ của Velopack
            using var f = File.Create(Path.Combine(root, FileName));
            res.CopyTo(f);
        }
        using var k = Registry.CurrentUser.OpenSubKey(UninstallKey + packId, writable: true);
        if (k is null) return;
        var exe = Path.Combine(root, FileName);
        k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
        k.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --silent");
    }

    /// <summary>
    /// Chạy từ thư mục cài thì chép mình sang thư mục tạm, chạy lại từ đó rồi thoát ngay: exe không tự xóa được chính nó,
    /// và Update.exe của Velopack đóng mọi tiến trình còn chạy trong thư mục cài. Bản trong thư mục tạm làm việc gỡ;
    /// chạy --silent thì lỗi (nếu có) ghi vào %TEMP%\BKStudyDesk-uninstall.log. Trả mã thoát.
    /// </summary>
    public static int Run(string[] args)
    {
        var silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase);
        var i = Array.FindIndex(args, a => a.Equals("--root", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length)
        {
            var self = Application.ExecutablePath;
            var tmp = Path.Combine(Path.GetTempPath(), "BKStudyDesk-uninstall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            var copy = Path.Combine(tmp, FileName);
            File.Copy(self, copy);
            var rest = string.Join(" ", args.Select(a => $"\"{a}\""));
            Process.Start(new ProcessStartInfo(copy, $"{rest} --root \"{Path.GetDirectoryName(self)}\"") { UseShellExecute = false })?.Dispose();
            return 0;
        }
        try { return RunJob(args, i, silent); }
        finally { DeleteSelfLater(); }
    }

    private static int RunJob(string[] args, int i, bool silent)
    {
        var root = args[i + 1];
        var job = new UninstallJob(root, Installer.PackId);
        if (silent)
        {
            var error = job.RunAsync(args.Contains("--delete-data", StringComparer.OrdinalIgnoreCase)).GetAwaiter().GetResult();
            // Chạy im lặng thì không có cửa sổ: ghi lý do lỗi ra %TEMP%\BKStudyDesk-uninstall.log để tra.
            if (error is not null) try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "BKStudyDesk-uninstall.log"), $"{DateTime.Now:s} {error}{Environment.NewLine}"); } catch (IOException) { }
            return error is null ? 0 : 1;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new UninstallForm(job));
        return 0;
    }

    /// <summary>Bản chạy trong thư mục tạm: nhờ cmd xóa thư mục tạm của mình sau khi thoát.</summary>
    private static void DeleteSelfLater()
    {
        var dir = Path.GetDirectoryName(Application.ExecutablePath)!;
        if (!Path.GetFileName(dir).StartsWith("BKStudyDesk-uninstall-", StringComparison.Ordinal)) return;
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/c timeout /t 3 /nobreak >nul & rmdir /s /q \"{dir}\"")
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })?.Dispose();
    }
}

/// <summary>Các bước gỡ, dùng chung cho giao diện và dòng lệnh.</summary>
internal sealed class UninstallJob(string root, string packId)
{
    public string Root { get; } = root;
    public string DataRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), packId + ".Data");

    /// <summary>Thư mục tài liệu môn học người dùng đã chọn (folders.root trong config), để chỉ ra chứ không xóa.</summary>
    public string? DocumentsFolder
    {
        get
        {
            try
            {
                var cfg = Path.Combine(DataRoot, "data", "config.json");
                if (!File.Exists(cfg)) return null;
                var m = Regex.Match(File.ReadAllText(cfg), "\"root\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                var path = m.Success ? Regex.Unescape(m.Groups[1].Value) : "";
                return path.Length > 0 && Directory.Exists(path) ? path : null;
            }
            catch (IOException) { return null; }
        }
    }

    public bool Installed => File.Exists(Path.Combine(Root, "Update.exe"));

    /// <summary>Phiên bản đang cài (thẻ &lt;version&gt; trong current\sq.version; bộ gỡ không đổi theo bản cập nhật nên phải đọc ở đây).</summary>
    public string InstalledVersion
    {
        get
        {
            try
            {
                var f = Path.Combine(Root, "current", "sq.version");
                var m = File.Exists(f) ? Regex.Match(File.ReadAllText(f), "<version>([^<]+)</version>") : null;
                return m is { Success: true } ? m.Groups[1].Value : "";
            }
            catch (IOException) { return ""; }
        }
    }

    /// <summary>Tiến trình BK Study Desk đang chạy từ thư mục cài này.</summary>
    public Process[] RunningApps() => Process.GetProcessesByName("BKStudyDesk").Where(p =>
    {
        try { return p.MainModule?.FileName.StartsWith(Root, StringComparison.OrdinalIgnoreCase) == true; }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }).ToArray();

    /// <summary>Gỡ. Trả null nếu xong, không thì lý do lỗi.</summary>
    public async Task<string?> RunAsync(bool deleteData)
    {
        if (!Installed) return $"Không tìm thấy bản cài ở {Root}.";
        foreach (var p in RunningApps()) { try { p.Kill(); p.WaitForExit(5000); } catch (InvalidOperationException) { } }
        using (var p = Process.Start(new ProcessStartInfo(Path.Combine(Root, "Update.exe"), "--uninstall --silent") { UseShellExecute = false, CreateNoWindow = true })!)
        {
            await Task.Run(() => p.WaitForExit(120_000));
            if (!p.HasExited) return "Gỡ quá lâu không xong, hãy thử lại.";
        }
        if (deleteData) await DeleteDirAsync(DataRoot);
        // Phần Velopack để lại: chính Update.exe, khóa gỡ cài đặt (khi gỡ lỗi giữa chừng), log.
        await DeleteDirAsync(Root);
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Installer.PackId, throwOnMissingSubKey: false); }
        catch (UnauthorizedAccessException) { }
        try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "velopack", $"velopack_{Installer.PackId}.log")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (!Directory.Exists(Root)) return null;
        // Còn lại chỉ là Uninstall.exe gốc đang chờ (chạy --silent, nó đợi bản chạy từ thư mục tạm này): nhờ cmd xóa thư mục
        // sau vài giây, khi cả hai đã thoát.
        if (Directory.EnumerateFileSystemEntries(Root).All(f => Path.GetFileName(f).Equals(Uninstaller.FileName, StringComparison.OrdinalIgnoreCase)))
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/c timeout /t 3 /nobreak >nul & rmdir /s /q \"{Root}\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            return null;
        }
        return $"Đã gỡ app nhưng còn sót thư mục {Root}, bạn có thể tự xóa.";
    }

    /// <summary>Xóa thư mục, thử lại vài lần (Update.exe có thể còn giữ file thêm vài giây sau khi thoát).</summary>
    private static async Task DeleteDirAsync(string dir)
    {
        for (var i = 0; i < 20 && Directory.Exists(dir); i++)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { await Task.Delay(500); }
        }
    }
}

/// <summary>Cửa sổ gỡ cài đặt: hỏi có xóa dữ liệu không, gỡ, báo kết quả.</summary>
internal sealed class UninstallForm : Form
{
    private readonly UninstallJob _job;
    private readonly CheckBox _deleteData = new() { AutoSize = true, Text = "Xóa cả dữ liệu của app", Margin = new Padding(0, 4, 0, 0) };
    private readonly Label _status = new() { AutoSize = true };
    private readonly ProgressBar _bar = new() { Style = ProgressBarStyle.Marquee, Width = 500, Visible = false };
    private readonly Button _ok = new() { Text = "Gỡ cài đặt", AutoSize = true, MinimumSize = new Size(110, 0) };
    private readonly Button _cancel = new() { Text = "Hủy", AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
    private bool _done;

    public UninstallForm(UninstallJob job)
    {
        _job = job;
        Text = $"Gỡ BK Study Desk {job.InstalledVersion}".TrimEnd();
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
        try { Icon = Icon.ExtractAssociatedIcon(Path.Combine(job.Root, "current", "BKStudyDesk.exe")); } catch (Exception e) when (e is ArgumentException or FileNotFoundException) { }

        var title = new Label { Text = "Gỡ BK Study Desk khỏi máy?", AutoSize = true, Font = new Font(Font.FontFamily, 11f, FontStyle.Bold) };
        var where = new Label { Text = $"App ở {job.Root}.", AutoSize = true, MaximumSize = new Size(Px(500), 0), Margin = new Padding(0, 6, 0, 10) };
        MinimumSize = new Size(Px(560), 0);
        Padding = new Padding(Px(20));
        _status.MaximumSize = new Size(Px(500), 0);
        _bar.Width = Px(500);
        var dataHint = new Label
        {
            Text = "Phiên đăng nhập, kết quả luyện tập, quiz tự soạn và cài đặt. Bỏ trống nếu bạn định cài lại và muốn dùng tiếp.",
            AutoSize = true,
            MaximumSize = new Size(Px(480), 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(Px(20), 0, 0, 0),
        };
        var docs = job.DocumentsFolder;
        var keep = new Label
        {
            Text = docs is null ? "Tài liệu môn học đã tải không bị xóa." : $"Tài liệu môn học ở {docs} không bị xóa.",
            AutoSize = true,
            MaximumSize = new Size(Px(500), 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 10, 0, 0),
        };
        var open = new LinkLabel { Text = "Mở thư mục tài liệu", AutoSize = true, Visible = docs is not null };
        open.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{docs}\"") { UseShellExecute = true });
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.AddRange([_cancel, _ok]);
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Px(500)));   // cột cố định: chữ xuống dòng theo 500px, không bóp hẹp
        foreach (Control c in new Control[] { title, where, _deleteData, dataHint, keep, open, _bar, _status, buttons }) layout.Controls.Add(c);
        Controls.Add(layout);
        _ok.Click += async (_, _) => await OnOkAsync();
        if (!job.Installed) { _status.Text = $"Không tìm thấy bản cài ở {job.Root}."; _ok.Enabled = false; }
    }

    /// <summary>
    /// Kích thước 96 DPI → pixel thật của màn hình. WinForms trên .NET Framework không tự nhân các số đặt trong code, và
    /// DeviceDpi luôn báo 96 khi thiếu cấu hình trong app.config, nên đọc DPI thật qua Graphics.
    /// </summary>
    private int Px(int v) => Dpi.Px(this, v);

    private async Task OnOkAsync()
    {
        if (_done) { Close(); return; }
        if (_job.RunningApps().Length > 0
            && MessageBox.Show(this, "BK Study Desk đang chạy. App sẽ được đóng để gỡ.", Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
            return;
        _ok.Enabled = _cancel.Enabled = _deleteData.Enabled = false;
        _bar.Visible = true;
        _status.Text = "Đang gỡ…";
        var deleteData = _deleteData.Checked;
        var error = await _job.RunAsync(deleteData);
        _bar.Visible = false;
        _done = true;
        _status.ForeColor = error is null ? SystemColors.ControlText : Color.Firebrick;
        _status.Text = error ?? (deleteData
            ? "Đã gỡ BK Study Desk và xóa dữ liệu của app."
            : $"Đã gỡ BK Study Desk. Dữ liệu của app vẫn giữ ở {_job.DataRoot}; cài lại sẽ dùng tiếp.");
        _ok.Text = "Đóng";
        _ok.Enabled = true;
    }
}
