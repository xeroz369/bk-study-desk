using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Controls;

/// <summary>Bảng sự kiện nằm ở đâu: quyết định cột, nhóm và menu (Hôm nay, Lịch, trang Môn học).</summary>
internal enum TimelineView { Agenda, Upcoming, Subject }

/// <summary>Sự kiện tự thêm (issue #22) do trang Lịch xử lý: mở form sửa và menu Sửa/Xóa/Sao chép.</summary>
internal sealed record CustomEventHooks(Action<string> Edit, Func<string, IEnumerable<MenuEntry>> Menu);

/// <summary>
/// Bảng mốc thời gian (hạn nộp, quiz, thi, buổi học) dùng chung cho Hôm nay, Lịch và Môn học: cột, nhóm kiểu Explorer,
/// double-click, menu chuột phải và sao chép đều ở đây. Thêm hay đổi một cột thì sửa đúng file này. Bọc <see cref="DataView"/>
/// nên trạng thái đang tải, trống, lỗi và việc giữ vị trí cuộn khi dữ liệu mới về là của DataView.
/// </summary>
internal sealed class TimelineList : UserControl
{
    private readonly TimelineView _view;
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private readonly DataView _data = new() { KeyOf = o => ((TimelineItem)o).Id };
    private DataGridColumn? _groupColumn;

    public TimelineList(TimelineView view, AppHost host, MainWindow main)
    {
        (_view, _host, _main) = (view, host, main);
        Focusable = false;
        IsTabStop = false;
        Content = _data;
        AddColumns(_data.Grid);
        if (view != TimelineView.Subject) _data.Grid.GroupStyle.Add((GroupStyle)Application.Current.FindResource("ExplorerGroup"));
        Grids.Setup<TimelineItem>(_data.Grid, Open, view == TimelineView.Subject ? null : Menu, e => view == TimelineView.Upcoming && Custom is not null && e.CustomId is not null);
    }

    /// <summary>Chỉ trang Lịch đặt: dòng sự kiện tự thêm thì mở form sửa và có menu riêng thay cho "Mở".</summary>
    public CustomEventHooks? Custom { get; set; }

    /// <summary>Câu đang hiện thay cho bảng (cho test và trình đọc màn hình).</summary>
    public string StatusText => _data.StatusText;

