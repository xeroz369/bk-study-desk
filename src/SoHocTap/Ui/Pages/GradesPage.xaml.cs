using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using SoHocTap.Shell;
using SoHocTap.Ui.Controls;

namespace SoHocTap.Ui.Pages;

internal sealed record StatCard(string Label, string Value, string? Tip = null);
internal sealed record GradeLine(string Term, string TermName, string Code, string Name, string Parts, double? Credits, string Score, double SortScore,
    string Letter, string Result, bool Fail)
{
    /// <summary>Kỳ mới nhất trước; nhóm chuyển điểm/miễn (BL) xuống cuối.</summary>
    public string TermSort => Term == "BL" ? "0" : Term;
}
/// <summary>Một môn đã đăng ký, kèm giảng viên và lịch học lấy từ thời khóa biểu MyBK (cùng mã môn, hoặc phần thí nghiệm cùng tên).</summary>
internal sealed record RegisteredLine(string Code, string Name, string ClassGroup, string Round, string Result);

/// <summary>Một buổi dạy trong tuần: giảng viên, môn, nhóm lớp, thứ, giờ, phòng (thời khóa biểu MyBK của kỳ).</summary>
internal sealed record TeacherLine(string Teacher, string Name, string Code, string Group, string Day, int DaySort, string Time, string Room);

internal sealed record LmsGradeLine(string Book, string Name, double? Grade, double? Max, string Percent)
{
    public string GradeText => Format.Score(Grade);
    public string MaxText => Format.Score(Max);
    public double SortGrade => Grade ?? -1;
}
/// <summary>Một khoản học phí còn nợ: số tiền định dạng theo ngôn ngữ (Format.Money), sắp theo số.</summary>
internal sealed record FeeLine(string Content, long Remaining, string Due)
{
    public string RemainingText => Format.Money(Remaining);
}
internal sealed record FilterItem(string Label, CourseStatus[] Match)
{
    public override string ToString() => Label;
}

