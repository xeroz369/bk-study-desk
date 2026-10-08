using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SoHocTap.Presentation.Practice.Pages;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>Trang chính Luyện tập. Chỉ nối model vào XAML và báo lên PracticeHost khi bấm; dữ liệu do PracticeHome dựng.</summary>
public partial class PracticeHomeView : UserControl
{
    public PracticeHomeView() => InitializeComponent();

    /// <summary>
    /// Người dùng chọn một việc: due, review, next, flagged, lesson:id, random:courseId, mix:courseId, author, retry,
    /// quiz:lessonId (quiz LMS), pack:file (gói: mở tab Môn học), remove:file (gỡ gói).
    /// </summary>
    public event Action<string>? Go;

    // Gỡ gói: bấm lần đầu đổi chữ nút, bấm lần nữa trong vài giây mới gỡ (như Nộp bài của thi thử).
    private Button? _armed;
    private readonly DispatcherTimer _disarm = new() { Interval = TimeSpan.FromSeconds(4) };

    internal PracticeHomeView(PracticeHomeModel m, IReadOnlyList<string> warnings) : this()
    {
        DataContext = m;
        MetaText.Text = L.F("practice.meta", m.Today.Due, m.Today.Review);
        ErrorCard.IsVisible = warnings.Count > 0;
        ErrorText.Text = L.F("practice.error.load", string.Join("; ", warnings.Take(3)));
        DueSub.Text = L.F("practice.today.dueSub", m.Today.Due);
        DueButton.IsEnabled = m.Today.Due > 0;
        ReviewSub.Text = L.F("practice.today.reviewSub", m.Today.Review);
        ReviewButton.IsEnabled = m.Today.Review > 0;
        NextSub.Text = m.Today.Next is { } e ? $"{e.CourseName}, {e.Title}" : L.T(m.Courses.Count == 0 ? "practice.today.nextEmpty" : "practice.today.nextNone");
        ExamsEmpty.IsVisible = m.Courses.Count == 0;
        NextButton.IsEnabled = m.Today.Next != null;
        FlaggedSub.Text = L.F("practice.today.flaggedSub", m.Flagged);
        FlaggedButton.IsEnabled = m.Flagged > 0;
        CoursesEmpty.IsVisible = m.Courses.Count == 0;
        QuizzesEmpty.IsVisible = m.Quizzes.Count == 0;
        ProgressGrid.IsVisible = m.Progress.Count > 0;
        ProgressEmpty.IsVisible = m.Progress.Count == 0;
        ResultsMeta.Text = L.F("practice.results.meta", m.Results.Count);
        ResultsGrid.IsVisible = m.Results.Count > 0;
        ResultsEmpty.IsVisible = m.Results.Count == 0;
        UpdateArchiveEmpty();
        // Hai cột khi đủ chỗ cho cả hai mức tối thiểu, không thì thẻ Thi thử xuống dưới (như trang Hôm nay).
        TodayColumns.SizeChanged += (_, _) => Layout(TodayColumns.Bounds.Width);
        _disarm.Tick += (_, _) => Disarm();
    }

    public void SelectTab(int i) => Tabs.SelectedIndex = Math.Clamp(i, 0, Tabs.ItemCount - 1);

    public int SelectedTab => Tabs.SelectedIndex;

