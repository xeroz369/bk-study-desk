using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>Trang MyBK: nối MybkPresenter vào các tab. Bảng có nhóm (điểm theo kỳ, chương trình theo khối, giảng viên) dùng nhóm của DataGrid.</summary>
public partial class MybkView : UserControl, IFillPage
{
    private readonly MybkData? _m;

    public MybkView() => InitializeComponent();

    internal MybkView(AppState state) : this()
    {
        _m = state.Mybk;
        var hasData = _m is not null;
        Subtitle.Text = MybkPresenter.Subtitle(_m);
        StatsText.Text = string.Join(", ", MybkPresenter.Stats(_m).Where(s => s.Value != "-").Select(s => $"{s.Label} {s.Value}" + (s.Tip is { } t ? $" ({t})" : "")));
        StatsText.IsVisible = StatsText.Text.Length > 0;
        string Empty(string key) => hasData ? L.T(key) : "Chưa có dữ liệu MyBK. Đăng nhập để đồng bộ.";
        Show(Grades, GradesEmpty, Grouped(MybkPresenter.Grades(_m), nameof(MybkGradeRow.Group)), Empty("grades.gradesEmpty"));
        ProgramFilter.ItemsSource = MybkPresenter.Filters;
        ProgramFilter.SelectedIndex = 0;   // OnProgramFilter dựng bảng chương trình
        ProgramNote.Text = MybkPresenter.ProgramNote(_m);
        Show(Registered, RegisteredEmpty, MybkPresenter.Registered(_m), Empty("grades.regEmpty"));
        Show(Teachers, TeachersEmpty, Grouped(MybkPresenter.Teachers(_m), nameof(TeacherRow.Teacher)), Empty("grades.teachersEmpty"));
        Show(Fees, FeesEmpty, MybkPresenter.Fees(_m), Empty("grades.feesEmpty"));
        SocialTitle.Text = L.F("grades.socialTitle", Format.Score(_m?.SocialWork?.Days));
        Show(Social, SocialEmpty, MybkPresenter.Social(_m), Empty("grades.socialEmpty"));
        Show(Decisions, DecisionsEmpty, MybkPresenter.Decisions(_m), Empty("grades.decisionsEmpty"));
        ShowServices();
    }

    private static DataGridCollectionView Grouped<T>(IReadOnlyList<T> rows, string path)
    {
        var view = new DataGridCollectionView(rows);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(path));
        return view;
    }

    private static void Show(DataGrid grid, TextBlock empty, System.Collections.IEnumerable rows, string emptyText)
    {
        var any = rows.Cast<object>().Any();
        grid.ItemsSource = rows;
        grid.IsVisible = any;
        empty.Text = emptyText;
        empty.IsVisible = !any;
    }

    private void OnProgramFilter(object? sender, SelectionChangedEventArgs e)
    {
        if (ProgramFilter.SelectedItem is not SoHocTap.Presentation.ProgramFilter f) return;
        var rows = MybkPresenter.Program(_m, f);
        Show(Program, ProgramEmpty, Grouped(rows ?? [], nameof(ProgramRow.Group)), L.T("grades.programEmpty"));
    }

    private void OnServiceFilter(object? sender, TextChangedEventArgs e) => ShowServices();

    private void ShowServices()
    {
        var q = ServiceFilter.Text ?? "";
        Show(Services, ServicesEmpty, Grouped(MybkPresenter.Services(q), nameof(ServiceRow.Group)), L.T(q.Trim().Length == 0 ? "services.empty" : "services.noMatch"));
    }

    private async void OnServiceOpen(object? sender, TappedEventArgs e)
    {
        if (Services.SelectedItem is ServiceRow s && Uri.TryCreate(s.Url, UriKind.Absolute, out var u) && TopLevel.GetTopLevel(this) is { } top)
            await top.Launcher.LaunchUriAsync(u);
    }

    private async void OnOpenWeb(object? sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(Config.Str("sources.mybk.home"), UriKind.Absolute, out var u) && TopLevel.GetTopLevel(this) is { } top)
            await top.Launcher.LaunchUriAsync(u);
    }
}
