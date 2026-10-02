using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

internal sealed record StatCard(string Label, string Value, string? Tip = null);
internal sealed record GradeLine(string Term, string TermName, string Code, string Name, string Parts, double? Credits, string Score, double SortScore,
    string Letter, string Result, bool Fail)
{
    /// <summary>Kỳ mới nhất trước; nhóm chuyển điểm/miễn (BL) xuống cuối.</summary>
    public string TermSort => Term == "BL" ? "0" : Term;
}
internal sealed record LmsGradeLine(string Book, string Name, double? Grade, double? Max, string Percent)
{
    public string GradeText => Format.Score(Grade);
    public string MaxText => Format.Score(Max);
    public double SortGrade => Grade ?? -1;
}
internal sealed record FilterItem(string Label, CourseStatus[] Match)
{
    public override string ToString() => Label;
}

public partial class GradesPage : UserControl, IPage
{
    private readonly AppHost _host;
    private CurriculumView? _view;
    private static readonly FilterItem[] Filters =
    [
        new(L.T("grades.filter.all"), []), new(L.T("grades.filter.retake"), [CourseStatus.HocLai, CourseStatus.Rut]), new(L.T("grades.filter.studying"), [CourseStatus.DangHoc]),
        new(L.T("grades.filter.notTaken"), [CourseStatus.ChuaHoc]), new(L.T("grades.filter.passed"), [CourseStatus.Dat, CourseStatus.Mien]),
    ];

    internal GradesPage(AppHost host)
    {
        InitializeComponent();
        _host = host;

        Grades.Columns.Add(Grids.Text(L.T("col.code"), nameof(GradeLine.Code), 80));
        Grades.Columns.Add(Grids.Text(L.T("col.subject"), nameof(GradeLine.Name), 260));
        Grades.Columns.Add(Grids.Text(L.T("col.parts"), nameof(GradeLine.Parts), star: true));
        Grades.Columns.Add(Grids.Right(L.T("col.credits"), nameof(GradeLine.Credits), 50));
        Grades.Columns.Add(Grids.Right(L.T("col.score"), nameof(GradeLine.Score), 60, nameof(GradeLine.SortScore)));
        Grades.Columns.Add(Grids.Text(L.T("col.letter"), nameof(GradeLine.Letter), 84));
        Grades.Columns.Add(Grids.Text(L.T("col.result"), nameof(GradeLine.Result), 100));
        Grades.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Grades.LoadingRow += (_, e) => e.Row.Foreground = e.Row.Item is GradeLine { Fail: true }
            ? (System.Windows.Media.Brush)FindResource("SystemFillColorCriticalBrush") : (System.Windows.Media.Brush)FindResource("TextFillColorPrimaryBrush");
        Grids.Setup<GradeLine>(Grades, null, g => [new(L.T("grades.copyRow"), () => Grids.Copy($"{g.Code}\t{g.Name}\t{g.Credits}\t{g.Score}\t{g.Letter}\t{g.Result}")),
            new(L.T("common.copyCode"), () => Grids.Copy(g.Code))]);

        ProgramGrid.Columns.Add(Grids.Text(L.T("col.code"), nameof(CourseView.Code), 80));
        ProgramGrid.Columns.Add(Grids.Text(L.T("col.subject"), nameof(CourseView.Name), star: true));
        ProgramGrid.Columns.Add(Grids.Right(L.T("col.credits"), nameof(CourseView.Credits), 50));
        ProgramGrid.Columns.Add(Grids.Right(L.T("col.score"), nameof(CourseView.Score), 60));
        ProgramGrid.Columns.Add(Grids.Text(L.T("col.letter"), nameof(CourseView.Letter), 84));
        ProgramGrid.Columns.Add(Grids.Text(L.T("col.status"), nameof(CourseView.StatusText), 220));
        ProgramGrid.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        ProgramGrid.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is CourseView { Status: CourseStatus.ChuaHoc or CourseStatus.LuaChon } ? 0.6 : 1;
        Grids.Setup<CourseView>(ProgramGrid, null, c => [new(L.T("common.copyCode"), () => Grids.Copy(c.Code))]);
        Filter.ItemsSource = Filters;
        Filter.SelectedIndex = 0;

        LmsGrades.Columns.Add(Grids.Text(L.T("col.item"), nameof(LmsGradeLine.Name), star: true));
        LmsGrades.Columns.Add(Grids.Right(L.T("col.score"), nameof(LmsGradeLine.GradeText), 70, nameof(LmsGradeLine.SortGrade)));
        LmsGrades.Columns.Add(Grids.Right(L.T("col.max"), nameof(LmsGradeLine.MaxText), 96));
        LmsGrades.Columns.Add(Grids.Right("%", nameof(LmsGradeLine.Percent), 90));
        LmsGrades.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        LmsGrades.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is LmsGradeLine { Grade: null } ? 0.55 : 1;

        Registered.Columns.Add(Grids.Text(L.T("col.code"), nameof(MybkRegistered.Code), 80));
        Registered.Columns.Add(Grids.Text(L.T("col.subject"), nameof(MybkRegistered.Name), star: true));
        Registered.Columns.Add(Grids.Text(L.T("col.group"), nameof(MybkRegistered.ClassGroup), 150));
        Registered.Columns.Add(Grids.Text(L.T("col.round"), nameof(MybkRegistered.Round), 120));
        Registered.Columns.Add(Grids.Text(L.T("col.result"), nameof(MybkRegistered.Result), 160));
        Grids.Setup<MybkRegistered>(Registered, null, r => [new(L.T("grades.copyClass"), () => Grids.Copy(r.ClassGroup))]);

        Fees.Columns.Add(Grids.Text(L.T("col.content"), nameof(MybkFee.Content), star: true));
        Fees.Columns.Add(Grids.Right(L.T("col.remaining"), nameof(MybkFee.Remaining), 120));
        Fees.Columns.Add(Grids.Text(L.T("col.due"), nameof(MybkFee.Due), 150));

        Social.Columns.Add(Grids.Text(L.T("col.activity"), nameof(MybkActivity.Name), star: true));
        Social.Columns.Add(Grids.Text(L.T("col.start"), nameof(MybkActivity.DateText), 110));
        Social.Columns.Add(Grids.Right(L.T("col.daysConverted"), nameof(MybkActivity.DaysText), 120));

        Decisions.Columns.Add(Grids.Text(L.T("col.date"), nameof(MybkDecision.Date), 100));
        Decisions.Columns.Add(Grids.Text(L.T("col.kind"), nameof(MybkDecision.Type), 120));
        Decisions.Columns.Add(Grids.Text(L.T("col.reason"), nameof(MybkDecision.Reason), star: true));
        Decisions.Columns.Add(Grids.Text(L.T("col.term"), nameof(MybkDecision.Term), 180));
        Decisions.Columns.Add(Grids.Text(L.T("col.state"), nameof(MybkDecision.Status), 130));
    }

