using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SoHocTap.Presentation.Practice;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>
/// Thi thử: hiện từng câu, lưới số câu, đồng hồ theo hạn chót (hết giờ tự nộp), nộp bấm hai lần, xem lại sau khi nộp.
/// Chấm và ghi kết quả ở <see cref="ExamSession"/>; view giữ câu đang xem.
/// </summary>
public partial class ExamView : UserControl
{
    private const string Letters = "ABCDEFGH";

    public ExamView() => InitializeComponent();

    public event Action? BackRequested;

    /// <summary>Mở tab Kết quả của trang chính.</summary>
    public event Action? HistoryRequested;

    /// <summary>Đã nộp (để host ghi kết quả ngay).</summary>
    public event Action? Finished;

    private readonly ExamSession _session = null!;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _disarm = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly List<ToggleButton> _choices = [];
    private int _index;
    private bool _armed;
    private bool _showing;

    internal ExamView(ExamSession session) : this()
    {
        _session = session;
        var sc = session.Scoring;
        TitleText.Text = session.Exam.Title;
        MetaText.Text = L.F("practice.exam.meta", session.Exam.Minutes, session.Shown.Count,
            sc.Right.ToString("0.###", L.Culture), sc.Wrong.ToString("0.###", L.Culture));
        var any = session.Shown.Count > 0;
        EmptyCard.IsVisible = !any;
        Columns.IsVisible = any;
        SubmitButton.IsEnabled = any;
        for (var i = 0; i < session.Shown.Count; i++)
        {
            var k = i;
            var b = new Button { Classes = { "qnum" }, Content = (i + 1).ToString(L.Culture), Margin = new Avalonia.Thickness(4) };
            b.Click += (_, _) => Show(k);
            Numbers.Children.Add(b);
        }
        _tick.Tick += (_, _) => Tick();
        _disarm.Tick += (_, _) => Disarm();
        Columns.SizeChanged += (_, _) => Layout(Columns.Bounds.Width);
        // Đồng hồ chỉ chạy khi màn đang hiện; rời màn (sang trang khác) vẫn tính theo hạn chót nên quay lại không lệch giờ.
        AttachedToVisualTree += (_, _) => { if (_session.Result is null) _tick.Start(); Tick(); };
        DetachedFromVisualTree += (_, _) => _tick.Stop();
        Tick();
        Show(0);
    }

    private Question Current => _session.Shown[_index];

    private void Tick()
    {
        if (_session.Result != null) return;
        ClockText.Text = L.F("practice.exam.left", ExamSession.Clock(_session.Left));
        ClockText.Classes.Set("danger", _session.Left <= 300);
        if (_session.Expired) Finish();
    }

