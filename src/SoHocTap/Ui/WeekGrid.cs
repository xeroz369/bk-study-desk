using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoHocTap.Ui;

/// <summary>Một buổi học đặt lên lưới tuần.</summary>
internal sealed record WeekBlock(int Day, int StartMin, int EndMin, string Title, string Detail, string Tip, string Key);

/// <summary>
/// Thời khóa biểu dạng lưới tuần (giống Google Calendar, Outlook): cột là ngày, trục dọc là giờ, mỗi buổi là một khối màu theo môn.
/// Tự canh bố cục:
/// - luôn đủ 7 cột Thứ hai tới Chủ nhật, khớp nhãn tuần (ẩn T7, CN theo từng tuần làm cột nhảy qua lại khi có buổi học bù);
/// - khung giờ từ buổi sớm nhất tới buổi muộn nhất (làm tròn theo giờ);
/// - chiều cao giãn cho vừa vùng hiển thị, không thấp hơn mức đọc được (thấp hơn thì cuộn);
/// - buổi trùng giờ trong cùng ngày chia đôi chiều ngang.
/// Vẽ bằng Grid/Canvas của WPF, không thư viện ngoài.
/// </summary>
internal sealed class WeekGrid : Grid
{
    private const double Gutter = 52;           // cột giờ bên trái
    private const double MinPxPerMin = 0.7;     // 1 giờ ≥ 42 px: buổi 50 phút cao 35 px, vừa hai dòng chữ
    private const double Pad = 10;              // lề trên/dưới để nhãn giờ đầu, cuối không bị cắt
    private const double MaxPxPerMin = 2.0;
    // Thanh cuộn Fluent nổi đè lên nội dung: chừa lề phải bằng bề rộng thanh cuộn để không che cột Chủ nhật.
    private static readonly Thickness ScrollGap = new(0, 0, SystemParameters.VerticalScrollBarWidth, 0);
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x00, 0x78, 0xD4), Color.FromRgb(0x10, 0x7C, 0x10), Color.FromRgb(0xC2, 0x39, 0xB3), Color.FromRgb(0xCA, 0x50, 0x10),
        Color.FromRgb(0x00, 0x99, 0xBC), Color.FromRgb(0x88, 0x17, 0x98), Color.FromRgb(0x98, 0x6F, 0x0B), Color.FromRgb(0xE7, 0x48, 0x56),
    ];

    private readonly Grid _header = new();
    private readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Grid _body = new();
    private List<WeekBlock> _blocks = [];
    private DateTime _monday;
    private int _from, _to;
    private int[] _days = [];

    public Action<WeekBlock>? Copy { get; set; }

    public WeekGrid()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        SetRow(_scroll, 1);
        _scroll.Content = _body;
        _body.Margin = ScrollGap;
        _header.Margin = ScrollGap;
        Children.Add(_header);
        Children.Add(_scroll);
        _scroll.SizeChanged += (_, e) => { if (Math.Abs(e.PreviousSize.Height - e.NewSize.Height) > 1) Layout(); };
    }

    /// <summary>Hiện các buổi của tuần bắt đầu từ <paramref name="monday"/>. Day theo MyBK: 2 = Thứ hai … 8 = Chủ nhật.</summary>
    public void Show(DateTime monday, IEnumerable<WeekBlock> blocks)
    {
        _monday = monday;
        _blocks = blocks.Where(b => b.Day is >= 2 and <= 8 && b.EndMin > b.StartMin).ToList();
        _days = [.. Enumerable.Range(2, 7)];
        _from = _blocks.Count == 0 ? 7 * 60 : _blocks.Min(b => b.StartMin) / 60 * 60;
        _to = _blocks.Count == 0 ? 17 * 60 : (_blocks.Max(b => b.EndMin) + 59) / 60 * 60;
        BuildHeader();
        Layout();
    }

    private void BuildHeader()
    {
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Gutter) });
        var today = DateTime.Today;
        for (var i = 0; i < _days.Length; i++)
        {
            _header.ColumnDefinitions.Add(new ColumnDefinition());
            var date = _monday.AddDays(_days[i] - 2);
            var isToday = date == today;
            var cell = new StackPanel { Margin = new Thickness(0, 0, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };
            var name = new TextBlock { Text = Format.MybkDays.GetValueOrDefault(_days[i]) ?? "", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12 };
            name.SetResourceReference(TextBlock.ForegroundProperty, isToday ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            var day = new TextBlock { Text = date.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 16, FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal };
            day.SetResourceReference(TextBlock.ForegroundProperty, isToday ? "AccentTextFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");
            cell.Children.Add(name);
            cell.Children.Add(day);
            SetColumn(cell, i + 1);
            _header.Children.Add(cell);
        }
    }

    private void Layout()
    {
        _body.Children.Clear();
        _body.ColumnDefinitions.Clear();
        if (_days.Length == 0) return;
        var minutes = _to - _from;
        var fit = (_scroll.ActualHeight - 2 * Pad) / minutes;
        var px = double.IsNaN(fit) || fit <= 0 ? MinPxPerMin : Math.Clamp(fit, MinPxPerMin, MaxPxPerMin);
        _body.Height = minutes * px + 2 * Pad;

        _body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Gutter) });
        foreach (var _ in _days) _body.ColumnDefinitions.Add(new ColumnDefinition());

        // Đường kẻ giờ và nhãn giờ.
        for (var m = _from; m <= _to; m += 60)
        {
            var y = Pad + (m - _from) * px;
            var label = new TextBlock { Text = $"{m / 60}:00", FontSize = 11, Margin = new Thickness(0, y - 7, 8, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            _body.Children.Add(label);
            var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, y, 0, 0) };
            line.SetResourceReference(Border.BackgroundProperty, "DividerStrokeColorDefaultBrush");
            SetColumn(line, 1);
            SetColumnSpan(line, _days.Length);
            _body.Children.Add(line);
        }

        var today = DateTime.Today;
        for (var i = 0; i < _days.Length; i++)
        {
            var canvas = new Canvas { ClipToBounds = true };
            var sep = new Border { BorderThickness = new Thickness(1, 0, 0, 0) };
            sep.SetResourceReference(Border.BorderBrushProperty, "DividerStrokeColorDefaultBrush");
            if (_monday.AddDays(_days[i] - 2) == today) sep.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
            SetColumn(sep, i + 1);
            SetColumn(canvas, i + 1);
            _body.Children.Add(sep);
            _body.Children.Add(canvas);
            var dayBlocks = _blocks.Where(b => b.Day == _days[i]).OrderBy(b => b.StartMin).ThenByDescending(b => b.EndMin).ToList();
            var lanes = Lanes(dayBlocks);
            var items = dayBlocks.Select(b => (Block: b, View: Block(b, px))).ToList();
            foreach (var it in items) canvas.Children.Add(it.View);
            canvas.SizeChanged += (_, _) =>
            {
                foreach (var (b, view) in items)
                {
                    var (lane, count) = lanes[b];
                    var w = (canvas.ActualWidth - 4) / count;
                    view.Width = Math.Max(0, w - 2);
                    Canvas.SetLeft(view, 2 + lane * w);
                    Canvas.SetTop(view, Pad + (b.StartMin - _from) * px + 1);
                }
            };
        }
    }

    /// <summary>Chia làn cho các buổi trùng giờ trong một ngày: mỗi cụm buổi chồng nhau dùng chung số làn.</summary>
    private static Dictionary<WeekBlock, (int Lane, int Count)> Lanes(List<WeekBlock> blocks)
    {
        var result = new Dictionary<WeekBlock, (int, int)>();
        var cluster = new List<(WeekBlock B, int Lane)>();
        var laneEnds = new List<int>();
        var clusterEnd = -1;
        void Flush()
        {
            foreach (var (b, l) in cluster) result[b] = (l, laneEnds.Count);
            cluster.Clear();
            laneEnds.Clear();
        }
        foreach (var b in blocks)
        {
            if (b.StartMin >= clusterEnd) Flush();
            var lane = laneEnds.FindIndex(end => end <= b.StartMin);
            if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(b.EndMin); } else laneEnds[lane] = b.EndMin;
            cluster.Add((b, lane));
            clusterEnd = Math.Max(clusterEnd, b.EndMin);
        }
        Flush();
        return result;
    }

    private Border Block(WeekBlock b, double px)
    {
        var color = Palette[(int)((uint)StableHash(b.Key) % (uint)Palette.Length)];
        var title = new TextBlock { Text = b.Title, FontWeight = FontWeights.SemiBold, FontSize = 12, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var detail = new TextBlock { Text = b.Detail, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var panel = new StackPanel();
        panel.Children.Add(title);
        panel.Children.Add(detail);
        var view = new Border
        {
            Height = Math.Max(18, (b.EndMin - b.StartMin) * px - 2),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = new SolidColorBrush(color),
            Background = new SolidColorBrush(Color.FromArgb(0x38, color.R, color.G, color.B)),
            Padding = new Thickness(6, 3, 4, 3),
            Child = panel,
            ToolTip = b.Tip,
            ClipToBounds = true,
        };
        System.Windows.Automation.AutomationProperties.SetName(view, b.Tip);
        if (Copy is { } copy) view.ContextMenu = Grids.Build([new(L.T("common.copy"), () => copy(b))]);
        return view;
    }

    /// <summary>Hash ổn định giữa các lần chạy (string.GetHashCode đổi theo process) để mỗi môn giữ một màu.</summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in s) h = h * 31 + c;
            return h;
        }
    }
}
