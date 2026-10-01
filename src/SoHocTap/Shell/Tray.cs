using System.Drawing;
using System.Windows.Forms;
using SoHocTap.Ui;

namespace SoHocTap.Shell;

/// <summary>
/// Tray icon (dùng NotifyIcon của WinForms vì WPF không có sẵn): hiện notification Windows cho hạn nộp và menu
/// Mở · Đồng bộ · Thoát. Lệnh default (double-click) được in đậm.
/// </summary>
internal sealed class Tray : IDisposable
{
    private readonly NotifyIcon _icon = new();
    private string _hash = "";

    public event Action? Open;
    public event Action? Sync;
    public event Action? Exit;
    /// <summary>Bấm vào thông báo thì mở app tới page này.</summary>
    public event Action<string>? Navigate;

    public Tray(string exePath)
    {
        var menu = new ContextMenuStrip();
        var open = menu.Items.Add(L.F("tray.open", AppInfo.Name), null, (_, _) => Open?.Invoke());
        open.Font = new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(L.T("tray.sync"), null, (_, _) => Sync?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("tray.exit"), null, (_, _) => Exit?.Invoke());
        _icon.Icon = Icon.ExtractAssociatedIcon(exePath);
        _icon.Text = AppInfo.Name;
        _icon.ContextMenuStrip = menu;
        _icon.Visible = true;
        _icon.DoubleClick += (_, _) => Open?.Invoke();
        _icon.BalloonTipClicked += (_, _) => { Open?.Invoke(); if (_hash.Length > 0) Navigate?.Invoke(_hash); };
    }

    public void Show(string title, string body, string page)
    {
        _hash = page;
        _icon.ShowBalloonTip(10_000, title, body.Length > 0 ? body : " ", ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
