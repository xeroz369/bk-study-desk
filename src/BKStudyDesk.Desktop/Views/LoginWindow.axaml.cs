using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Cửa sổ đăng nhập: chạy một LoginFlow trên trang nhìn thấy được. Xong thì tự đóng; đóng giữa chừng thì báo "cancel".
/// Quá 20 giây chưa mở được trang nào thì báo trang trường chậm, có nút Thử lại (không để cửa sổ trống mà không nói gì).
/// </summary>
public partial class LoginWindow : Window
{
    private readonly Action<string, string> _progress = (_, _) => { };
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(20) };
    private bool _done;
    private bool _shown;   // trang trường đã hiện ít nhất một lần

    public LoginWindow() => InitializeComponent();

    internal LoginWindow(Action<string, string> progress) : this()
    {
        _progress = progress;
        Opened += (_, _) => Begin();
        Closed += (_, _) => { _slow.Stop(); if (!_done) _progress("cancel", ""); };
        _slow.Tick += (_, _) => { _slow.Stop(); if (!_shown) Problem(L.T("login.slow")); };
    }

    private void Begin()
    {
        Status.Text = L.T("login.opening");
        Retry.IsVisible = false;
        var page = new WebPage();
        page.Loaded += (_, ok) =>
        {
            if (!ok) { if (!_shown) Problem(L.T("web.offline")); return; }
            _shown = true;
            _slow.Stop();
            Status.Text = L.T("login.note");
        };
        Host.Child = page.View;
        var flow = new LoginFlow(page, (stage, message) =>
        {
            if (stage == "error") Problem(message);
            _progress(stage, message);
        });
        flow.Finished += async () => { _done = true; await Task.Delay(800); Close(); };
        flow.Start();
        _slow.Start();
    }

    private void Problem(string text)
    {
        Log.Warn($"Đăng nhập: {text}");
        Status.Text = text;
        Retry.IsVisible = true;
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        _shown = false;
        Begin();
    }
}
