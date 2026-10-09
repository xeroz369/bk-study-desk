using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang đăng nhập nhìn thấy được: chạy một LoginFlow. Xong thì báo Finished (MainWindow gỡ khung); Hủy thì báo "cancel".
/// Quá 20 giây chưa mở được trang nào hay trang trường lỗi: báo "error" (dải báo hiện lý do và nút Đăng nhập lại, gọi Retry).
/// </summary>
public partial class LoginView : UserControl
{
    private readonly Action<string, string> _progress = (_, _) => { };
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(20) };
    private bool _shown;   // trang trường đã hiện ít nhất một lần trong lượt này
    private bool _done;

    /// <summary>Lượt đăng nhập kết thúc (xong hay hủy): chủ khung gỡ LoginView.</summary>
    public event Action? Finished;

    public LoginView() => InitializeComponent();

    internal LoginView(Action<string, string> progress) : this()
    {
        _progress = progress;
        _slow.Tick += (_, _) => { _slow.Stop(); if (!_shown) Problem(L.T("login.slow")); };
        // Trang web nhúng chỉ tải được khi đã nằm trong cửa sổ: bắt đầu lúc gắn vào cây giao diện, một lần.
        AttachedToVisualTree += (_, _) => { if (Host.Child is null) Retry(); };
        DetachedFromVisualTree += (_, _) => _slow.Stop();
    }

    /// <summary>Lượt mới trên trang mới (lần đầu, hay người dùng bấm Đăng nhập lại khi trang lỗi).</summary>
    public void Retry()
    {
        _shown = false;
        _progress("password", "");
        var page = new WebPage();
        page.Loaded += (_, ok) =>
        {
            if (!ok) { if (!_shown) Problem(L.T("web.offline")); return; }
            _shown = true;
            _slow.Stop();
        };
        Host.Child = page.View;
        var flow = new LoginFlow(page, _progress);
        flow.Finished += async () => { _done = true; await Task.Delay(800); Finished?.Invoke(); };   // đủ lâu để thấy trang xong
        flow.Start();
        _slow.Start();
    }

    private void Problem(string text)
    {
        Log.Warn($"Đăng nhập: {text}");
        _progress("error", text);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (!_done) _progress("cancel", "");
        Finished?.Invoke();
    }
}
