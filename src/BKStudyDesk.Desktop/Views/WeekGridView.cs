using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Thời khóa biểu dạng lưới tuần (như 1.x, Ui/WeekGrid; giống Google Calendar, Outlook): cột là ngày, trục dọc là giờ, mỗi buổi một khối
/// màu theo môn. Chỉ ghép control có sẵn (Grid, Canvas, Border, TextBlock), không template riêng. Bố cục:
/// - luôn đủ 7 cột Thứ hai tới Chủ nhật và đủ 0:00 tới 24:00 (lớp buổi tối, sự kiện khuya); mở tuần thì cuộn tới buổi sớm nhất, hay tới
///   giờ hiện tại nếu giờ đó không lọt khung nhìn (WeekMath.ScrollAnchor);
/// - tỉ lệ dọc giãn để khoảng có buổi học vừa khung nhìn, trong khoảng WeekMinPxPerMin tới WeekMaxPxPerMin (Tokens.axaml);
/// - buổi trùng giờ chia bề ngang (WeekGridPresenter.Lanes); sự kiện tự thêm viền nét đứt và có nhãn;
/// - vạch "bây giờ" màu nhấn ở cột hôm nay, dời mỗi phút bằng DispatcherTimer chỉ chạy khi lưới đang hiện (đỡ tốn pin).
/// Giờ là giờ VN (giờ trường, như MyBK).
/// </summary>
public sealed class WeekGridView : UserControl
{
    private readonly Grid _header = new();
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly Grid _body = new();
    private readonly Grid _now = new() { VerticalAlignment = VerticalAlignment.Top, Height = 9, IsVisible = false, ZIndex = 10 };
    private readonly DispatcherTimer _clock = new(DispatcherPriority.Background);
    private IReadOnlyList<WeekBlock> _blocks = [];
    private DateTime _monday;
    private DateTime? _scrolledFor;   // tuần đã tự cuộn; dựng lại sau đồng bộ không kéo người dùng về
    private DateTime _shownToday;
    private double _px;
    private const int From = 0, To = 24 * 60;

    /// <summary>Bấm đúp một khối (sự kiện tự thêm: mở cửa sổ sửa).</summary>
    public Action<WeekBlock>? Open { get; set; }

    /// <summary>Dựng menu chuột phải của khối sự kiện tự thêm mỗi lần mở (buổi học của MyBK không có menu).</summary>
    public Action<MenuFlyout, WeekBlock>? FillMenu { get; set; }

    private static DateTime WallNow => DateTime.UtcNow + SoHocTap.Core.VnTime.Offset;