    // Hai cột khi đủ chỗ, không thì lưới số câu xuống dưới (như trang Hôm nay).
    private void Layout(double width)
    {
        var two = width >= (double)this.FindResource("MainMinWidth")! + 24 + (double)this.FindResource("SideMinWidth")!;
        if (two == (Grid.GetColumn(GridCard) == 2)) return;
        Columns.ColumnDefinitions[0].MinWidth = two ? (double)this.FindResource("MainMinWidth")! : 0;
        Columns.ColumnDefinitions[2].MinWidth = two ? (double)this.FindResource("SideMinWidth")! : 0;
        Columns.ColumnDefinitions[0].Width = two ? new GridLength(2, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        Columns.ColumnDefinitions[2].Width = two ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Columns.RowDefinitions.Clear();
        if (!two)
        {
            Columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Columns.RowDefinitions.Add(new RowDefinition(24, GridUnitType.Pixel));
            Columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        Grid.SetColumn(GridCard, two ? 2 : 0);
        Grid.SetRow(GridCard, two ? 0 : 2);
    }

    private void Show(int i)
    {
        if (_session.Shown.Count == 0) return;
        _showing = true;
        _index = i;
        var q = Current;
        var done = _session.Result != null;
        var type = Grade.QType(q);
        var picked = _session.Picked(q);
        NumberText.Text = L.F("practice.run.question", i + 1);
        TagText.Text = q.Tag is { } tag && tag.Trim() != NumberText.Text ? tag : "";
        StatusText.Text = done ? Status(q) : "";
        StatusText.IsVisible = done;
        Prompt.Content = ContentRenderer.Render(q.Prompt);
        Options.Children.Clear();
        _choices.Clear();
        var order = _session.Order(q) ?? [.. Enumerable.Range(0, q.Options.Count)];
        for (var pos = 0; pos < order.Length; pos++)
        {
            var k = order[pos];
            ToggleButton choice = type == "multi" ? new CheckBox() : new RadioButton { GroupName = "exam" + q.Id };
            var row = new DockPanel();
            var letter = new TextBlock { Classes = { "marker" }, Text = Letters[Math.Min(pos, Letters.Length - 1)] + "." };
            DockPanel.SetDock(letter, Dock.Left);
            row.Children.Add(letter);
            if (done && Mark(q, k, picked) is { } mark)
            {
                DockPanel.SetDock(mark, Dock.Right);
                row.Children.Add(mark);
            }
            row.Children.Add(ContentRenderer.Render(q.Options[k]));
            choice.Content = row;
            choice.Tag = k;
            choice.IsChecked = type == "multi" ? picked?.Indices?.Contains(k) == true : picked?.Number == k;
            choice.IsEnabled = !done;
            choice.IsCheckedChanged += OnChoice;
            _choices.Add(choice);
            Options.Children.Add(choice);
        }
        AnswerRow.IsVisible = !done && type is "numeric" or "short";
        AnswerBox.Text = picked?.Text ?? "";
        UnitText.Text = q.Unit ?? "";
        YourAnswer.IsVisible = done && type is "numeric" or "short";
        YourAnswer.Text = L.F("practice.exam.yourAnswer", picked?.Text is { Length: > 0 } t ? t : L.T("practice.exam.blankAnswer"), Grade.AnswerText(q));
        SolutionBox.IsVisible = done && q.Solution.Length > 0;
        if (done) Solution.Content = ContentRenderer.Render(q.Solution);
        ClearButton.IsVisible = !done;
        PrevButton.IsEnabled = i > 0;
        NextButton.IsEnabled = i < _session.Shown.Count - 1;
        _showing = false;
        MarkGrid();
    }

    // Sau khi nộp: ghi "đáp án đúng" cạnh phương án đúng, "bạn chọn" cạnh phương án đã chọn mà sai.
    private static TextBlock? Mark(Question q, int k, Answer? picked)
    {
        var right = Grade.QType(q) == "multi" ? (q.Answers ?? []).Contains(k) : k == q.Answer;
        var chosen = Grade.QType(q) == "multi" ? picked?.Indices?.Contains(k) == true : picked?.Number == k;
        if (right) return new TextBlock { Classes = { "right", "ok" }, Margin = new Avalonia.Thickness(12, 0, 0, 0), Text = L.T("practice.exam.markRight") };
        if (chosen) return new TextBlock { Classes = { "right", "danger" }, Margin = new Avalonia.Thickness(12, 0, 0, 0), Text = L.T("practice.exam.markPicked") };
        return null;
    }

    private string Status(Question q)
    {
        var p = _session.Picked(q);
        var state = L.T(Answer.IsBlankValue(p) ? "practice.exam.blank" : Grade.IsCorrect(q, p) ? "practice.exam.right" : "practice.exam.wrong");
        var s = _session.Seconds(q);
        if (s <= 0) return state;
        var time = ExamSession.Clock(s);
        if (s > _session.Pace * 1.5) time = L.F("practice.exam.slowTime", time, ExamSession.Clock(_session.Pace));
        return L.F("practice.exam.time", state, time);
    }

    private void MarkGrid()
    {
        var done = _session.Result != null;
        for (var k = 0; k < Numbers.Children.Count; k++)
        {
            var b = (Button)Numbers.Children[k];
            var q = _session.Shown[k];
            var p = _session.Picked(q);
            b.Classes.Set("done", !done && p != null);
            b.Classes.Set("ok", done && !Answer.IsBlankValue(p) && Grade.IsCorrect(q, p));
            b.Classes.Set("danger", done && !Answer.IsBlankValue(p) && !Grade.IsCorrect(q, p));
            b.Classes.Set("accent", k == _index);
        }
        GridMeta.Text = L.F("practice.exam.gridMeta", _session.PickedCount, _session.Shown.Count);
    }

    private void Pick()
    {
        var q = Current;
        Answer? a = Grade.QType(q) switch
        {
            "multi" => _choices.Any(c => c.IsChecked == true) ? Answer.Many([.. _choices.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!)]) : null,
            "numeric" or "short" => AnswerBox.Text is { } t && t.Trim().Length > 0 ? Answer.Of(t) : null,
            _ => _choices.FirstOrDefault(c => c.IsChecked == true) is { } c ? Answer.Index((int)c.Tag!) : null,
        };
        _session.Pick(q, a);
        MarkGrid();
    }

    private void OnChoice(object? sender, RoutedEventArgs e)
    {
        // RadioButton bỏ chọn cái cũ cũng báo đổi: chỉ ghi khi có phương án được chọn, hoặc khi bỏ tick (multi).
        if (_showing || (sender is RadioButton r && r.IsChecked != true)) return;
        Pick();
    }

    private void OnAnswerText(object? sender, TextChangedEventArgs e)
    {
        if (!_showing && _session.Result is null && _session.Shown.Count > 0) Pick();
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        if (_session.Shown.Count == 0) return;
        _session.Pick(Current, null);
        Show(_index);
    }

    /// <summary>Chọn sẵn vài đáp án (chụp kiểm tra lưới số câu có câu đã làm).</summary>
    internal void Demo(params (int Question, int Option)[] picks)
    {
        foreach (var (q, o) in picks)
            if (q < _session.Shown.Count) _session.Pick(_session.Shown[q], Answer.Index(o));
        Show(_index);
    }

    /// <summary>Nộp luôn (chụp kiểm tra màn xem lại).</summary>
    internal void DemoFinish() => Finish();

    private void Disarm()
    {
        _disarm.Stop();
        _armed = false;
        SubmitButton.Content = L.T("practice.exam.submit");
    }

    private void OnSubmit(object? sender, RoutedEventArgs e)
    {
        if (!_armed)
        {
            _armed = true;
            SubmitButton.Content = L.T("practice.exam.confirm");
            _disarm.Start();
            return;
        }
        Disarm();
        Finish();
    }

    private void Finish()
    {
        if (_session.Result != null) return;
        _tick.Stop();
        var r = _session.Finish();
        ClockText.IsVisible = SubmitButton.IsVisible = HintText.IsVisible = false;
        ResultCard.IsVisible = true;
        ScoreText.Text = L.F("practice.exam.score", r.Score.ToString("0.##", L.Culture), r.Max.ToString("0.##", L.Culture), r.Ten.ToString("0.#", L.Culture));
        CountsText.Text = L.F("practice.exam.counts", r.Right, r.Wrong, r.Blank, ExamSession.Clock(r.Seconds));
        var slow = _session.Slow();
        SlowText.IsVisible = slow.Count > 0;
        SlowText.Text = L.F("practice.exam.slow", slow.Count, ExamSession.Clock(_session.Pace), string.Join(", ", slow));
        Finished?.Invoke();
        Show(0);
    }

    private void OnPrev(object? sender, RoutedEventArgs e) => Show(Math.Max(0, _index - 1));
    private void OnNext(object? sender, RoutedEventArgs e) => Show(Math.Min(_session.Shown.Count - 1, _index + 1));
    private void OnHistory(object? sender, RoutedEventArgs e) => HistoryRequested?.Invoke();
    private void OnBack(object? sender, RoutedEventArgs e) => BackRequested?.Invoke();
}
