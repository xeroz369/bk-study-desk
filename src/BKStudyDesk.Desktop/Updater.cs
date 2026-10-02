using Avalonia.Controls;
using SoHocTap.Core;
using SoHocTap.Ui;
using Velopack;

namespace BKStudyDesk.Desktop;

/// <summary>
/// Cập nhật cho bản đa nền tảng (AppImage trên Linux): chỉ kiểm tra khi người dùng bấm "Kiểm tra cập nhật" ở trang Giới thiệu,
/// có bản mới thì người dùng bấm "Cập nhật" mới tải, cài rồi mở lại. Không chạy ngầm, không tự kiểm tra.
/// </summary>
internal static class Updater
{
    private static UpdateManager Manager()
    {
        var src = Config.Str("app.update.source");
        if (!UpdatePolicy.IsValidSource(src, Directory.Exists)) throw new InvalidOperationException(L.T("update.badSource"));
        // Gói cập nhật ở release cố định "updates" của repo (cùng chỗ với bản Windows).
        return new UpdateManager(src.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ? src.TrimEnd('/') + "/releases/download/updates" : src);
    }

    /// <summary>Khối nút cho trang Giới thiệu; bản không cài bằng Velopack (chạy từ thư mục build) thì không hiện gì.</summary>
    public static Control? Panel()
    {
        UpdateManager mgr;
        try { mgr = Manager(); }
        catch (Exception e) when (e is not OutOfMemoryException) { return null; }
        if (!mgr.IsInstalled) return null;
        var status = new TextBlock { Margin = new(0, 6, 0, 0), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var check = new Button { Content = L.T("update.check") };
        var install = new Button { Content = L.T("update.install"), IsVisible = false, Margin = new(8, 0, 0, 0) };
        UpdateInfo? found = null;
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            status.Text = L.T("update.checking");
            try
            {
                found = await mgr.CheckForUpdatesAsync();
                status.Text = found is null ? L.F("update.latest", SoHocTap.Shell.AppInfo.Version) : L.F("update.available", found.TargetFullRelease.Version);
                install.IsVisible = found is not null;
            }
            catch (Exception e) when (e is not OutOfMemoryException) { status.Text = L.F("update.error", e.Message); }
            check.IsEnabled = true;
        };
        install.Click += async (_, _) =>
        {
            if (found is null) return;
            install.IsEnabled = check.IsEnabled = false;
            try
            {
                await mgr.DownloadUpdatesAsync(found, p => Avalonia.Threading.Dispatcher.UIThread.Post(() => status.Text = L.F("update.downloading", p)));
                mgr.ApplyUpdatesAndRestart(found.TargetFullRelease);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                status.Text = L.F("update.failed", e.Message);
                install.IsEnabled = check.IsEnabled = true;
            }
        };
        return new StackPanel
        {
            Margin = new(0, 12, 0, 0),
            Children = { new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { check, install } }, status },
        };
    }
}