    /// <summary>
    /// Hiện các mốc. Hôm nay nhóm theo ngày, Lịch nhóm theo khoảng thời gian ("Quá hạn" đứng đầu), cả hai sắp theo giờ;
    /// trang Môn học là danh sách phẳng. Danh sách trống thì DataView nói lý do (đang tải, lỗi, chưa đăng nhập) hoặc <paramref name="emptyText"/>.
    /// </summary>
    public void Show(IReadOnlyList<TimelineItem> items, string emptyText, AppState state, params (string Name, string Label)[] sources)
    {
        // Cột Nhóm chỉ hiện khi có mốc mang mã nhóm (lớp thí nghiệm), không chiếm chỗ bằng một cột trống.
        if (_groupColumn is not null) _groupColumn.Visibility = items.Any(i => i.ClassGroup.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
        if (_view == TimelineView.Subject) { _data.Show(items, emptyText, state, sources); return; }
        var grouped = Grids.Grouped(items, _view == TimelineView.Agenda ? nameof(TimelineItem.Day) : nameof(TimelineItem.Group), nameof(TimelineItem.GroupRank));
        grouped.SortDescriptions.Add(new SortDescription(nameof(TimelineItem.Time), ListSortDirection.Ascending));
        _data.Show(grouped, emptyText, state, sources);
    }

    private void Open(TimelineItem e)
    {
        if (Custom is { } c && e.CustomId is { } id && _view == TimelineView.Upcoming) c.Edit(id);
        else if (e.Url is { } u) _host.OpenWeb(u, e.Name);
        else if (_view == TimelineView.Agenda) _main.Go(Routes.Calendar);
    }

    private IEnumerable<MenuEntry> Menu(TimelineItem e)
    {
        if (_view == TimelineView.Upcoming && Custom is { } c && e.CustomId is { } id) return c.Menu(id);
        var menu = new List<MenuEntry>();
        if (_view == TimelineView.Agenda)
        {
            if (e.Url is { } u) menu.Add(new(L.T("common.openWeb"), () => _host.OpenWeb(u, e.Name)));
            if (e.Subject.Length > 0) menu.Add(new(L.T("home.subjectPage"), () => _main.Go(Routes.Subjects + "/" + e.Subject)));
        }
        // "Mở" trên web của Lịch đã có ở đầu menu do Grids.Setup thêm. Ở Hôm nay dòng sao chép chưa ghi chi tiết.
        var text = $"{e.Spoken}, {e.When}";
        menu.Add(new(L.T("common.copy"), () => Grids.Copy(text, _main), Separator: _view == TimelineView.Agenda));
        return menu;
    }

    /// <summary>Cột của từng view, theo thứ tự hiện. Thêm hay bớt một cột của một view thì sửa đúng một dòng ở đây.</summary>
    private void AddColumns(DataGrid g)
    {
        const string time = nameof(TimelineItem.Time);
        // Hôm nay chỉ ghi giờ vì nhóm đã là ngày; Lịch và Môn học gom nhiều ngày nên cột thời gian ghi cả ngày.
        // Tên là cột duy nhất co giãn, dòng phụ mờ ghi môn và chi tiết (trước là ba cột Loại, Chi tiết, Môn lặp ý nhau, cửa sổ rộng vẫn chật).
        // Trang Môn học đã biết môn nên dòng phụ chỉ ghi chi tiết.
        _groupColumn = Grids.Text(L.T("col.group"), nameof(TimelineItem.ClassGroup), 64);
        var name = _view == TimelineView.Subject
            ? Grids.TwoLine(L.T("col.name"), nameof(TimelineItem.Name), nameof(TimelineItem.Label), nameof(TimelineItem.Spoken))
            : Grids.TwoLine(L.T("col.name"), nameof(TimelineItem.Name), nameof(TimelineItem.Sub), nameof(TimelineItem.Spoken));
        DataGridColumn[] columns = _view switch
        {
            TimelineView.Agenda =>
            [
                Grids.Text(L.T("col.time"), nameof(TimelineItem.Hour), 56, sortPath: time),
                name, _groupColumn,
                Grids.Text(L.T("col.status"), nameof(TimelineItem.StateText), 90),
                LeftColumn(110, time),
            ],
            _ =>
            [
                Grids.Text(L.T("col.when"), nameof(TimelineItem.When), 130, sortPath: time),
                name, _groupColumn,
                Grids.Text(L.T("col.status"), nameof(TimelineItem.StateText), 90),
                LeftColumn(110, time),
            ],
        };
        foreach (var c in columns) g.Columns.Add(c);
    }

    /// <summary>
    /// Cột "Còn" (rộng hơn cột chữ thường để đủ chỗ cho biểu tượng mức gấp, "còn 19 phút" không bị cắt): giữ tiêu đề, độ rộng, căn phải và khóa sắp xếp như cột chữ phải, thêm mức gấp. Rất gấp và Quá hạn đỏ, đậm, có biểu tượng lỗi;
    /// Gấp vàng, có biểu tượng cảnh báo. Không chỉ dựa vào màu: biểu tượng, chữ đậm và HelpText (tên mức) cho trình đọc màn hình.
    /// </summary>
    private static DataGridTemplateColumn LeftColumn(double width, string sortPath)
    {
        // Grid hai cột (biểu tượng tự co, chữ chiếm phần còn lại) để chữ vẫn căn phải và cắt "..." được khi hẹp.
        var panel = new FrameworkElementFactory(typeof(Grid));
        panel.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        // Canh trên như các cột chữ: dòng tên có hai dòng thì chữ "còn..." vẫn thẳng hàng với tên và trạng thái.
        panel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        var iconCol = new FrameworkElementFactory(typeof(ColumnDefinition));
        iconCol.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        var textCol = new FrameworkElementFactory(typeof(ColumnDefinition));
        textCol.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        panel.AppendChild(iconCol);
        panel.AppendChild(textCol);

        var icon = new FrameworkElementFactory(typeof(SeverityIcon)) { Name = "icon" };
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        icon.SetValue(Grid.ColumnProperty, 0);
        icon.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        panel.AppendChild(icon);

        var text = new FrameworkElementFactory(typeof(TextBlock)) { Name = "text" };
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(TimelineItem.Left)));
        text.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(TimelineItem.Left)));
        text.SetValue(Grid.ColumnProperty, 1);
        text.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(text);

        var template = new DataTemplate { DataType = typeof(TimelineItem), VisualTree = panel };
        AddLevel(template, Urgency.Urgent, "urgency.urgent", Severity.Error, "SystemFillColorCriticalBrush", bold: true);
        AddLevel(template, Urgency.Overdue, "urgency.overdue", Severity.Error, "SystemFillColorCriticalBrush", bold: true);
        AddLevel(template, Urgency.Soon, "urgency.soon", Severity.Warning, "SystemFillColorCautionBrush", bold: false);
        return Grids.RightTemplate(L.T("col.left"), nameof(TimelineItem.Left), width, sortPath, template);
    }

    private static void AddLevel(DataTemplate template, Urgency level, string nameKey, Severity severity, string brush, bool bold)
    {
        var trigger = new DataTrigger { Binding = new Binding(nameof(TimelineItem.Urgency)), Value = level };
        trigger.Setters.Add(new Setter(AutomationProperties.HelpTextProperty, L.T(nameKey), "text"));
        trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "icon"));
        trigger.Setters.Add(new Setter(SeverityIcon.SeverityProperty, severity, "icon"));
        trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension(brush), "text"));
        if (bold) trigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold, "text"));
        template.Triggers.Add(trigger);
    }
}
