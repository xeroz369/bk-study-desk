using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using BKStudyDesk.Desktop.Practice;
using SoHocTap.Presentation.Practice;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>
/// Một lượt làm câu (ôn câu sai, ôn theo lịch, luyện trộn, câu của bài, câu đã đánh dấu). Ghi kết quả qua <see cref="QuizSession"/>;
/// view chỉ giữ câu đang hiện, chữ đang gõ và lúc câu hiện ra (đo thời gian làm).
/// </summary>
public partial class QuizRunView : UserControl
{
    private const string Letters = "ABCDEFGH";

    public QuizRunView() => InitializeComponent();

    public event Action? BackRequested;

    private readonly PracticeService _service = null!;
    private readonly Func<ShufflePrefs, QuizSession> _make = null!;
    private readonly string _title = "";
    private QuizSession _session = null!;
    private int _index;
    private DateTime _shownAt;
    private bool _showing;
    private string? _noteFor;
    private readonly List<ToggleButton> _choices = [];

    /// <param name="make">Dựng lượt mới với lựa chọn xáo (gọi lại khi bấm Lượt mới hay đổi ô xáo).</param>
    /// <param name="shuffle">Có thì hiện hai ô Xáo câu, Xáo phương án với giá trị đầu này (bài học).</param>
    /// <param name="newRound">Hiện nút Lượt mới (luyện trộn).</param>
    // Đánh dấu: chép câu để báo lỗi. Null ở các chế độ khác.
    private Func<Question, bool>? _copyable;

    /// <summary>Bật nút chép câu để báo lỗi (trang Đánh dấu). copyable: câu được chép (quiz LMS còn mở thì không).</summary>
    internal void EnableReport(Func<Question, bool> copyable)
    {
        _copyable = copyable;
        UpdateCopyAll();
        if (_session.Shown.Count > 0) Show(_index);
    }

    private List<Question> CopyableFlagged() =>
        _copyable is null ? [] : [.. _session.Shown.Where(q => _service.Progress.Note(q.Fp)?.Flag == true && _copyable(q))];

    private void UpdateCopyAll()
    {
        var n = CopyableFlagged().Count;
        CopyAllButton.IsVisible = n > 0;
        CopyAllButton.Content = L.F("practice.report.copyAll", n);
    }

    private async Task CopyAsync(IReadOnlyList<Question> qs, Button button)
    {
        if (qs.Count == 0 || TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) return;
        await clip.SetTextAsync(PracticeActions.ReportText(_service.Study, _service.Progress, qs));
        var was = button.Content;
        button.Content = L.T("practice.report.copied");
        await Task.Delay(2000);
        button.Content = was;
        UpdateCopyAll();
    }

    private async void OnCopyAll(object? sender, RoutedEventArgs e) => await CopyAsync(CopyableFlagged(), CopyAllButton);

    private async void OnCopyOne(object? sender, RoutedEventArgs e)
    {
        if (_session.Shown.Count > 0) await CopyAsync([Current], CopyOneButton);
    }

    internal QuizRunView(string title, PracticeService service, Func<ShufflePrefs, QuizSession> make, string empty,
        ShufflePrefs? shuffle = null, bool newRound = false, string? hint = null) : this()
    {
        _service = service;
        _make = make;
        _title = title;
        TitleText.Text = title;
        EmptyText.Text = empty;
        NewRoundButton.IsVisible = newRound;
        HintText.Text = hint ?? "";
        ShufflePanel.IsVisible = shuffle != null;
        _showing = true;
        ShuffleQuestions.IsChecked = shuffle?.Questions == true;
        ShuffleOptions.IsChecked = shuffle?.Options == true;
        _showing = false;
        Start(shuffle ?? ShufflePrefs.None);
    }

    private Question Current => _session.Shown[_index];

    private void Start(ShufflePrefs prefs)
    {
        _noteFor = null;
        _session = _make(prefs);
        var any = _session.Shown.Count > 0;
        EmptyCard.IsVisible = !any;
        QuestionCard.IsVisible = any;
        Bar.IsVisible = any;
        HintText.IsVisible = any && HintText.Text is { Length: > 0 };
        NewRoundButton.IsEnabled = any;
        Bar.Maximum = Math.Max(1, _session.Shown.Count);
        Show(0);
    }

