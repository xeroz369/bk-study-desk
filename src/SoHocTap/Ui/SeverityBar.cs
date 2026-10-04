using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoHocTap.Ui;

/// <summary>Mức độ của thông báo, theo InfoBar.Severity của WinUI.</summary>
public enum Severity { Informational, Success, Warning, Error }

/// <summary>
/// Biểu tượng mức độ giống InfoBar của WinUI (microsoft-ui-xaml, InfoBar_themeresources.xaml): vòng tròn F136 tô màu mức độ,
/// đè glyph F13F/F13E/F13C/F13D màu chữ đảo, cỡ 16. Màu lấy từ brush hệ thống của theme Fluent nên tự đúng ở sáng/tối/tương phản cao.
/// </summary>
public sealed class SeverityIcon : Grid
{
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(nameof(Severity), typeof(Severity), typeof(SeverityIcon),
        new PropertyMetadata(Severity.Informational, (d, _) => ((SeverityIcon)d).Apply()));

    private readonly TextBlock _back = Glyph("");
    private readonly TextBlock _front = Glyph("");

    public SeverityIcon()
    {
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_back);
        Children.Add(_front);
        _front.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorInverseBrush");
        Apply();
    }

    public Severity Severity { get => (Severity)GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }

    public double Size { set { _back.FontSize = _front.FontSize = value; } }

    private static TextBlock Glyph(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 16 };
        t.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        return t;
    }

    private void Apply()
    {
        _front.Text = Severity switch { Severity.Error => "", Severity.Warning => "", Severity.Success => "", _ => "" };
        _back.SetResourceReference(TextBlock.ForegroundProperty, IconBrush(Severity));
    }

    internal static string IconBrush(Severity s) => s switch
    {
        Severity.Error => "SystemFillColorCriticalBrush",
        Severity.Warning => "SystemFillColorCautionBrush",
        Severity.Success => "SystemFillColorSuccessBrush",
        _ => "SystemFillColorAttentionBrush",
    };

    internal static string BackgroundBrush(Severity s) => s switch
    {
        Severity.Error => "SystemFillColorCriticalBackgroundBrush",
        Severity.Warning => "SystemFillColorCautionBackgroundBrush",
        Severity.Success => "SystemFillColorSuccessBackgroundBrush",
        _ => "SystemFillColorAttentionBackgroundBrush",
    };
}

/// <summary>
/// Thanh báo nằm trong bố cục (không đè nội dung), làm theo InfoBar của WinUI: nền + biểu tượng theo mức độ, tiêu đề đậm vừa,
/// nội dung, nút hành động, nút đóng; cao tối thiểu 48. Hướng dẫn của Microsoft: Error = sự cố đã xảy ra, Warning = điều có thể
/// gây sự cố, Success = việc chạy nền đã xong, Informational = thông tin cần người dùng để ý; luôn có chữ, không chỉ màu/biểu tượng.
/// </summary>
public sealed class SeverityBar : Border
{
    private readonly SeverityIcon _icon = new() { Margin = new Thickness(0, 0, 12, 0) };
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _detailsToggle = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _copy = new() { Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _details = new()
    {
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        MaxHeight = 160,
        Margin = new Thickness(0, 8, 0, 0),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
    };
    private readonly StackPanel _detailsPanel = new() { Visibility = Visibility.Collapsed };
    private readonly Button _action = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _action2 = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _close = new()
    {
        Content = "",
        Padding = new Thickness(6, 4, 6, 4),
        Margin = new Thickness(8, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };
    private Action? _run, _run2;

    public SeverityBar()
    {
        Visibility = Visibility.Collapsed;
        MinHeight = 48;
        Padding = new Thickness(16, 8, 8, 8);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        SetResourceReference(BorderBrushProperty, "CardStrokeColorDefaultBrush");
        _title.SetResourceReference(TextBlock.FontSizeProperty, "AppFontSizeSection");
        _close.SetResourceReference(Control.FontFamilyProperty, "IconFont");
        AutomationProperties.SetName(_close, L.T("common.close"));
        _close.ToolTip = L.T("common.close");
        _close.Click += (_, _) => { Visibility = Visibility.Collapsed; Closed?.Invoke(); };
        _action.Click += (_, _) => _run?.Invoke();
        _action2.Click += (_, _) => _run2?.Invoke();
        _detailsToggle.Click += (_, _) => SetDetailsOpen(_detailsPanel.Visibility != Visibility.Visible);
        _copy.Content = L.T("common.copy");
        _copy.Click += (_, _) => { try { Clipboard.SetText(_details.Text); } catch (System.Runtime.InteropServices.COMException) { } };
        _detailsPanel.Children.Add(_details);
        _detailsPanel.Children.Add(_copy);

        var text = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_title);
        text.Children.Add(_message);
        var dock = new DockPanel();
        DockPanel.SetDock(_close, Dock.Right);
        DockPanel.SetDock(_action, Dock.Right);
        DockPanel.SetDock(_action2, Dock.Right);
        DockPanel.SetDock(_detailsToggle, Dock.Right);
        DockPanel.SetDock(_icon, Dock.Left);
        dock.Children.Add(_close);
        dock.Children.Add(_action);
        dock.Children.Add(_action2);
        dock.Children.Add(_detailsToggle);
        dock.Children.Add(_icon);
        dock.Children.Add(text);
        var root = new StackPanel();
        root.Children.Add(dock);
        root.Children.Add(_detailsPanel);
        Child = root;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>Người dùng bấm X.</summary>
    public event Action? Closed;

    public bool IsClosable { set => _close.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }

    /// <summary>
    /// Hiện thanh báo. <paramref name="details"/>: chữ kỹ thuật (mã lỗi, câu gốc của server) để sau nút Chi tiết, có nút Sao chép.
    /// Cùng nội dung thì không vẽ lại (tránh nhấp nháy, theo hướng dẫn của InfoBar).
    /// <paramref name="action2"/>: nút phụ đứng trước nút chính (ví dụ "Mở MyBK" cạnh "Thử lại").
    /// </summary>
    public void Show(Severity severity, string title, string message, string? action = null, Action? run = null, string? details = null,
        string? action2 = null, Action? run2 = null)
    {
        _run = run;
        _action.Content = action;
        _action.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        _run2 = run2;
        _action2.Content = action2;
        _action2.Visibility = action2 is null ? Visibility.Collapsed : Visibility.Visible;
        _detailsToggle.Visibility = string.IsNullOrEmpty(details) ? Visibility.Collapsed : Visibility.Visible;
        if (_details.Text != (details ?? "")) { _details.Text = details ?? ""; SetDetailsOpen(false); }
        if (Visibility == Visibility.Visible && _icon.Severity == severity && _title.Text == title && _message.Text == message) return;
        _icon.Severity = severity;
        _title.Text = title;
        _title.Visibility = title.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _message.Text = message;
        SetResourceReference(BackgroundProperty, SeverityIcon.BackgroundBrush(severity));
        AutomationProperties.SetName(this, $"{title} {message}".Trim());
        Visibility = Visibility.Visible;
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    private void SetDetailsOpen(bool open)
    {
        _detailsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        _detailsToggle.Content = L.T(open ? "common.hideDetails" : "common.details");
    }
}
