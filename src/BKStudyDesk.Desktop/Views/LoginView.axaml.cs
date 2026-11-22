using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Web;
using SoHocTap.Core;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

public partial class LoginView : UserControl
{
    private readonly Action<string, string> _progress = (_, _) => { };
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(20) };
    private bool _shown;
    private bool _done;

    public event Action? Finished;

    public LoginView() => InitializeComponent();

    internal LoginView(Action<string, string> progress) : this()
    {
        _progress = progress;
        _slow.Tick += (_, _) => { _slow.Stop(); if (!_shown) Problem(L.T("login.slow")); };
        AttachedToVisualTree += (_, _) => { if (Host.Child is null) Retry(); };
        DetachedFromVisualTree += (_, _) => _slow.Stop();
    }

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
        var flow = new LoginFlow(page, _progress, mybk: Settings.Sync.MybkEnabled);
        flow.Finished += () => { _done = true; Finished?.Invoke(); };
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
