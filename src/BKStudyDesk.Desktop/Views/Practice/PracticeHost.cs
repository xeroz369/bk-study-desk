using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SoHocTap.Core;
using SoHocTap.Presentation.Practice;
using SoHocTap.Ui;
using BKStudyDesk.Desktop.Practice;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>
/// Trang Luyện tập native (thay khung Svelte trong WebView): nạp dữ liệu một lần (PracticeService), hiện trang chính, mở màn con
/// (làm câu, bài học, thi thử, soạn) và quay lại. MainWindow giữ một instance để chuyển trang không mất bài đang làm.
/// </summary>
public sealed class PracticeHost : UserControl, ITabbedPage
{
    private readonly PracticeService _service;
    private readonly Stack<Control> _back = new();
    private readonly string? _demo;

    // Cỡ chữ (Ctrl+cộng, Ctrl+trừ) nay áp cho cả app ở MainWindow (config app.zoom), không còn riêng Luyện tập.
    private Control? Screen
    {
        get => Content as Control;
        set => Content = value;
    }

    public PracticeHost(SoHocTap.Api.ApiRouter router)
    {
        _service = new PracticeService(router);
        // Chụp kiểm tra: --luyen-man=lam|bai|thi|soan mở thẳng màn con với dữ liệu đang có (thư mục demo).
        _demo = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--luyen-man=", StringComparison.Ordinal))?[12..];
        Screen = new TextBlock { Classes = { "empty" }, Text = L.T("practice.loading") };
        AttachedToVisualTree += async (_, _) =>
        {
            if (_service.Loaded) return;
            try { await _service.LoadAsync(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Log.Error("Luyện tập: nạp dữ liệu", e);
            }
            ShowHome();
            if (_demo is { } d) OpenDemo(d);
        };
    }

    /// <summary>Đang ở một màn con (không phải trang chính): Alt+Mũi tên trái lùi trong Luyện tập trước.</summary>
    public bool CanGoBack => Screen is Control and not PracticeHomeView and not TextBlock;

    /// <summary>Lùi một màn (như nút Quay lại).</summary>
    public void GoBack() => Back();

    /// <summary>Bài đã soạn cho tìm nhanh Ctrl+K; rỗng khi sổ chưa nạp.</summary>
    public IEnumerable<SoHocTap.Presentation.QuickItem> QuickItems() => !_service.Loaded ? [] :
        _service.Study.Manifest.Courses.SelectMany(c => c.Units.SelectMany(u => u.Lessons))
            .Where(e => _service.Study.Lessons.ContainsKey(e.Id) && _service.Study.Entry(e.Id) is not null)
            .Select(e => new SoHocTap.Presentation.QuickItem(e.Title, L.F("quick.lesson", _service.Study.Entry(e.Id)!.CourseName), "lesson:" + e.Id));

    /// <summary>Mở một bài từ tìm nhanh: về trang chính rồi mở bài, Quay lại về trang chính.</summary>
    public void OpenLessonById(string id)
    {
        if (!_service.Loaded) return;
        if (Screen is not PracticeHomeView) ShowHome();
        OpenLesson(id);
    }

    /// <summary>Tab của trang chính: đang hiện trang chính thì đọc, ghi thẳng vào đó; đang ở màn con thì dùng khi quay về.</summary>
    public int Tab
    {
        get => Screen is PracticeHomeView h ? h.SelectedTab : _tab;
        set
        {
            _tab = value;
            if (Screen is PracticeHomeView h) h.SelectTab(value);
        }
    }

    private int _tab;

    /// <summary>Ghi nốt kết quả còn chờ (MainWindow gọi trước khi rời trang).</summary>
    public async Task FlushAsync()
    {
        if (_service.Loaded && _service.Progress.Pending) await _service.Progress.FlushAsync();
    }

    // Dựng lại trang chính (số liệu đổi sau mỗi lượt làm), giữ tab đang mở.
    private void ShowHome()
    {
        if (Screen is PracticeHomeView old) Tab = old.SelectedTab;
        var home = new PracticeHomeView(_service.Home(), _service.Problems());
        home.SelectTab(Tab);
        home.Go += Go;
        home.ArchiveRequested += on => _ = ArchiveAsync(home, on);
        _back.Clear();
        Screen = home;
        _ = LoadAutoSaveAsync(home);
    }

    private async Task LoadAutoSaveAsync(PracticeHomeView home)
    {
        try { home.SetAutoSave(await _service.SaveQuizzesAsync()); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { Log.Warn($"Luyện tập: đọc công tắc lưu quiz: {e.Message}"); }
    }

    // Công tắc tự lưu (true, false) hay Lưu ngay (null), như QuizArchive.svelte.
    private async Task ArchiveAsync(PracticeHomeView home, bool? on)
    {
        try
        {
            if (on is { } want)
            {
                var now = await _service.SaveQuizzesAsync(want);
                home.SetAutoSave(now ?? !want);
                home.ArchiveSays(L.T(now is null ? "practice.archive.prefFailed" : want ? "practice.archive.on" : "practice.archive.off"));
            }
            else home.ArchiveSays(L.T(await _service.SyncLmsAsync() ? "practice.archive.syncing" : "practice.archive.syncFailed"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Warn($"Luyện tập: kho quiz: {e.Message}");
            home.ArchiveSays(L.T("practice.archive.prefFailed"));
        }
    }

    private void Open(Control screen)
    {
        if (Screen is Control c) _back.Push(c);
        Screen = screen;
    }

    private async void Back()
    {
        await FlushAsync();
        var prev = _back.Count > 0 ? _back.Pop() : null;
        if (prev is null or PracticeHomeView)
        {
            if (prev is PracticeHomeView h) Tab = h.SelectedTab;
            ShowHome();
        }
        else Screen = prev;
    }

    private async Task ReloadAsync()
    {
        await FlushAsync();
        Screen = new TextBlock { Classes = { "empty" }, Text = L.T("practice.loading") };
        try { await _service.LoadAsync(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Error("Luyện tập: nạp lại dữ liệu", e);
        }
        ShowHome();
    }

    // Lỗi khi mở một màn chỉ ghi log và giữ màn đang có, không làm sập app (async void không có ai bắt lỗi).
    private async void Go(string what)
    {
        try { await GoAsync(what); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Error($"Luyện tập: mở {what}", e);
        }
    }

    private async Task GoAsync(string what)
    {
        var study = _service.Study;
        var progress = _service.Progress;
        var (verb, arg) = what.IndexOf(':') is var i and >= 0 ? (what[..i], what[(i + 1)..]) : (what, "");
        switch (verb)
        {
            case "due":
                var due = progress.DueQuestions();
                Run(L.T("practice.run.due"), RunMode.Due, _ => due, "practice.run.empty.due");
                break;
            case "review":
                var wrong = study.LessonQuestions().Where(q => progress.NeedsReview(q.Id)).ToList();
                Run(L.T("practice.run.review"), RunMode.Review, _ => wrong, "practice.run.empty.review");
                break;
            case "flagged":
                var noted = PracticeActions.Noted(study, progress);
                var transfer = _service.Transfer();
                Run(L.T("practice.today.flagged"), RunMode.Flagged, _ => noted, "practice.run.empty.flagged")
                    .EnableReport(q => !transfer.QuizLocked(q.LessonId ?? ""));
                break;
            case "next" when progress.NextLesson() is { } e:
                OpenLesson(e.Id);
                break;
            case "lesson" or "quiz":
                OpenLesson(arg);
                break;
            case "mix" when study.Course(arg) is { } c:
                // Mỗi lượt rút lại: câu vừa làm xuống sau vì không còn "chưa làm".
                Run($"{L.T("practice.mix")}, {c.Name}", RunMode.Mix, _ => Interleave.Run(study.UnitQuestions(c), Interleave.MixCount, PracticeActions.Fresh(progress)),
                    "practice.run.empty.mix", newRound: true, hint: L.T("practice.run.mixHint"));
                break;
            case "random" when study.RandomExam(arg, PracticeActions.Fresh(progress)) is { } x:
                OpenExam(x);
                break;
            case "author":
                OpenAuthor(int.TryParse(arg, out var tabNo) ? new AuthorPlace(null, null, null, tabNo, null) : null);
                break;
            case "recall":
                // Quiz LMS không cho xem lại: ghi câu còn nhớ vào bài "Ghi lại: tên quiz" của chương quiz LMS (chỉ trên máy này).
                var (code, quizName) = arg.IndexOf('|') is var bar and >= 0 ? (arg[..bar], arg[(bar + 1)..]) : (arg, "");
                if (study.Manifest.Courses.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) is { } rc)
                    OpenAuthor(new AuthorPlace(rc.Id, LmsQuiz.UnitTitle, SoHocTap.Presentation.Practice.Transfer.RecallTitle(quizName), 0, null));
                else if (Screen is PracticeHomeView rh) rh.ArchiveSays(L.T("practice.archive.noCourse"));
                break;
            case "page":
                var page = new StaticPageView(study.Manifest.Pages?.FirstOrDefault(p => p.Id == arg)?.Title ?? arg, study.Pages.GetValueOrDefault(arg)?.Html);
                page.BackRequested += Back;
                Open(page);
                break;
            case "retry":
                await ReloadAsync();
                break;
            case "remove":
                if (await _service.RemovePackAsync(arg) is { } err) _service.Warnings.Add(L.F("practice.quiz.removeFailed", arg, err));
                await ReloadAsync();
                break;
        }
    }

    /// <param name="questions">Danh sách câu cho một lượt (gọi lại khi Lượt mới); danh sách chốt khi mở, như bản Svelte.</param>
    private QuizRunView Run(string title, RunMode mode, Func<ShufflePrefs, IReadOnlyList<Question>> questions, string emptyKey,
        ShufflePrefs? shuffle = null, bool newRound = false, string? hint = null)
    {
        var v = new QuizRunView(title, _service,
            prefs => new QuizSession(_service.Progress, mode, questions(prefs), prefs, (uint)Environment.TickCount),
            L.T(emptyKey), shuffle, newRound, hint);
        v.BackRequested += Back;
        Open(v);
        return v;
    }

    /// <param name="replace">Bài trước, bài sau: thay bài đang mở, Quay lại vẫn về chỗ đã mở bài đầu tiên.</param>
    private void OpenLesson(string id, bool replace = false)
    {
        if (_service.Study.Entry(id) is not { } entry) return;
        var l = _service.Study.Lessons.GetValueOrDefault(id);
        if (l != null)
        {
            _service.Progress.OpenLesson(id);
            _ = _service.Progress.FlushAsync();
        }
        var v = new LessonView(_service, entry, l);
        v.BackRequested += Back;
        v.LessonRequested += next => OpenLesson(next, replace: true);
        // Xáo theo gói cho tới khi người học tự chọn (hai ô trên màn làm câu).
        var qs = l?.Questions ?? [];
        v.QuestionsRequested += () => Run(l?.Title ?? entry.Title, RunMode.Lesson, _ => qs, "practice.run.empty.lesson",
            // Lựa chọn người học đã đổi thì nhớ trên máy (config practice.shuffle), chưa đổi thì theo cài đặt của gói.
            new ShufflePrefs(Config.Bool("practice.shuffle.questions", l?.Shuffle?.Questions == true), Config.Bool("practice.shuffle.options", l?.Shuffle?.Options == true)));
        if (replace) Screen = v;
        else Open(v);
    }

    private AuthorView OpenAuthor(AuthorPlace? place)
    {
        var v = new AuthorView(_service, place);
        v.BackRequested += Back;
        // Đã lưu gói: nạp lại sổ (gói mới, id câu mới) rồi mở lại Soạn đúng chỗ, Quay lại vẫn về trang chính.
        v.Saved += async after =>
        {
            try
            {
                await ReloadAsync();
                OpenAuthor(after);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Error("Luyện tập: nạp lại sau khi lưu gói", e);
            }
        };
        Open(v);
        return v;
    }

    private ExamView OpenExam(Exam x)
    {
        var qs = _service.Study.ExamQuestions(x);
        var session = new ExamSession(_service.Progress, x, qs, ExamSession.ScoringFor(_service.Study, x, qs.Count),
            new ShufflePrefs(x.Shuffle?.Questions == true, x.Shuffle?.Options == true), (uint)Environment.TickCount);
        var v = new ExamView(session);
        v.BackRequested += Back;
        v.Finished += () => _ = _service.Progress.FlushAsync();
        v.HistoryRequested += () =>
        {
            Tab = 3;
            _back.Clear();
            Screen = null;
            ShowHome();
        };
        Open(v);
        return v;
    }

    // Màn mẫu để chụp: lấy dữ liệu demo có sẵn, đặt sẵn trạng thái cho đủ phần cần duyệt.
    private void OpenDemo(string screen)
    {
        var study = _service.Study;
        switch (screen)
        {
            case "lam":
                var qs = study.LessonQuestions();
                if (qs.Count == 0) return;
                Run(L.T("practice.run.review"), RunMode.Review, _ => qs, "practice.run.empty.review").Demo(0);
                break;
            case "lam-dung":
                var all = study.LessonQuestions();
                if (all.Count == 0) return;
                Run(L.T("practice.run.lesson"), RunMode.Lesson, _ => all, "practice.run.empty.lesson", new ShufflePrefs(false, true)).Demo(all[0].Answer);
                break;
            case "danh-dau" when study.LessonQuestions().FirstOrDefault() is { } fq:
                _service.Progress.SetNote(fq.Fp, text: "Lời giải thiếu bước đổi dấu.", flag: true);
                Go("flagged");
                break;
            case "ghi-lai" when _service.Quizzes.FirstOrDefault(i => i.LessonId.Length == 0) is { } gq:
                Go($"recall:{gq.Quiz.Code}|{gq.Quiz.Quiz}");
                break;
            case "trang" when study.Pages.Keys.FirstOrDefault() is { } pid:
                Go("page:" + pid);
                break;
            case "bai" when study.Lessons.Keys.FirstOrDefault() is { } id:
                OpenLesson(id);
                break;
            case "thi" when study.Exams.Values.FirstOrDefault() is { } x:
                OpenExam(x).Demo((0, 0), (1, 1), (3, 2));
                break;
            case "thi-xong" when study.Exams.Values.FirstOrDefault() is { } xd:
                var ev = OpenExam(xd);
                ev.Demo((0, 0), (1, 1), (3, 2));
                ev.DemoFinish();
                break;
            case "soan":
                var a = OpenAuthor(study.Manifest.Courses.FirstOrDefault() is { Units.Count: > 0 } dc && dc.Units[0].Lessons.Count > 0 ? new AuthorPlace(dc.Id, dc.Units[0].Title, dc.Units[0].Lessons[0].Title, 0, null) : null);
                a.Demo(@"Tính \(\int_0^2 3x^2\,dx\).", ["4", "6", "8", "12"], 2, @"\(x^3\big|_0^2=8\).");
                break;
            case "soan-luu" when study.Manifest.Courses.FirstOrDefault() is { Units.Count: > 0 } lc && lc.Units[0].Lessons.Count > 0:
                // Kiểm tra luồng lưu trên thư mục demo: điền câu rồi bấm Lưu câu như người dùng.
                var la = OpenAuthor(new AuthorPlace(lc.Id, lc.Units[0].Title, lc.Units[0].Lessons[0].Title, 0, null));
                la.Demo("Câu demo lưu thử: 1 + 1 bằng", ["1", "2"], 1, "Vì 1 + 1 = 2.");
                la.DemoSave();
                break;
            case "soan-nhap" or "soan-xuat" or "soan-ai" when study.Manifest.Courses.FirstOrDefault() is { } sc:
                var tab = screen switch { "soan-nhap" => 1, "soan-xuat" => 2, _ => 3 };
                var at = OpenAuthor(new AuthorPlace(sc.Id, sc.Units.FirstOrDefault()?.Title, null, tab, null));
                if (tab == 1) at.DemoRaw("### Câu 1\nĐạo hàm của \\(\\sin x\\) là\n- [x] \\(\\cos x\\)\n- [ ] \\(-\\cos x\\)\n> Công thức cơ bản.\n\n### Câu 2\nThiếu đáp án\n- [ ] a\n- [ ] b\n");
                break;
        }
    }
}