    public string Title => L.T("nav.grades");
    public string Subtitle => _host.State.Mybk is { } m ? $"{m.Student.Mssv} · {m.Student.Class} · {m.Term.Name}" : L.T("grades.noData");

    public void Open(string arg) => Tabs.SelectedIndex = arg switch { "ctdt" => 1, "lms" => 2, "dang-ky" => 3, _ => 0 };

    private void OnFilter(object sender, SelectionChangedEventArgs e) => RefreshProgram();

    public void Refresh()
    {
        var m = _host.State.Mybk;
        _view = m is null ? null : CurriculumView.From(m);
        var real = (m?.GradeTerms ?? []).Where(t => t.Code != "BL").ToList();
        var latest = real.FirstOrDefault();
        var latestCredits = real.FirstOrDefault(t => double.TryParse(t.CreditsTerm.Text(), out var c) && c > 0);
        static string Gpa(string? v) => double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0 ? v! : "—";
        Stats.ItemsSource = new List<StatCard>
        {
            new(L.T("grades.gpaAll"), Gpa(latest?.GpaAll), latest?.Name),
            new(L.T("grades.credits"), m?.Curriculum is { } c ? $"{c.CreditsDone:0}/{c.CreditsNeed:0}" : "—", _view?.Missing is { } miss ? L.F("grades.missing", miss) : null),
            new(L.T("grades.gpaTerm"), Gpa(latestCredits?.GpaTerm), latestCredits?.Name),
            new(L.T("grades.retake"), _view?.Retake.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"),
            new(L.T("grades.social"), m?.SocialWork?.Days is { } days ? Format.Score(days) : "—"),
        };

        // Bảng điểm: nhóm theo kỳ (mới nhất trước), ghép điểm thành phần chính thức.
        var termNames = (m?.GradeTerms ?? []).Where(t => t.Code is not null).DistinctBy(t => t.Code).ToDictionary(t => t.Code, t => t.Name);
        var comps = (m?.Components ?? []).GroupBy(c => (c.TermId, c.CourseId)).ToDictionary(g => g.Key, g => g.First());
        string Parts(MybkGrade g) => g.TermId is { } t && g.CourseId is { } c && comps.TryGetValue((t, c), out var x)
            ? string.Join(" · ", x.Items.Where(i => i.Weight > 0).Select(i => $"{i.Name} {i.Weight:0}%: {i.Special ?? Format.Score(i.Score)}")) : "";
        string ResultText(MybkGrade g) => g.Result == 1 ? L.T("grades.pass") : g.Special ?? L.T(g.Result == 0 ? "grades.fail" : "grades.notCounted");
        var lines = (m?.Grades ?? []).Select(g => new GradeLine(g.Term, termNames.GetValueOrDefault(g.Term, g.Term == "BL" ? L.T("grades.reserved") : g.Term),
            g.Code, g.Name, Parts(g), g.Credits, g.Special ?? Format.Score(g.Score), g.Score ?? -1, g.Letter ?? "", ResultText(g), g.Result == 0)).ToList();
        var view = new ListCollectionView(lines);
        view.SortDescriptions.Add(new SortDescription(nameof(GradeLine.TermSort), ListSortDirection.Descending));
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GradeLine.TermName)));
        Grades.ItemsSource = view;
        Show(Grades, GradesEmpty, lines.Count, _host.State.NoDataReason("mybk", "MyBK") ?? L.T("grades.gradesEmpty"));

        RefreshProgram();
        Stale.Text = _view?.Stale == true ? L.F("grades.stale", m?.Curriculum?.Updated) : "";

        var lms = (_host.State.Lms?.Grades ?? []).SelectMany(b => b.Items.Select(i =>
            new LmsGradeLine(b.Subject + (b.Part is null ? "" : " · " + b.Part), i.Name, i.Grade, i.Max, i.Grade is null ? "" : i.Percent ?? ""))).ToList();
        LmsGrades.ItemsSource = Grids.Grouped(lms, nameof(LmsGradeLine.Book));
        Show(LmsGrades, LmsEmpty, lms.Count, _host.State.NoDataReason("lms", "LMS") ?? L.T("grades.lmsEmpty"));

        RegTitle.Text = L.F("grades.regTitle", m?.Term.Name ?? L.T("grades.thisTerm"));
        Registered.ItemsSource = m?.Registered ?? [];
        Show(Registered, RegisteredEmpty, m?.Registered?.Count ?? 0, _host.State.NoDataReason("mybk", "MyBK") ?? L.T("grades.regEmpty"));
        Fees.ItemsSource = m?.Fees ?? [];
        FeesEmpty.Visibility = (m?.Fees?.Count ?? 0) == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        Fees.Visibility = FeesEmpty.Visibility == System.Windows.Visibility.Visible ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        SocialTitle.Text = L.F("grades.socialTitle", Format.Score(m?.SocialWork?.Days));
        Social.ItemsSource = m?.SocialWork?.Activities ?? [];
        Show(Social, SocialEmpty, m?.SocialWork?.Activities?.Count ?? 0, _host.State.NoDataReason("mybk", "MyBK") ?? L.T("grades.socialEmpty"));
        Decisions.ItemsSource = m?.Decisions ?? [];
        Show(Decisions, DecisionsEmpty, m?.Decisions?.Count ?? 0, _host.State.NoDataReason("mybk", "MyBK") ?? L.T("grades.decisionsEmpty"));
    }

    /// <summary>
    /// Bảng trống thì ẩn bảng, hiện câu nói rõ vì sao (đang đồng bộ, lỗi, hay thật sự không có gì).
    /// DataGrid trống trong theme Fluent co cột về gần 0 và không có chữ ở header; hiện lại thì Grids.RestoreWidths đặt lại độ rộng.
    /// </summary>
    private static void Show(DataGrid grid, TextBlock empty, int count, string reason)
    {
        empty.Text = reason;
        empty.Visibility = count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        grid.Visibility = count == 0 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    private void RefreshProgram()
    {
        if (_view is null)
        {
            ProgramGrid.ItemsSource = null;
            Show(ProgramGrid, ProgramEmpty, 0, _host.State.NoDataReason("mybk", "MyBK") ?? L.T("grades.programEmpty"));
            return;
        }
        var f = Filter.SelectedItem as FilterItem ?? Filters[0];
        var rows = _view.Blocks.SelectMany(b => b.Courses.Where(c => f.Match.Length == 0 || f.Match.Contains(c.Status)).Select(c => (b, c))).ToList();
        var titles = _view.Blocks.DistinctBy(b => b.Block.Id ?? "").ToDictionary(b => b.Block.Id ?? "", b => b.Title);
        var v = new ListCollectionView(rows.Select(x => x.c).ToList());
        v.GroupDescriptions.Add(new PropertyGroupDescription(null, new BlockTitle(titles)));
        ProgramGrid.ItemsSource = v;
        Show(ProgramGrid, ProgramEmpty, rows.Count, L.T("grades.programEmpty"));
    }

    /// <summary>Tên nhóm của một môn trong CTĐT = tiêu đề khối (tên · bắt buộc/tự chọn · tín chỉ).</summary>
    private sealed class BlockTitle(Dictionary<string, string> titles) : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => value is CourseView v ? titles.GetValueOrDefault(v.Course.Block ?? "", v.Course.Block ?? "") : "";
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }
}
