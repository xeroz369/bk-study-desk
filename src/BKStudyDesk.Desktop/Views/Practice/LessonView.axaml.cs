using Avalonia.Controls;
using Avalonia.Interactivity;
using BKStudyDesk.Desktop.Practice;
using SoHocTap.Presentation.Practice;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>Bài học: các mục kiến thức (đã lọc HTML) vẽ bằng ContentRenderer, nguồn, nút làm câu hỏi, bài trước và bài sau.</summary>
public partial class LessonView : UserControl
{
    public LessonView() => InitializeComponent();

    public event Action? BackRequested;
    public event Action? QuestionsRequested;

    /// <summary>Mở bài khác (bài trước, bài sau).</summary>
    public event Action<string>? LessonRequested;

    private readonly PracticeService _service = null!;
    private readonly Entry _entry = null!;
    private readonly (Entry? Prev, Entry? Next) _near;

    /// <param name="lesson">Null khi bài có trong mục lục mà chưa soạn.</param>
    internal LessonView(PracticeService service, Entry entry, Lesson? lesson) : this()
    {
        _service = service;
        _entry = entry;
        TitleText.Text = lesson?.Title ?? entry.Title;
        MetaText.Text = $"{entry.CourseName}, {entry.UnitTitle}, {service.Progress.Status(entry.Id).Label()}";
        var sources = lesson?.Sources ?? entry.Sources ?? [];
        foreach (var s in sources)
        {
            var b = new Button { Content = s.Pages is { Length: > 0 } p ? L.F("practice.lessonPage.sourcePages", s.File, p) : L.F("practice.lessonPage.source", s.File), Tag = s };
            b.Click += OnSource;
            Sources.Children.Add(b);
        }
        SourcePanel.IsVisible = sources.Count > 0;
        foreach (var s in lesson?.Sections ?? [])
        {
            var body = new StackPanel();
            if (!string.IsNullOrEmpty(s.Title))
                body.Children.Add(new Border { Classes = { "card-head" }, Child = new TextBlock { Classes = { "card-title" }, Text = s.Title } });
            body.Children.Add(new Border { Padding = new Avalonia.Thickness(16), Child = ContentRenderer.Render(s.Html) });
            Sections.Children.Add(new Border { Classes = { "card" }, Child = body });
        }
        var n = lesson?.Questions?.Count ?? 0;
        QuestionsButton.Content = L.F("practice.lessonPage.questions", n);
        QuestionsButton.IsVisible = n > 0;
        EmptyCard.IsVisible = n == 0;
        EmptyText.Text = L.T(lesson is null ? "practice.lessonPage.notWritten" : "practice.lessonPage.noQuestions");
        _near = PracticeActions.Neighbors(service.Study, entry.Id);
        PrevButton.IsVisible = _near.Prev != null;
        PrevButton.Content = _near.Prev is { } pv ? L.F("practice.lessonPage.prev", pv.Title) : "";
        NextButton.IsVisible = _near.Next != null;
        NextButton.Content = _near.Next is { } nx ? L.F("practice.lessonPage.next", nx.Title) : "";
    }

    private async void OnSource(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not SourceRef s) return;
        var page = s.Pages is { Length: > 0 } p && int.TryParse(new string([.. p.TakeWhile(char.IsAsciiDigit)]), out var n) ? n : 0;
        var ok = await _service.OpenSourceAsync(_entry.CourseName, s.File, page);
        SourceMissing.Text = L.F("practice.run.slideMissing", _entry.CourseName);
        SourceMissing.IsVisible = !ok;
    }

    private void OnBack(object? sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void OnQuestions(object? sender, RoutedEventArgs e) => QuestionsRequested?.Invoke();
    private void OnPrev(object? sender, RoutedEventArgs e) { if (_near.Prev is { } p) LessonRequested?.Invoke(p.Id); }
    private void OnNext(object? sender, RoutedEventArgs e) { if (_near.Next is { } n) LessonRequested?.Invoke(n.Id); }
}