    private void Layout(double width)
    {
        var two = width >= (double)this.FindResource("MainMinWidth")! + 24 + (double)this.FindResource("SideMinWidth")!;
        if (two == (Grid.GetColumn(ExamCard) == 2)) return;
        TodayColumns.ColumnDefinitions[0].MinWidth = two ? (double)this.FindResource("MainMinWidth")! : 0;
        TodayColumns.ColumnDefinitions[2].MinWidth = two ? (double)this.FindResource("SideMinWidth")! : 0;
        TodayColumns.ColumnDefinitions[0].Width = two ? new GridLength(2, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        TodayColumns.ColumnDefinitions[2].Width = two ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TodayColumns.RowDefinitions.Clear();
        if (!two)
        {
            TodayColumns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TodayColumns.RowDefinitions.Add(new RowDefinition(24, GridUnitType.Pixel));
            TodayColumns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        Grid.SetColumn(ExamCard, two ? 2 : 0);
        Grid.SetRow(ExamCard, two ? 0 : 2);
    }

    private void Disarm()
    {
        _disarm.Stop();
        if (_armed is { } b) b.Content = L.T("practice.quiz.remove");
        _armed = null;
    }

    private static string? TagOf(object? sender) => (sender as Control)?.Tag as string;

    private void OnDue(object? sender, RoutedEventArgs e) => Go?.Invoke("due");
    private void OnReview(object? sender, RoutedEventArgs e) => Go?.Invoke("review");
    private void OnNext(object? sender, RoutedEventArgs e) => Go?.Invoke("next");
    private void OnFlagged(object? sender, RoutedEventArgs e) => Go?.Invoke("flagged");
    private void OnPage(object? sender, RoutedEventArgs e) => Go?.Invoke("page:" + TagOf(sender));
    private void OnRetry(object? sender, RoutedEventArgs e) => Go?.Invoke("retry");
    private void OnAuthor(object? sender, RoutedEventArgs e) => Go?.Invoke("author");
    private void OnRandomExam(object? sender, RoutedEventArgs e) => Go?.Invoke("random:" + TagOf(sender));
    private void OnMix(object? sender, RoutedEventArgs e) => Go?.Invoke("mix:" + TagOf(sender));
    private void OnLessonTapped(object? sender, TappedEventArgs e) => Go?.Invoke("lesson:" + TagOf(sender));

    private void OnOpenQuiz(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not PracticeQuizRow r) return;
        if (r.LessonId.Length > 0) Go?.Invoke("quiz:" + r.LessonId);
        else SelectTab(1);
    }

    // ------------------------------------------------------------------ kho quiz LMS

    /// <summary>Người dùng đổi công tắc tự lưu (true, false) hay bấm Lưu ngay (null). Host gọi API rồi báo lại qua <see cref="ArchiveSays"/>.</summary>
    public event Action<bool?>? ArchiveRequested;

    private bool _loadingPref;

    /// <summary>Giá trị công tắc tự lưu đọc từ app; null là chưa đọc được (giữ bật như bản Svelte).</summary>
    public void SetAutoSave(bool? on)
    {
        _loadingPref = true;
        AutoSave.IsChecked = on ?? true;
        _loadingPref = false;
        SaveNowButton.IsEnabled = AutoSave.IsChecked == true;
        UpdateArchiveEmpty();
    }

    /// <summary>Câu báo trên thẻ kho quiz (đã bật, đang đồng bộ, lỗi).</summary>
    public void ArchiveSays(string text)
    {
        ArchiveNote.Text = text;
        ArchiveNote.IsVisible = text.Length > 0;
    }

    private void UpdateArchiveEmpty()
    {
        var empty = DataContext is PracticeHomeModel m && m.Archive.Count == 0;
        ArchiveEmpty.IsVisible = empty;
        ArchiveEmpty.Text = L.T(AutoSave.IsChecked == true ? "practice.archive.empty" : "practice.archive.emptyOff");
    }

    private void OnAutoSave(object? sender, RoutedEventArgs e)
    {
        if (_loadingPref) return;
        SaveNowButton.IsEnabled = AutoSave.IsChecked == true;
        UpdateArchiveEmpty();
        ArchiveRequested?.Invoke(AutoSave.IsChecked == true);
    }

    private void OnSaveNow(object? sender, RoutedEventArgs e) => ArchiveRequested?.Invoke(null);

    private void OnToneLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock t) return;
        t.Classes.Set("ok", t.Tag as string == "ok");
        t.Classes.Set("soon", t.Tag as string == "soon");
    }

    private void OnArchiveButtonLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PracticeArchiveRow r } b) b.Content = L.T(r.LessonId.Length > 0 ? "practice.archive.review" : "practice.archive.recall");
    }

    private void OnArchiveOpen(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not PracticeArchiveRow r) return;
        Go?.Invoke(r.LessonId.Length > 0 ? "quiz:" + r.LessonId : $"recall:{r.Code}|{r.Quiz}");
    }

    private void OnAuthorImport(object? sender, RoutedEventArgs e) => Go?.Invoke("author:1");

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (_armed != b)
        {
            Disarm();
            _armed = b;
            b.Content = L.T("practice.quiz.removeConfirm");
            _disarm.Start();
            return;
        }
        Disarm();
        Go?.Invoke("remove:" + TagOf(b));
    }
}
