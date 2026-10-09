using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;
using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Tải tài liệu LMS của nhiều lớp một lượt (discussion #42), lưu vào thư mục từng môn như lúc đồng bộ (bỏ file trùng, bản cũ vào
/// _Lưu trữ). Cây, đếm file mới, cảnh báo nằm ở Presentation/Download.cs; ở đây chỉ có việc của cửa sổ: đọc danh sách mục từng lớp
/// (lần lượt, chỉ lớp được tích hay mở, đỡ gọi LMS), tải, báo tiến độ. Đóng cửa sổ thì dừng cả đọc lẫn tải.
/// </summary>
public partial class DownloadWindow : Window
{
    private readonly IReadOnlyList<TermRow> _terms = [];
    private readonly HashSet<LmsSource.FileKind> _kinds = [.. Enum.GetValues<LmsSource.FileKind>()];
    private readonly CancellationTokenSource _life = new();
    private readonly Queue<CourseRow> _toRead = new();
    private int _readDone, _readTotal;
    private bool _reading;
    private CancellationTokenSource? _run;
    private (int Files, long Bytes)? _confirmed;   // đã bấm Vẫn tải cho đúng lượt lớn này
    private string? _outcome;                      // kết quả lượt tải vừa xong, giữ tới khi người dùng chọn lại

    public DownloadWindow() => InitializeComponent();

    /// <summary><paramref name="pick"/>: các lớp tích sẵn (nút Tải ở một môn); null: mọi lớp của học kỳ đang học.</summary>
    internal DownloadWindow(IReadOnlyCollection<long>? pick) : this()
    {
        Sub.Text = L.F("download.saveToSubjects", Config.Str("folders.subjects"));
        Extract.IsChecked = SoHocTap.Core.Settings.Archives.Extract;
        Closing += (_, _) => _life.Cancel();
        if (LmsStore.Read() is not { Courses.Count: > 0 } lms)
        {
            ListState.Text = L.T("download.noLms");
            ListState.IsVisible = true;
            Update();
            return;
        }
        _terms = DownloadPlan.Build(lms.Courses, lms.Term, () => _kinds);
        foreach (var t in _terms)
            foreach (var c in t.Courses.Where(c => pick?.Contains(c.Course.Id) ?? t.Current))
            {
                c.Checked = true;
                c.Expanded = pick is not null;   // từ một môn: mở sẵn để thấy các mục
                t.Expanded = true;
            }
        Terms.ItemsSource = _terms;
        Opened += (_, _) => Read(Courses.Where(c => c.Checked == true));
        Update();
    }

    private IEnumerable<CourseRow> Courses => _terms.SelectMany(t => t.Courses);

    // ------------------------------------------------------------------ đọc danh sách mục

    /// <summary>Đưa lớp vào hàng đọc (lớp đang đọc thì thôi). again: đọc lại lớp đã đọc (sau khi tải, để số file mới đúng với máy).</summary>
    private void Read(IEnumerable<CourseRow> rows, bool again = false)
    {
        foreach (var c in rows.Where(c => c.State is LoadState.Unread or LoadState.Failed || (again && c.State == LoadState.Ready)))
        {
            c.Begin();
            _toRead.Enqueue(c);
            _readTotal++;
        }
        if (!_reading && _toRead.Count > 0) _ = ReadAllAsync();
        Update();
    }

    /// <summary>Một lớp một lần (mỗi lớp một request core_course_get_contents), không dồn request lên LMS.</summary>
    private async Task ReadAllAsync()
    {
        _reading = true;
        while (_toRead.TryDequeue(out var c))
        {
            try { c.Loaded(await Task.Run(() => LmsSource.SectionsAsync(c.Course.Id, _life.Token))); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Warn($"Tải tài liệu: không đọc được các mục của {c.Name}: {e.Message}");
                c.Failed(e.Message);
            }
            _readDone++;
            Update();
        }
        _reading = false;
        _readDone = _readTotal = 0;
        Update();
    }

    // ------------------------------------------------------------------ chọn

    private static T? Row<T>(object? sender) where T : class => (sender as Control)?.DataContext as T;

    private void OnTermCheck(object? sender, RoutedEventArgs e)
    {
        if (Row<TermRow>(sender) is not { } t) return;
        t.Checked = t.Checked != true;
        Picked(t.Courses.Where(c => c.Checked == true));
    }

    private void OnCourseCheck(object? sender, RoutedEventArgs e)
    {
        if (Row<CourseRow>(sender) is not { } c) return;
        c.Checked = c.Checked != true;
        Picked(c.Checked == true ? [c] : []);
    }

    private void OnCourseOpen(object? sender, RoutedEventArgs e)
    {
        if (Row<CourseRow>(sender) is { } c) Read([c]);
    }

    private void OnPick(object? sender, RoutedEventArgs e) => Picked([]);

    private void OnKinds(object? sender, RoutedEventArgs e)
    {
        foreach (var (box, kind) in new[] { (KindPdf, LmsSource.FileKind.Pdf), (KindSlide, LmsSource.FileKind.Slide), (KindOther, LmsSource.FileKind.Other) })
            if (box.IsChecked == true) _kinds.Add(kind); else _kinds.Remove(kind);
        Picked([]);
    }

    /// <summary>Người dùng vừa đổi phần chọn: bỏ kết quả lượt trước, đọc lớp vừa tích mà chưa đọc.</summary>
    private void Picked(IEnumerable<CourseRow> read)
    {
        _outcome = null;
        Read(read);
    }

    // ------------------------------------------------------------------ kế hoạch, trạng thái

    private sealed record Plan(IReadOnlyList<CourseRow> Courses, int Files, long Bytes, DownloadPlan.Verdict Verdict, long? Free);

    private Plan Current()
    {
        var courses = Courses.Where(c => c.Pending.Count > 0).ToList();
        var pending = courses.SelectMany(c => c.Pending).ToList();
        var bytes = pending.Sum(f => f.Bytes);
        var free = FreeBytes();
        var verdict = DownloadPlan.Judge(pending.Count, bytes, free, Config.Int("download.warnMB", 500) * 1024L * 1024, Config.Int("download.warnFiles", 200));
        return new(courses, pending.Count, bytes, verdict, free);
    }

    /// <summary>Chỗ trống của ổ chứa thư mục môn học; không đo được (ổ mạng, chưa tạo thư mục) thì null, không chặn.</summary>
    private static long? FreeBytes()
    {
        try { return Path.GetPathRoot(Path.GetFullPath(Organizer.SubjectsRoot)) is { Length: > 0 } root ? new DriveInfo(root).AvailableFreeSpace : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    /// <summary>Vẽ lại dòng phụ của cây, nút, dải cảnh báo và dòng trạng thái theo phần đang chọn. Đang tải thì trạng thái do lượt tải ghi.</summary>
    private void Update()
    {
        foreach (var t in _terms)
        {
            foreach (var c in t.Courses) c.Refresh();
            t.Refresh();
        }
        if (_run is not null) return;
        var plan = Current();
        var large = plan.Verdict == DownloadPlan.Verdict.Large;
        var confirmed = large && _confirmed == (plan.Files, plan.Bytes);
        Start.IsEnabled = !_reading && plan.Verdict is DownloadPlan.Verdict.Ok or DownloadPlan.Verdict.Large;
        Start.Content = L.T(confirmed ? "download.startAnyway" : "download.start");
        Notify(plan.Verdict == DownloadPlan.Verdict.NoSpace ? ("error", L.F("download.noSpace", Format.Size(plan.Bytes), Format.Size(plan.Free ?? 0)))
            : confirmed ? ("warning", L.F("download.large", plan.Files, Format.Size(plan.Bytes), plan.Free is { } f ? Format.Size(f) : L.T("download.freeUnknown")))
            : null);
        Bar.IsVisible = _reading;
        if (_reading) { Bar.IsIndeterminate = false; Bar.Maximum = _readTotal; Bar.Value = _readDone; }
        Status.Text = _reading ? L.F("download.readingCount", _readDone, _readTotal)
            : _outcome ?? (plan.Files == 0 ? (_terms.Count > 0 ? L.T("download.nothing") : "") : L.F("download.plan", plan.Files, Format.Size(plan.Bytes), plan.Courses.Count));
    }

    private void Notify((string Kind, string Text)? notice)
    {
        Notice.IsVisible = notice is not null;
        Notice.Classes.Set("warning", notice?.Kind == "warning");
        Notice.Classes.Set("error", notice?.Kind == "error");
        NoticeText.Text = notice?.Text ?? "";
    }

    // ------------------------------------------------------------------ tải

    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        var plan = Current();
        if (plan.Verdict is DownloadPlan.Verdict.Nothing or DownloadPlan.Verdict.NoSpace || _run is not null) return;
        // Lượt lớn: lần bấm đầu chỉ hiện cảnh báo, nút thành Vẫn tải; đổi phần chọn (số file, dung lượng khác) thì phải xác nhận lại.
        if (plan.Verdict == DownloadPlan.Verdict.Large && _confirmed != (plan.Files, plan.Bytes))
        {
            _confirmed = (plan.Files, plan.Bytes);
            Update();
            return;
        }
        var extract = Extract.IsChecked == true;
        var kinds = _kinds.ToHashSet();
        var run = _run = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        Busy(true);
        Notify(null);
        Bar.Maximum = plan.Files;
        Bar.Value = 0;
        int done = 0, got = 0, failed = 0;
        try
        {
            foreach (var c in plan.Courses)
            {
                var sections = c.Sections.Where(s => s.Selected && s.Files.Any(f => !f.Have)).Select(s => s.Info.Index).ToList();
                var name = c.Name;
                var r = await Task.Run(() => LmsSource.DownloadSectionsAsync(c.Course.Id, sections, extract, line => Dispatcher.UIThread.Post(() => Write(line)),
                    run.Token, kinds, file => Dispatcher.UIThread.Post(() =>
                    {
                        done++;
                        Bar.Value = Math.Min(done, plan.Files);
                        Status.Text = L.F("download.progress", done, plan.Files, $"{name}, {file}");
                    })));
                got += r.Got;
                failed += r.Failed;
            }
            _outcome = failed > 0 ? L.F("download.doneFailed", got, failed) : got == 0 ? L.T("download.doneNone") : L.F("download.done", got);
        }
        catch (OperationCanceledException) { _outcome = L.T("download.stopped"); }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Log.Error("Lỗi tải tài liệu nhiều lớp", x);
            _outcome = L.F("download.error", x.Message);
        }
        finally
        {
            run.Dispose();
            _run = null;
            _confirmed = null;
            Busy(false);
            if (!_life.IsCancellationRequested) Read(plan.Courses, again: true);   // số file mới, "đã có" khớp lại với máy
        }
    }

    private void Busy(bool on)
    {
        Terms.IsEnabled = KindPdf.IsEnabled = KindSlide.IsEnabled = KindOther.IsEnabled = Extract.IsEnabled = !on;
        Start.IsVisible = !on;
        Stop.IsVisible = on;
        Bar.IsVisible = on;
        if (on) LogBox.IsVisible = true;
    }

    private void OnStop(object? sender, RoutedEventArgs e) => _run?.Cancel();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void Write(string line)
    {
        Output.Text += line.TrimStart() + Environment.NewLine;
        Output.CaretIndex = Output.Text.Length;
    }
}