public partial class GradesPage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly MainWindow _main;
    private CurriculumView? _view;
    private static readonly FilterItem[] Filters =
    [
        new(L.T("grades.filter.all"), []), new(L.T("grades.filter.retake"), [CourseStatus.HocLai, CourseStatus.Rut]), new(L.T("grades.filter.studying"), [CourseStatus.DangHoc]),
        new(L.T("grades.filter.notTaken"), [CourseStatus.ChuaHoc]), new(L.T("grades.filter.passed"), [CourseStatus.Dat, CourseStatus.Mien]),
    ];

    internal GradesPage(AppHost host, MainWindow main)
    {
        InitializeComponent();
        _host = host;
        _main = main;

        var grades = Grades.Grid;
        grades.Columns.Add(Grids.Text(L.T("col.code"), nameof(GradeLine.Code), 80));
        grades.Columns.Add(Grids.Flex(L.T("col.subject"), nameof(GradeLine.Name), 1.6, 160));
        grades.Columns.Add(Grids.Flex(L.T("col.parts"), nameof(GradeLine.Parts), 1.4, 110));
        grades.Columns.Add(Grids.Right(L.T("col.credits"), nameof(GradeLine.Credits), 50));
        grades.Columns.Add(Grids.Right(L.T("col.score"), nameof(GradeLine.Score), 60, nameof(GradeLine.SortScore)));
        grades.Columns.Add(Grids.Text(L.T("col.letter"), nameof(GradeLine.Letter), 84));
        grades.Columns.Add(Grids.Text(L.T("col.result"), nameof(GradeLine.Result), 100));
        grades.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        grades.LoadingRow += (_, e) => e.Row.Foreground = e.Row.Item is GradeLine { Fail: true }
            ? (System.Windows.Media.Brush)FindResource("SystemFillColorCriticalBrush") : (System.Windows.Media.Brush)FindResource("TextFillColorPrimaryBrush");
        Grades.KeyOf = o => o is GradeLine g ? (g.Term, g.Code) : null;
        Grids.Setup<GradeLine>(grades, null, g => [new(L.T("grades.copyRow"), () => Grids.Copy($"{g.Code}\t{g.Name}\t{g.Credits}\t{g.Score}\t{g.Letter}\t{g.Result}", _main)),
            new(L.T("common.copyCode"), () => Grids.Copy(g.Code, _main))]);

        var program = ProgramGrid.Grid;
        program.Columns.Add(Grids.Text(L.T("col.code"), nameof(CourseView.Code), 80));
        program.Columns.Add(Grids.Text(L.T("col.subject"), nameof(CourseView.Name), star: true));
        program.Columns.Add(Grids.Right(L.T("col.credits"), nameof(CourseView.Credits), 50));
        program.Columns.Add(Grids.Right(L.T("col.score"), nameof(CourseView.Score), 60));
        program.Columns.Add(Grids.Text(L.T("col.letter"), nameof(CourseView.Letter), 84));
        program.Columns.Add(Grids.Flex(L.T("col.status"), nameof(CourseView.StatusText), 1, 120));
        program.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        program.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is CourseView { Status: CourseStatus.ChuaHoc or CourseStatus.LuaChon } ? 0.6 : 1;
        ProgramGrid.KeyOf = o => o is CourseView v ? (v.Course.Block, v.Code) : null;
        Grids.Setup<CourseView>(program, null, c => [new(L.T("common.copyCode"), () => Grids.Copy(c.Code, _main))]);
        Filter.ItemsSource = Filters;
        Filter.SelectedIndex = 0;

        var lms = LmsGrades.Grid;
        lms.Columns.Add(Grids.Text(L.T("col.item"), nameof(LmsGradeLine.Name), star: true));
        lms.Columns.Add(Grids.Right(L.T("col.score"), nameof(LmsGradeLine.GradeText), 70, nameof(LmsGradeLine.SortGrade)));
        lms.Columns.Add(Grids.Right(L.T("col.max"), nameof(LmsGradeLine.MaxText), 96));
        lms.Columns.Add(Grids.Right("%", nameof(LmsGradeLine.Percent), 90));
        lms.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        lms.LoadingRow += (_, e) => e.Row.Opacity = e.Row.Item is LmsGradeLine { Grade: null } ? 0.55 : 1;

        var registered = Registered.Grid;
        registered.Columns.Add(Grids.Text(L.T("col.code"), nameof(RegisteredLine.Code), 80));
        registered.Columns.Add(Grids.Text(L.T("col.subject"), nameof(RegisteredLine.Name), star: true));
        registered.Columns.Add(Grids.Flex(L.T("col.group"), nameof(RegisteredLine.ClassGroup), 1, 100));
        registered.Columns.Add(Grids.Text(L.T("col.round"), nameof(RegisteredLine.Round), 110));
        registered.Columns.Add(Grids.Flex(L.T("col.result"), nameof(RegisteredLine.Result), 1, 100));
        Grids.Setup<RegisteredLine>(registered, null, r => [new(L.T("grades.copyClass"), () => Grids.Copy(r.ClassGroup, _main)),
            new(L.T("common.copy"), () => Grids.Copy($"{r.Code} {r.Name}, {r.ClassGroup}", _main))]);

        var teachers = Teachers.Grid;
        teachers.Columns.Add(Grids.Text(L.T("col.subject"), nameof(TeacherLine.Name), star: true));
        teachers.Columns.Add(Grids.Text(L.T("col.code"), nameof(TeacherLine.Code), 80));
        teachers.Columns.Add(Grids.Text(L.T("col.group"), nameof(TeacherLine.Group), 90));
        teachers.Columns.Add(Grids.Text(L.T("col.day"), nameof(TeacherLine.Day), 110, sortPath: nameof(TeacherLine.DaySort)));
        teachers.Columns.Add(Grids.Text(L.T("col.time"), nameof(TeacherLine.Time), 110));
        teachers.Columns.Add(Grids.Flex(L.T("col.room"), nameof(TeacherLine.Room), 1, 80));
        teachers.GroupStyle.Add((GroupStyle)FindResource("ExplorerGroup"));
        Grids.Setup<TeacherLine>(teachers, null, t => [new(L.T("common.copy"), () => Grids.Copy($"{t.Teacher}: {t.Name} ({t.Code}, {t.Group}), {t.Day} {t.Time}, {t.Room}", _main))]);

        var fees = Fees.Grid;
        fees.Columns.Add(Grids.Text(L.T("col.content"), nameof(FeeLine.Content), star: true));
        fees.Columns.Add(Grids.Right(L.T("col.remaining"), nameof(FeeLine.RemainingText), 140, nameof(FeeLine.Remaining)));
        fees.Columns.Add(Grids.Flex(L.T("col.due"), nameof(FeeLine.Due), 1, 110));

        var social = Social.Grid;
        social.Columns.Add(Grids.Text(L.T("col.activity"), nameof(MybkActivity.Name), star: true));
        social.Columns.Add(Grids.Text(L.T("col.start"), nameof(MybkActivity.DateText), 110));
        social.Columns.Add(Grids.Right(L.T("col.daysConverted"), nameof(MybkActivity.DaysText), 120));

        var decisions = Decisions.Grid;
        decisions.Columns.Add(Grids.Text(L.T("col.date"), nameof(MybkDecision.Date), 100));
        decisions.Columns.Add(Grids.Flex(L.T("col.kind"), nameof(MybkDecision.Type), 0.8, 90));
        decisions.Columns.Add(Grids.Text(L.T("col.reason"), nameof(MybkDecision.Reason), star: true));
        decisions.Columns.Add(Grids.Flex(L.T("col.term"), nameof(MybkDecision.Term), 1, 100));
        decisions.Columns.Add(Grids.Flex(L.T("col.state"), nameof(MybkDecision.Status), 0.8, 90));
    }

    public string Title => L.T("nav.grades");
    public string Subtitle => _host.State.Mybk is { } m ? $"{m.Student.Mssv}, {m.Student.Class}, {m.Term.Name}" : L.T("grades.noData");

    public void Open(string arg) => Tabs.SelectedIndex = arg switch { "ctdt" => 1, "lms" => 2, "dang-ky" => 3, _ => 0 };

    private void OnFilter(object sender, SelectionChangedEventArgs e) => RefreshProgram();

    public void Refresh()
    {
        var m = _host.State.Mybk;
        _view = m is null ? null : CurriculumView.From(m);
        var real = (m?.GradeTerms ?? []).Where(t => t.Code != "BL").ToList();
        var latest = real.FirstOrDefault();
        var latestCredits = real.FirstOrDefault(t => double.TryParse(t.CreditsTerm.Text(), out var c) && c > 0);
        // MyBK trả GPA dạng chuỗi kiểu Mỹ ("8.12"): đọc ra số rồi in theo ngôn ngữ đang dùng (8,12 hay 8.12), giống mọi số khác trong app.
        static string Gpa(string? v) => double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0 ? Format.Num(d) : "-";
        Stats.ItemsSource = new List<StatCard>
        {
            new(L.T("grades.gpaAll"), Gpa(latest?.GpaAll), latest?.Name),
            new(L.T("grades.credits"), m?.Curriculum is { } c ? $"{c.CreditsDone:0}/{c.CreditsNeed:0}" : "-", _view?.Missing is { } miss ? L.F("grades.missing", miss) : null),
            new(L.T("grades.gpaTerm"), Gpa(latestCredits?.GpaTerm), latestCredits?.Name),
            new(L.T("grades.retake"), _view?.Retake.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"),
            new(L.T("grades.social"), m?.SocialWork?.Days is { } days ? Format.Score(days) : "-"),
        };

        // Bảng điểm: nhóm theo kỳ (mới nhất trước), ghép điểm thành phần chính thức.
        var termNames = (m?.GradeTerms ?? []).Where(t => t.Code is not null).DistinctBy(t => t.Code).ToDictionary(t => t.Code, t => t.Name);
        var comps = (m?.Components ?? []).GroupBy(c => (c.TermId, c.CourseId)).ToDictionary(g => g.Key, g => g.First());
        string Parts(MybkGrade g) => g.TermId is { } t && g.CourseId is { } c && comps.TryGetValue((t, c), out var x)
            ? string.Join(", ", x.Items.Where(i => i.Weight > 0).Select(i => $"{i.Name} {i.Weight:0}%: {i.Special ?? Format.Score(i.Score)}")) : "";
        string ResultText(MybkGrade g) => g.Result == 1 ? L.T("grades.pass") : g.Special ?? L.T(g.Result == 0 ? "grades.fail" : "grades.notCounted");
        var lines = (m?.Grades ?? []).Select(g => new GradeLine(g.Term, termNames.GetValueOrDefault(g.Term, g.Term == "BL" ? L.T("grades.reserved") : g.Term),
            g.Code, g.Name, Parts(g), g.Credits, g.Special ?? Format.Score(g.Score), g.Score ?? -1, g.Letter ?? "", ResultText(g), g.Result == 0)).ToList();
        var view = new ListCollectionView(lines);
        view.SortDescriptions.Add(new SortDescription(nameof(GradeLine.TermSort), ListSortDirection.Descending));
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GradeLine.TermName)));
        var s = _host.State;
        Grades.Show(view, L.T("grades.gradesEmpty"), s, Src.Mybk);

        RefreshProgram();
        Stale.Text = _view?.Stale == true ? L.F("grades.stale", m?.Curriculum?.Updated) : "";

        var lms = (_host.State.Lms?.Grades ?? []).SelectMany(b => b.Items.Select(i =>
            new LmsGradeLine(b.Subject + (b.Part is null ? "" : ", " + b.Part), i.Name, i.Grade, i.Max, i.Grade is null ? "" : i.Percent ?? ""))).ToList();
        LmsGrades.Show(Grids.Grouped(lms, nameof(LmsGradeLine.Book)), L.T("grades.lmsEmpty"), s, Src.Lms);

        RegTitle.Text = L.F("grades.regTitle", m?.Term.Name ?? L.T("grades.thisTerm"));
        Registered.Show((m?.Registered ?? []).Select(r => Registration(r, m!.Schedule)).ToList(), L.T("grades.regEmpty"), s, Src.Mybk);
        var teachers = TeacherLines(m);
        var tv = Grids.Grouped(teachers, nameof(TeacherLine.Teacher));
        tv.SortDescriptions.Add(new SortDescription(nameof(TeacherLine.Teacher), ListSortDirection.Ascending));
        tv.SortDescriptions.Add(new SortDescription(nameof(TeacherLine.DaySort), ListSortDirection.Ascending));
        Teachers.Show(tv, L.T("grades.teachersEmpty"), s, Src.Mybk);
        Fees.Show((m?.Fees ?? []).Select(f => new FeeLine(f.Content, f.Remaining, f.Due)).ToList(), L.T("grades.feesEmpty"), s, Src.Mybk);
        SocialTitle.Text = L.F("grades.socialTitle", Format.Score(m?.SocialWork?.Days));
        Social.Show(m?.SocialWork?.Activities ?? [], L.T("grades.socialEmpty"), s, Src.Mybk);
        Decisions.Show(m?.Decisions ?? [], L.T("grades.decisionsEmpty"), s, Src.Mybk);
    }

    /// <summary>
    /// Bảng giảng viên (đề xuất #6): mỗi buổi trong thời khóa biểu MyBK của kỳ là một dòng, nhóm theo giảng viên.
    /// Nhóm lớp lấy từ kết quả đăng ký (đợt cuối) nếu có, không thì từ thời khóa biểu. Buổi không có giờ cố định ghi riêng.
    /// </summary>
    private static List<TeacherLine> TeacherLines(MybkData? m)
    {
        if (m is null) return [];
        string GroupOf(MybkClass c) => m.Registered?.FirstOrDefault(r => r.Code == c.Code)?.ClassGroup is { Length: > 0 } g ? g : c.Group ?? "";
        return [.. m.Schedule.Select(c =>
        {
            var timed = c.Day is >= 2 and <= 8;
            return new TeacherLine(
                string.IsNullOrWhiteSpace(c.Teacher) ? L.T("grades.noTeacher") : c.Teacher!,
                c.Name, c.Code, GroupOf(c),
                timed ? Format.MybkDays.GetValueOrDefault(c.Day) ?? "" : L.T("grades.noFixedTime"), timed ? c.Day : 99,
                timed ? $"{c.Start}-{c.End}" : "", c.Room);
        }).DistinctBy(t => (t.Teacher, t.Code, t.Group, t.DaySort, t.Time, t.Room))];
    }

    private static RegisteredLine Registration(MybkRegistered r, List<MybkClass> schedule) => new(r.Code, r.Name, r.ClassGroup, r.Round, r.Result);

    private void RefreshProgram()
    {
        if (_view is null)
        {
            ProgramGrid.Show(null, L.T("grades.programEmpty"), _host.State, Src.Mybk);
            return;
        }
        var f = Filter.SelectedItem as FilterItem ?? Filters[0];
        var rows = _view.Blocks.SelectMany(b => b.Courses.Where(c => f.Match.Length == 0 || f.Match.Contains(c.Status)).Select(c => (b, c))).ToList();
        var titles = _view.Blocks.DistinctBy(b => b.Block.Id ?? "").ToDictionary(b => b.Block.Id ?? "", b => b.Title);
        var v = new ListCollectionView(rows.Select(x => x.c).ToList());
        v.GroupDescriptions.Add(new PropertyGroupDescription(null, new BlockTitle(titles)));
        ProgramGrid.Show(v, L.T("grades.programEmpty"));
    }

    /// <summary>Tên nhóm của một môn trong CTĐT = tiêu đề khối (tên · bắt buộc/tự chọn · tín chỉ).</summary>
    private sealed class BlockTitle(Dictionary<string, string> titles) : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => value is CourseView v ? titles.GetValueOrDefault(v.Course.Block ?? "", v.Course.Block ?? "") : "";
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
    }
}