    private void Show(int i)
    {
        SaveNote();
        _index = i;
        SolutionCard.IsVisible = false;
        RetryText.IsVisible = false;
        UpdateMeta();
        if (_session.Shown.Count == 0) return;
        _showing = true;
        var q = Current;
        _shownAt = DateTime.UtcNow;
        Bar.Value = i + 1;
        NumberText.Text = L.F("practice.run.question", i + 1);
        // Nhãn nguồn (ví dụ "GK251 câu 3"); bỏ khi chỉ lặp lại số câu.
        TagText.Text = q.Tag is { } tag && tag.Trim() != L.F("practice.run.question", i + 1) ? tag : "";
        WhereText.Text = Where(q);
        WhereText.IsVisible = WhereText.Text.Length > 0;
        Prompt.Content = ContentRenderer.Render(q.Prompt);
        Options.Children.Clear();
        _choices.Clear();
        var type = Grade.QType(q);
        AnswerRow.IsVisible = type is "numeric" or "short";
        AnswerBox.Text = "";
        AnswerBox.PlaceholderText = L.T(type == "numeric" ? "practice.run.answerBox" : "practice.run.textBox");
        UnitText.Text = q.Unit ?? "";
        var order = _session.Order(q) ?? [.. Enumerable.Range(0, q.Options.Count)];
        for (var pos = 0; pos < order.Length; pos++)
        {
            var k = order[pos];
            ToggleButton choice = type == "multi" ? new CheckBox() : new RadioButton { GroupName = "q" + q.Id };
            var row = new DockPanel();
            var letter = new TextBlock { Classes = { "marker" }, Text = Letters[Math.Min(pos, Letters.Length - 1)] + "." };
            DockPanel.SetDock(letter, Dock.Left);
            row.Children.Add(letter);
            row.Children.Add(ContentRenderer.Render(q.Options[k]));
            choice.Content = row;
            choice.Tag = k;
            _choices.Add(choice);
            Options.Children.Add(choice);
        }
        CheckButton.IsVisible = type != "single" || q.Options.Count > 0;
        PrevButton.IsEnabled = i > 0;
        NextButton.IsEnabled = i < _session.Shown.Count - 1;
        var note = _service.Progress.Note(q.Fp);
        FlagBox.IsChecked = note?.Flag == true;
        FlagBox.IsEnabled = NoteBox.IsEnabled = q.Fp != null;
        NoteBox.Text = note?.Text ?? "";
        _noteFor = q.Id;
        var slide = PracticeActions.Slide(_service.Study, q);
        SlideButton.IsVisible = slide != null;
        SlideButton.Content = slide is { Page: > 0 } s ? L.F("practice.run.slidePage", s.Page) : L.T("practice.run.slide");
        SlideMissing.IsVisible = false;
        CopyOneButton.IsVisible = _copyable?.Invoke(q) == true;
        // Câu đã trả lời trong lượt: chọn lại đáp án đã chọn và hiện lại kết quả.
        if (_session.Tried(q) is { } tried)
        {
            Restore(type, tried);
            Feedback(q);
        }
        StatusText.Text = _session.Status(q);
        StatusText.IsVisible = StatusText.Text.Length > 0;
        _showing = false;
    }

    private string Where(Question q)
    {
        var where = PracticeActions.Where(_service.Study, q);
        if (_session.Mode == RunMode.Due && q.Fp is { } fp && _service.Progress.State.Srs.GetValueOrDefault(fp) is { } r)
            where = L.F("practice.run.where", where, L.F("practice.run.box", r.Box, r.Wrong, r.Seen));
        return where;
    }

    private void UpdateMeta() =>
        MetaText.Text = _session.Shown.Count == 0 ? "" : L.F("practice.run.done", _session.Done, _session.Shown.Count);

    private void Restore(string type, Answer a)
    {
        if (type is "numeric" or "short") AnswerBox.Text = a.Text ?? (a.Number is { } n ? JsNumber.Format(n) : "");
        foreach (var c in _choices)
            c.IsChecked = type == "multi" ? a.Indices?.Contains((int)c.Tag!) == true : a.Number == (int)c.Tag!;
    }

    private Answer? Picked()
    {
        var q = Current;
        return Grade.QType(q) switch
        {
            "multi" => _choices.Any(c => c.IsChecked == true) ? Answer.Many([.. _choices.Where(c => c.IsChecked == true).Select(c => (int)c.Tag!)]) : null,
            "numeric" or "short" => Answer.Of(AnswerBox.Text ?? ""),
            _ => _choices.FirstOrDefault(c => c.IsChecked == true) is { } c ? Answer.Index((int)c.Tag!) : null,
        };
    }

