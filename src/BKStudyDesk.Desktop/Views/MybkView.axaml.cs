using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>Trang MyBK: nối MybkPresenter vào các tab. Bảng có nhóm (điểm theo kỳ, chương trình theo khối, giảng viên) dùng nhóm của DataGrid.</summary>
public partial class MybkView : UserControl, IFillPage, ITabbedPage
{
    private readonly MybkData? _m;

    public MybkView() => InitializeComponent();

    public int Tab { get => Tabs.SelectedIndex; set => Tabs.SelectedIndex = Math.Clamp(value, 0, Tabs.ItemCount - 1); }

    /// <summary>
    /// Tab Tính thử GPA: mỗi môn đang học chưa có điểm một dòng (tên, tín chỉ, ô điểm dự kiến), kết quả GPA học kỳ và tích lũy cập nhật
    /// ngay khi sửa. Không lưu, không gửi đi đâu.
    /// </summary>
    private void ShowGpaPlanner()
    {
        GpaPanel.Children.Clear();
        GpaPanel.Children.Add(new TextBlock { Text = L.T("gpa.note"), Classes = { "meta" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var start = GpaPlanner.Base(_m);
        var courses = GpaPlanner.Pending(_m);
        if (courses.Count == 0) { GpaPanel.Children.Add(new TextBlock { Text = L.T("gpa.none"), Classes = { "empty" } }); return; }
        var result = new TextBlock { Classes = { "card-title" }, Margin = new Avalonia.Thickness(0, 8, 0, 0) };
        var boxes = new List<(GpaCourse Course, NumericUpDown Box)>();
        void Update()
        {
            var (term, all) = GpaPlanner.Compute(start, boxes.Select(b => (b.Course.Credits, b.Box.Value is { } v ? (double?)(double)v : null)));
            result.Text = L.F("gpa.result", term is { } t ? Format.Num(t) : "-", all is { } a ? Format.Num(a) : "-");
        }
        foreach (var c in courses)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,120") };
            row.Children.Add(new TextBlock { Text = $"{c.Name} ({c.Code})", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
            var cr = new TextBlock { Text = c.Credits is { } x ? L.F("gpa.credits", Format.Num(x)) : L.T("gpa.noCredits"), Classes = { "meta" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            Grid.SetColumn(cr, 1);
            row.Children.Add(cr);
            var box = new NumericUpDown { Minimum = 0, Maximum = 10, Increment = 0.5m, FormatString = "0.0", IsEnabled = c.Credits is not null };
            box.ValueChanged += (_, _) => Update();
            Grid.SetColumn(box, 2);
            row.Children.Add(box);
            GpaPanel.Children.Add(row);
            boxes.Add((c, box));
        }
        GpaPanel.Children.Add(result);
        if (start is { } s0) GpaPanel.Children.Add(new TextBlock { Text = L.F("gpa.base", Format.Num(s0.Gpa), Format.Num(s0.Credits)), Classes = { "meta" } });
        Update();
    }

    private readonly Action? _refresh;

    private void OnProgramRefresh(object? sender, RoutedEventArgs e)
    {
        ProgramRefresh.IsEnabled = false;
        ProgramNote.Text = L.T("grades.refreshing");
        _refresh?.Invoke();
    }

    internal MybkView(AppState state, Action? refresh = null) : this()
    {
        _refresh = refresh;
        ProgramRefresh.IsVisible = refresh is not null;
        _m = state.Mybk;
        ShowGpaPlanner();
        var hasData = _m is not null;
        Subtitle.Text = MybkPresenter.Subtitle(_m);
        var stats = MybkPresenter.Stats(_m).Where(s => s.Value != "-").ToList();
        Stats.ItemsSource = stats;
        Stats.IsVisible = stats.Count > 0;
        string Empty(string key) => L.T(hasData ? key : "mybk.noData");
        _grades = MybkPresenter.Grades(_m);
        _gradesEmpty = Empty("grades.gradesEmpty");
        GradesSearch.IsVisible = _grades.Count > 0;
        ShowGrades();
        ProgramFilter.ItemsSource = MybkPresenter.Filters;
        ProgramFilter.SelectedIndex = 0;   // OnProgramFilter dựng bảng chương trình
        ProgramNote.Text = MybkPresenter.ProgramNote(_m);
        Tables.Show(Registered, RegisteredEmpty, MybkPresenter.Registered(_m), Empty("grades.regEmpty"));
        Tables.Show(Teachers, TeachersEmpty, Tables.Grouped(MybkPresenter.Teachers(_m), nameof(TeacherRow.Teacher)), Empty("grades.teachersEmpty"));
        Tables.Show(Fees, FeesEmpty, MybkPresenter.Fees(_m), Empty("grades.feesEmpty"));
        SocialTitle.Text = L.F("grades.socialTitle", Format.Score(_m?.SocialWork?.Days));
        Tables.Show(Social, SocialEmpty, MybkPresenter.Social(_m), Empty("grades.socialEmpty"));
        Tables.Show(Decisions, DecisionsEmpty, MybkPresenter.Decisions(_m), Empty("grades.decisionsEmpty"));
        ShowServices();
    }

    private IReadOnlyList<MybkGradeRow> _grades = [];
    private string _gradesEmpty = "";

    // Ô tìm Bảng điểm (MybkPresenter.SearchGrades): lọc trên danh sách đã dựng, nhóm theo học kỳ như cũ.
    private void ShowGrades()
    {
        var q = GradesSearch.Text ?? "";
        Tables.Show(Grades, GradesEmpty, Tables.Grouped(MybkPresenter.SearchGrades(_grades, q), nameof(MybkGradeRow.Group)),
            string.IsNullOrWhiteSpace(q) ? _gradesEmpty : L.T("grades.searchNone"));
    }

    private void OnGradesSearch(object? sender, TextChangedEventArgs e) => ShowGrades();

    private void OnProgramFilter(object? sender, SelectionChangedEventArgs e)
    {
        if (ProgramFilter.SelectedItem is not SoHocTap.Presentation.ProgramFilter f) return;
        var rows = MybkPresenter.Program(_m, f);
        Tables.Show(Program, ProgramEmpty, Tables.Grouped(rows ?? [], nameof(ProgramRow.Group)), MybkPresenter.ProgramEmpty(_m));
    }

    private void OnServiceFilter(object? sender, TextChangedEventArgs e) => ShowServices();

    private void ShowServices()
    {
        var q = ServiceFilter.Text ?? "";
        Tables.Show(Services, ServicesEmpty, Tables.Grouped(MybkPresenter.Services(q), nameof(ServiceRow.Group)), L.T(q.Trim().Length == 0 ? "services.empty" : "services.noMatch"));
    }

    private async void OnServiceOpen(object? sender, TappedEventArgs e)
    {
        if (Services.SelectedItem is ServiceRow s) await Files.Links.OpenAsync(this, s.Url, s.Name);
    }

    private async void OnOpenWeb(object? sender, RoutedEventArgs e) => await Files.Links.OpenAsync(this, Config.Str("sources.mybk.home"), "MyBK");
}