    public WeekGridView()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _scroll.Content = _body;
        Grid.SetRow(_scroll, 1);
        root.Children.Add(_header);
        root.Children.Add(_scroll);
        Content = root;
        var line = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Center };
        line.Bind(Border.BackgroundProperty, line.GetResourceObservable("Accent"));
        var dot = new Ellipse { Width = 9, Height = 9, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-5, 0, 0, 0) };
        dot.Bind(Shape.FillProperty, dot.GetResourceObservable("Accent"));
        _now.Children.Add(line);
        _now.Children.Add(dot);
        _scroll.SizeChanged += (_, e) => { if (Math.Abs(e.PreviousSize.Height - e.NewSize.Height) > 1) Layout(); };
        _clock.Tick += (_, _) => OnClock();
        // Đồng hồ chỉ chạy khi lưới đang hiện: chuyển trang, đổi sang Danh sách, xuống khay thì dừng.
        AttachedToVisualTree += (_, _) => OnClock();
        DetachedFromVisualTree += (_, _) => _clock.Stop();
        PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) { if (IsVisible) OnClock(); else _clock.Stop(); } };
    }

    // Token lấy từ Application: lưới được dựng trước khi gắn vào cửa sổ, lúc đó FindResource trên control chưa thấy Tokens.axaml.
    private static object? Token(string key) => Application.Current!.TryFindResource(key, out var v) ? v : null;
    private static double Res(string key) => Token(key) is double d ? d : throw new InvalidOperationException($"Thiếu token {key} trong Tokens.axaml");

    /// <summary>Hiện các buổi của tuần bắt đầu từ <paramref name="monday"/>.</summary>
    public void Show(DateTime monday, IReadOnlyList<WeekBlock> blocks)
    {
        _monday = monday;
        _blocks = [.. blocks.Where(b => b.Day is >= 2 and <= 8 && b.EndMin > b.StartMin)];
        BuildHeader();
        Layout();
    }

    private void OnClock()
    {
        _clock.Stop();
        if (!IsVisible || VisualRoot is null) return;
        if (WallNow.Date != _shownToday && _monday != default) { BuildHeader(); Layout(); }
        else PlaceNowLine();
        _clock.Interval = WeekMath.UntilNextMinute(WallNow);
        _clock.Start();
    }

    private void BuildHeader()
    {
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        _header.ColumnDefinitions.Add(new ColumnDefinition(Res("WeekGutter"), GridUnitType.Pixel));
        var today = _shownToday = WallNow.Date;
        for (var i = 0; i < 7; i++)
        {
            _header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var date = _monday.AddDays(i);
            var isToday = date == today;
            var cell = new StackPanel { Margin = new Thickness(0, 0, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
            cell.Children.Add(new TextBlock { Text = Format.MybkDays.GetValueOrDefault(i + 2) ?? "", Classes = { "meta" }, HorizontalAlignment = HorizontalAlignment.Center });
            var day = new TextBlock { Text = date.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture), Classes = { "card-title" }, HorizontalAlignment = HorizontalAlignment.Center };
            if (isToday) day.Bind(TextBlock.ForegroundProperty, day.GetResourceObservable("Link"));
            cell.Children.Add(day);
            Grid.SetColumn(cell, i + 1);
            _header.Children.Add(cell);
        }
        // Thanh cuộn dọc nổi đè lên nội dung: đầu cột lùi bằng bề rộng thanh cuộn để thẳng hàng với thân lưới.
        _header.Margin = new Thickness(0, 0, _scroll.Bounds.Width - _scroll.Viewport.Width, 0);
    }

    private void Layout()
    {
        _body.Children.Clear();
        _body.ColumnDefinitions.Clear();
        if (_monday == default) return;
        var pad = Res("WeekPad");
        var (spanFrom, spanTo) = WeekGridPresenter.Span(_blocks);
        var fit = (_scroll.Bounds.Height - 2 * pad) / Math.Max(60, spanTo - spanFrom);
        var px = _px = double.IsNaN(fit) || fit <= 0 ? Res("WeekMinPxPerMin") : Math.Clamp(fit, Res("WeekMinPxPerMin"), Res("WeekMaxPxPerMin"));
        _body.Height = (To - From) * px + 2 * pad;
        _body.ColumnDefinitions.Add(new ColumnDefinition(Res("WeekGutter"), GridUnitType.Pixel));
        for (var i = 0; i < 7; i++) _body.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        for (var m = From; m <= To; m += 60)   // đường kẻ giờ và nhãn giờ
        {
            var y = pad + (m - From) * px;
            _body.Children.Add(new TextBlock { Text = $"{m / 60}:00", Classes = { "sub" }, Margin = new Thickness(0, y - 7, 8, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, y, 0, 0) };
            rule.Bind(Border.BackgroundProperty, rule.GetResourceObservable("Line"));
            Grid.SetColumn(rule, 1);
            Grid.SetColumnSpan(rule, 7);
            _body.Children.Add(rule);
        }

        for (var i = 0; i < 7; i++)
        {
            var sep = new Border { BorderThickness = new Thickness(1, 0, 0, 0) };
            sep.Bind(Border.BorderBrushProperty, sep.GetResourceObservable("Line"));
            if (_monday.AddDays(i) == _shownToday) sep.Bind(Border.BackgroundProperty, sep.GetResourceObservable("GroupBg"));
            var canvas = new Canvas { ClipToBounds = true };
            Grid.SetColumn(sep, i + 1);
            Grid.SetColumn(canvas, i + 1);
            _body.Children.Add(sep);
            _body.Children.Add(canvas);
            var dayBlocks = _blocks.Where(b => b.Day == i + 2).ToList();
            var lanes = WeekGridPresenter.Lanes(dayBlocks);
            var items = dayBlocks.Select(b => (Block: b, View: Block(b, px))).ToList();
            foreach (var it in items) canvas.Children.Add(it.View);
            canvas.SizeChanged += (_, _) =>
            {
                foreach (var (b, view) in items)
                {
                    var (lane, count) = lanes[b];
                    var w = (canvas.Bounds.Width - 4) / count;
                    view.Width = Math.Max(0, w - 2);
                    Canvas.SetLeft(view, 2 + lane * w);
                    Canvas.SetTop(view, pad + (b.StartMin - From) * px + 1);
                }
            };
        }
        _body.Children.Add(_now);
        PlaceNowLine();
        if (_scrolledFor != _monday && _scroll.Bounds.Height > 0)
        {
            _scrolledFor = _monday;
            var visible = (int)((_scroll.Bounds.Height - 2 * pad) / px);
            var first = _blocks.Count == 0 ? (int?)null : _blocks.Min(b => b.StartMin);
            _scroll.Offset = new Vector(0, (WeekMath.ScrollAnchor(_monday, WallNow, first, visible) - From) * px);
        }
    }

    private void PlaceNowLine()
    {
        var now = WallNow;
        if (_monday == default || WeekMath.NowLine(_monday, now, From, To, _px, Res("WeekPad")) is not { } at) { _now.IsVisible = false; return; }
        Grid.SetColumn(_now, at.Column + 1);
        _now.Margin = new Thickness(0, at.Y - _now.Height / 2, 0, 0);
        ToolTip.SetTip(_now, L.F("calendar.nowTip", now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
        _now.IsVisible = true;
    }

    /// <summary>Một khối: buổi học là nền màu môn nhạt có viền trái đậm; sự kiện tự thêm thì viền nét đứt quanh khối.</summary>
    private Control Block(WeekBlock b, double px)
    {
        var colors = 0;
        while (Token("Course" + colors) is Color) colors++;
        var color = Token("Course" + WeekGridPresenter.ColorIndex(b.Key, colors)) is Color c ? c : Colors.Gray;
        var fill = new SolidColorBrush(Color.FromArgb(Token("WeekFillAlpha") is byte a ? a : throw new InvalidOperationException("Thiếu token WeekFillAlpha trong Tokens.axaml"), color.R, color.G, color.B));
        var panel = new StackPanel { Margin = new Thickness(6, 3, 4, 3) };
        // Xuống dòng ở khoảng trắng, không cắt giữa chữ khi cột hẹp (chữ dài hơn cột thì khối tự cắt, rê chuột xem đủ).
        panel.Children.Add(new TextBlock { Text = b.Title, Classes = { "name" }, TextWrapping = TextWrapping.WrapWithOverflow });
        panel.Children.Add(new TextBlock { Text = b.Tag is { } tag ? $"{tag}, {b.Detail}" : b.Detail, Classes = { "sub" } });
        var height = Math.Max(18, (b.EndMin - b.StartMin) * px - 2);
        Control view = b.Tag is null
            ? new Border { CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(3, 0, 0, 0), BorderBrush = new SolidColorBrush(color), Background = fill, Child = panel }
            : new Grid
            {
                Children =
                {
                    // Border không có nét đứt: viền sự kiện tự thêm vẽ bằng Rectangle có sẵn.
                    new Rectangle { RadiusX = 4, RadiusY = 4, Fill = fill, Stroke = new SolidColorBrush(color), StrokeThickness = 1.5, StrokeDashArray = new AvaloniaList<double> { 4, 2 } },
                    panel,
                },
            };
        view.Height = height;
        view.ClipToBounds = true;
        ToolTip.SetTip(view, b.Tip);
        AutomationProperties.SetName(view, b.Tip);
        if (b.CustomId is not null && FillMenu is { } fillMenu)
        {
            var menu = new MenuFlyout();
            menu.Opening += (_, _) => fillMenu(menu, b);
            view.ContextFlyout = menu;
        }
        if (Open is { } open) view.DoubleTapped += (_, e) => { e.Handled = true; open(b); };
        return view;
    }
}