    private void Check()
    {
        if (_session.Shown.Count == 0 || Picked() is not { } a || Answer.IsBlankValue(a)) return;
        var q = Current;
        _session.Answer(q, a, (DateTime.UtcNow - _shownAt).TotalSeconds);
        Feedback(q);
        UpdateMeta();
        StatusText.Text = _session.Status(q);
        StatusText.IsVisible = StatusText.Text.Length > 0;
        _ = _service.Progress.FlushAsync();
    }

    // Sai: báo và cho làm lại. Đúng: hiện lời giải, kèm đáp án theo chữ cái người học thấy khi cần.
    private void Feedback(Question q)
    {
        var ok = _session.Solved(q);
        var type = Grade.QType(q);
        RetryText.IsVisible = !ok;
        RetryText.Text = L.T(type == "multi" ? "practice.run.retryMulti" : "practice.run.retry");
        SolutionCard.IsVisible = ok;
        if (!ok) return;
        var reordered = _session.Order(q) != null;
        AnswerText.IsVisible = type != "single" || reordered;
        AnswerText.Text = L.F("practice.run.answerIs", _session.RightText(q));
        ReorderedText.IsVisible = reordered;
        Solution.Content = ContentRenderer.Render(q.Solution);
    }

    private void SaveNote()
    {
        // Chỉ ghi khi ô ghi chú đang chứa ghi chú của chính câu này (lần hiện đầu ô còn trống, ghi thì xóa mất ghi chú cũ).
        if (_session is null || _session.Shown.Count == 0 || _index >= _session.Shown.Count || _noteFor != Current.Id) return;
        var q = Current;
        var text = NoteBox.Text?.Trim() ?? "";
        if (text == (_service.Progress.Note(q.Fp)?.Text ?? "")) return;
        _service.Progress.SetNote(q.Fp, text: text);
        _ = _service.Progress.FlushAsync();
    }

    /// <summary>Chọn sẵn đáp án rồi chấm (chụp kiểm tra trạng thái sau khi trả lời).</summary>
    internal void Demo(int option)
    {
        foreach (var c in _choices) c.IsChecked = (int)c.Tag! == option;
        Check();
    }

    private void OnCheck(object? sender, RoutedEventArgs e) => Check();

    private void OnAnswerKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Check();
    }

    private void OnPrev(object? sender, RoutedEventArgs e) => Show(Math.Max(0, _index - 1));
    private void OnNext(object? sender, RoutedEventArgs e) => Show(Math.Min(_session.Shown.Count - 1, _index + 1));
    private void OnNewRound(object? sender, RoutedEventArgs e) => Start(Prefs());

    private ShufflePrefs Prefs() => new(ShuffleQuestions.IsChecked == true, ShuffleOptions.IsChecked == true);

    private void OnShuffle(object? sender, RoutedEventArgs e)
    {
        if (_showing || _session is null) return;
        var p = Prefs();
        SoHocTap.Core.Config.Set("practice.shuffle.questions", p.Questions);
        SoHocTap.Core.Config.Set("practice.shuffle.options", p.Options);
        Start(p);
    }

    private void OnFlag(object? sender, RoutedEventArgs e)
    {
        if (_showing || _session.Shown.Count == 0) return;
        var q = Current;
        _service.Progress.SetNote(q.Fp, flag: FlagBox.IsChecked == true);
        UpdateCopyAll();
        StatusText.Text = _session.Status(q);
        StatusText.IsVisible = StatusText.Text.Length > 0;
        _ = _service.Progress.FlushAsync();
    }

    private void OnNoteLost(object? sender, RoutedEventArgs e) => SaveNote();

    private async void OnSlide(object? sender, RoutedEventArgs e)
    {
        if (_session.Shown.Count == 0 || PracticeActions.Slide(_service.Study, Current) is not { } s) return;
        var ok = await _service.OpenSourceAsync(s.Subject, s.File, s.Page);
        SlideMissing.Text = L.F("practice.run.slideMissing", s.Subject);
        SlideMissing.IsVisible = !ok;
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        SaveNote();
        BackRequested?.Invoke();
    }
}
