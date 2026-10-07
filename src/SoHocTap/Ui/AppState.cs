using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Ui;

/// <summary>Một mốc thời gian (hạn nộp, quiz, buổi học, thi, đợt đăng ký) gộp từ LMS và MyBK.</summary>
/// <param name="ClassGroup">Mã nhóm (lớp thí nghiệm) trong tên mốc, nối bằng ", "; rỗng nếu tên không có mã. Tên <c>Group</c> đã dùng cho nhóm theo ngày của bảng.</param>
/// <param name="State">Trạng thái nộp bài thật từ LMS (chỉ bài tập); Unknown thì cột Trạng thái để trống.</param>
/// <param name="RawWhen">Chữ ngày giờ gốc khi không đọc được hết (MyBK đổi định dạng): hiện nguyên chữ đó thay cho ngày giờ tính ra.</param>
public sealed record TimelineItem(string Id, string Kind, string Name, string Subject, long Time, string Label, string? Url, string Source,
    bool Done = false, string? Warn = null, long? Course = null, bool Opens = false, long? Due = null, string? RawWhen = null, string ClassGroup = "",
    SubmissionState State = SubmissionState.Unknown)
{
    /// <summary>Time của mốc không đọc được ngày (01/01/9999): xếp cuối, không đếm ngược, vẫn hiện trong danh sách.</summary>
    public const long UndatedTime = 253_370_764_800;
    public bool Undated => Time >= UndatedTime;

    /// <summary>
    /// Hạn của LMS đã qua mà chưa làm: hiện ở nhóm "Quá hạn" đầu danh sách, không biến mất. Chỉ tính trong phần lịch LMS đọc về
    /// (từ 7 ngày trước, <see cref="Sources.Lms.AssignPairing.WindowBefore"/>): bài có hạn cũ hơn thì app không biết đã nộp hay chưa.
    /// </summary>
    public bool Overdue => Source == SourceIds.Lms && !Done && !Opens && !Undated && Time < Format.Now && Time >= Format.Now - Sources.Lms.AssignPairing.WindowBefore;

    /// <summary>Thứ tự nhóm: "Quá hạn" luôn đứng đầu, các nhóm còn lại theo giờ.</summary>
    public int GroupRank => Overdue ? 0 : 1;

    /// <summary>Loại mốc → key tên loại trong file ngôn ngữ.</summary>
    public static readonly Dictionary<string, string> KindText = new()
    {
        ["assign"] = "kind.assign",
        ["quiz"] = "kind.quiz",
        ["event"] = "kind.event",
        ["exam"] = "kind.exam",
        ["class"] = "kind.class",
        ["reg"] = "kind.reg",
        ["custom"] = "kind.custom",
        ["makeup"] = "kind.makeup",
    };

    /// <summary>Id của mốc lấy từ sự kiện tự thêm (data/custom-events.json).</summary>
    public const string CustomPrefix = "ce-";

    /// <summary>Id trong data/custom-events.json nếu mốc là sự kiện tự thêm; null với mốc từ LMS, MyBK.</summary>
    public string? CustomId => Source == "custom" && Id.StartsWith(CustomPrefix, StringComparison.Ordinal) ? Id[CustomPrefix.Length..] : null;
    /// <summary>Cột Trạng thái: chữ theo trạng thái nộp bài LMS; mốc không phải bài tập hay chưa biết thì để trống.</summary>
    public string StateText => State switch
    {
        SubmissionState.Submitted => L.T("state.submitted"),
        SubmissionState.Draft => L.T("state.draft"),
        SubmissionState.NotSubmitted => L.T("state.notSubmitted"),
        SubmissionState.Graded => L.T("state.graded"),
        _ => "",
    };
    public string KindName => Opens ? L.T("kind.quizOpen") : KindText.TryGetValue(Kind, out var k) ? L.T(k) : Kind;
    /// <summary>Dòng phụ dưới tên trong bảng sự kiện: môn rồi chi tiết (Hạn nộp, Quiz đóng, phòng học). Môn trùng tên mốc (buổi học) thì chỉ ghi chi tiết.</summary>
    public string Sub => Subject.Length == 0 || Subject == Name ? Label : Label.Length == 0 ? Subject : $"{Subject}, {Label}";
    /// <summary>Ngày giờ kèm chi tiết (phòng thi, số phút): dòng phụ của thẻ Lịch thi.</summary>
    public string WhenAndLabel => Label.Length == 0 ? When : $"{When}, {Label}";
    /// <summary>Tên và dòng phụ, cho trình đọc màn hình và sao chép.</summary>
    public string Spoken => Sub.Length == 0 ? Name : $"{Name}, {Sub}";
    public string When => RawWhen ?? Format.DateTime(Time);
    /// <summary>Cột "Còn": mốc quiz mở thì ghi hạn đóng (mốc mở không phải hạn nộp, đếm ngược tới đó dễ hiểu nhầm).</summary>
    public string Left => Kind == "class" || Undated ? "" : Done ? L.T("timeline.done") : Overdue ? L.F("timeline.overdueOn", Format.Local(Time))
        : Opens ? (Due is { } d ? L.F("timeline.dueOn", Format.Local(d)) : L.T("timeline.opens")) : Ui.Due.LeftText(Time, Format.Now);
    /// <summary>Mức gấp theo cài đặt (Rất gấp, Gấp). Buổi học, mốc không rõ ngày và quiz mở (chưa phải hạn nộp) không tính: None.</summary>
    public Urgency Urgency
    {
        get
        {
            if (Kind == "class" || Undated || Opens) return Urgency.None;
            var u = Ui.Due.Of(Time, Format.Now, Done, Core.Settings.Notify.UrgentHours, Core.Settings.Notify.SoonHours);
            // Quá hạn chỉ tính theo <see cref="Overdue"/> (LMS, trong cửa sổ 7 ngày); hạn cũ hơn thì app không biết đã nộp chưa.
            return u == Urgency.Overdue && !Overdue ? Urgency.None : u;
        }
    }
    public string Group => Undated ? L.T("timeline.undated") : Overdue ? L.T("format.group.overdue") : Format.DayGroup(Time);
    public string Hour => RawWhen is not null || Undated ? "" : Format.Hm(Time);
    /// <summary>Nhóm theo ngày: "Hôm nay, Thứ năm 01/10"; hạn đã qua mà chưa làm thì gom vào "Quá hạn".</summary>
    public string Day => Undated ? L.T("timeline.undated") : Overdue ? L.T("format.group.overdue") : Format.DayDiff(Time) switch
    {
        0 => L.F("format.dayToday", Format.DateLong(Format.Local(Time))),
        1 => L.F("format.dayTomorrow", Format.DateLong(Format.Local(Time))),
        _ => Format.DateLong(Format.Local(Time)),
    };
}

public enum AccountNeed { None, Lms, Mybk, Both }

/// <summary>
/// State dữ liệu cho UI WPF: đọc lms.json, mybk.json, trạng thái từng nguồn rồi dựng timeline.
/// Nguồn sync xong thì đọc lại (<see cref="Reload"/>) và bắn event <see cref="Changed"/> trên UI thread.
/// </summary>
public sealed partial class AppState(SourceHub hub)
{
    public LmsData? Lms { get; private set; }
    public MybkData? Mybk { get; private set; }
    public JsonObject Sources { get; private set; } = [];
    public List<TimelineItem> Timeline { get; private set; } = [];
    public event Action? Changed;

    public void Reload()
    {
        // Store có cache theo mtime: file không đổi thì không parse lại (Reload được gọi ở mỗi lần nguồn báo có dữ liệu).
        Lms = LmsStore.Read();
        Mybk = MybkStore.Read();
        RefreshStatus();
    }

    /// <summary>Đang đồng bộ, chỉ đổi bước/tiến độ: đọc lại trạng thái nguồn, báo riêng cho thanh trạng thái.</summary>
    public event Action? Progress;

    public void RefreshProgress()
    {
        Sources = hub.StatusJson();
        Progress?.Invoke();
    }

    /// <summary>Trạng thái nguồn đổi (bắt đầu, xong mà không có gì mới, lỗi được xóa) nhưng dữ liệu thì không: thanh trạng thái và InfoBar.</summary>
    public event Action? StatusChanged;

    public void RefreshStatusOnly()
    {
        Sources = hub.StatusJson();
        StatusChanged?.Invoke();
    }

    /// <summary>Chỉ đọc lại trạng thái nguồn (đang sync, lỗi), khá nhẹ nên gọi lúc nguồn vừa bắt đầu chạy.</summary>
    public void RefreshStatus()
    {
        Sources = hub.StatusJson();
        Timeline = BuildTimeline();
        Changed?.Invoke();
    }

    public JsonObject? Source(string name) => Sources[name] as JsonObject;
    public bool Syncing(string name) => Source(name)?["syncing"]?.GetValue<bool>() == true;
    public string? Error(string name) => Source(name)?["error"]?.GetValue<string>();
    public long? SyncedAt(string name) => Source(name)?["syncedAt"]?.GetValue<long?>();

    /// <summary>Loại lỗi đồng bộ gần nhất của nguồn; null nếu không lỗi hoặc lỗi ghi bởi bản cũ (chỉ có message).</summary>
    public SyncErrorKind? ErrorKind(string name) => Error(name) is null ? null : SyncErrorText.Parse(Source(name)?["errorKind"]?.GetValue<string>());

    /// <summary>
    /// File dữ liệu đã lưu của nguồn có mà không đọc được (hỏng, khác định dạng): lý do kỹ thuật để hiện ở Chi tiết;
    /// null = đọc được hoặc chưa có file. Trang nên báo <c>data.unreadable</c> thay vì để trống.
    /// </summary>
    public static string? Unreadable(string name) => name switch
    {
        SourceIds.Lms => LmsStore.Problem,
        SourceIds.Mybk => MybkStore.Problem,
        _ => null,
    };

    /// <summary>Phần không đọc được ở lần đồng bộ gần nhất (đồng bộ vẫn xong): tên phần và chi tiết kỹ thuật.</summary>
    public IReadOnlyList<(string What, string Detail)> Warnings(string name) =>
        (Source(name)?["warnings"] as JsonArray ?? []).Select(w => w?.GetValue<string>() ?? "").Where(w => w.Length > 0)
            .Select(w => w.Split(SyncSignal.DetailMark, 2) is var p && p.Length == 2 ? (p[0], p[1]) : (w, "")).ToList();

    /// <summary>
    /// Lỗi đồng bộ gần nhất của nguồn <paramref name="name"/>, viết lại cho người dùng theo loại lỗi (chữ kỹ thuật để ở Detail);
    /// null nếu không lỗi. Dùng cái này thay cho <see cref="Explain(string, string)"/> để câu báo dựa trên loại lỗi.
    /// </summary>
    public (string Text, string? Detail)? ExplainError(string name, string label) =>
        Error(name) is { } raw ? Explain(label, ErrorKind(name), raw) : null;

    /// <summary>Câu báo theo loại lỗi; không có loại (sync-state.json của bản cũ) thì đoán theo chữ như trước.</summary>
    public static (string Text, string? Detail) Explain(string label, SyncErrorKind? kind, string raw) =>
        kind is { } k ? (L.F(SyncErrorText.LangKey(k), label), raw) : Explain(label, raw);

    /// <summary>
    /// Lỗi của cả lượt đồng bộ, viết lại cho người dùng theo hướng dẫn thông báo lỗi của Windows (vấn đề, nguyên nhân, cách xử lý; chữ kỹ
    /// thuật để ở Chi tiết). Lỗi do app tự viết (mất mạng, server chậm, giới hạn, hết phiên) đã là câu dễ hiểu thì giữ nguyên.
    /// Chỉ còn dùng cho lỗi không có loại (ghi bởi bản cũ); lỗi mới đi qua <see cref="ExplainError"/>.
    /// </summary>
    public static (string Text, string? Detail) Explain(string label, string raw)
    {
        if (raw.StartsWith("Không ", StringComparison.Ordinal) || raw.StartsWith("Phiên ", StringComparison.Ordinal)
            || raw.Contains("không phản hồi", StringComparison.Ordinal) || raw.Contains("giới hạn", StringComparison.Ordinal)
            || raw.Contains("bảo trì", StringComparison.Ordinal))
            return (raw, null);
        if (raw.Contains("Access to the path", StringComparison.OrdinalIgnoreCase) || raw.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
            return (L.F("error.fileBusy", label), raw);
        return (L.F("error.generic", label), raw);
    }

    /// <summary>Tiến độ của lần đồng bộ đang chạy (câu, đếm, tên, phần đã xong); null nếu không đồng bộ hay chưa báo bước nào.</summary>
    public SyncProgress? Step(string name) =>
        Source(name) is { } o && o["step"]?.GetValue<string>() is { } k
            ? new SyncProgress(k, o["count"]?.GetValue<int>() ?? 0, o["of"]?.GetValue<int>() ?? 0, o["detail"]?.GetValue<string>(), o["permille"]?.GetValue<int?>())
            : null;

    /// <summary>
    /// Câu nói thật về dữ liệu của một nguồn khi trang không có gì để hiện: file đã lưu không đọc được, đang lấy lần đầu, lấy lỗi,
    /// hay chưa đăng nhập. null = đã có dữ liệu (trang trống nghĩa là thật sự không có gì).
    /// </summary>
    public string? NoDataReason(string name, string label)
    {
        if (Unreadable(name) is not null) return L.F("data.unreadable", label);
        if (SyncedAt(name) is not null) return null;
        if (Syncing(name)) return L.F("data.loading", label);
        if (Error(name) is { } err) return L.F("data.failed", label, ErrorKind(name) is { } k ? L.F(SyncErrorText.LangKey(k), label) : err);
        return L.F("data.none", label);
    }

    [GeneratedRegex("hết hạn|chưa đăng nhập|cần đăng nhập|chưa sẵn sàng|invalidtoken", RegexOptions.IgnoreCase)] private static partial Regex LoginError();

    /// <summary>Lỗi gần nhất của nguồn là do phiên đăng nhập: theo loại lỗi, lỗi của bản cũ (không có loại) thì dò chữ như trước.</summary>
    private bool NeedsLogin(string name) =>
        ErrorKind(name) is { } k ? k == SyncErrorKind.SessionExpired : Error(name) is { } e && LoginError().IsMatch(e);

    /// <summary>Một tài khoản HCMUT cho cả hai nguồn: nguồn nào đang cần đăng nhập lại.</summary>
    public AccountNeed Account
    {
        get
        {
            var lms = Source(SourceIds.Lms) is { } l && (l["connected"]?.GetValue<bool>() != true || NeedsLogin(SourceIds.Lms));
            var mybk = NeedsLogin(SourceIds.Mybk);
            return (lms, mybk) switch { (true, true) => AccountNeed.Both, (true, _) => AccountNeed.Lms, (_, true) => AccountNeed.Mybk, _ => AccountNeed.None };
        }
    }

    [GeneratedRegex(@"(\d{1,2})/(\d{1,2})/(\d{4})")] private static partial Regex DateInName();

    /// <summary>Tên quiz có ngày lệch xa giờ trên LMS (≥ 30 ngày): nhiều khả năng giảng viên đặt nhầm năm.</summary>
    public static (long Named, string Text)? QuizAnomaly(LmsQuiz q)
    {
        var m = DateInName().Match(q.Name);
        var refTime = q.Open ?? q.Close;
        if (!m.Success || refTime is null) return null;
        try
        {
            var named = Format.Sec(new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value), 0, 1, 0));
            return Math.Abs(named - refTime.Value) >= 30 * 86400 ? (named, m.Value) : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Nhãn hiện ra của một mốc LMS. Nhãn hạn nộp là chữ của app lưu trong dữ liệu (LmsSource.DueLabel): dịch theo ngôn ngữ đang dùng.
    /// Nhãn khác là chữ của LMS (tên sự kiện theo ngôn ngữ của trang LMS), giữ nguyên.
    /// </summary>
    public static string LmsLabel(LmsEvent e) =>
        e.Kind == "assign" && e.Label == global::SoHocTap.Sources.Lms.LmsSource.DueLabel ? L.T("timeline.dueLabel") : e.Label;

    private List<TimelineItem> BuildTimeline()
    {
        var output = new List<TimelineItem>();
        if (Lms is { } lms)
        {
            var seen = new HashSet<string>();
            // Bài đã nộp (done) vẫn vào timeline, đánh dấu Done: trang tự lọc theo "Hiện việc đã làm", không đếm, không nhắc.
            foreach (var e in lms.Events)
            {
                // Mốc mở của quiz: theo eventtype của Moodle; dữ liệu cũ chưa có eventtype thì so với giờ mở trong danh sách quiz.
                var quiz = e.Kind == "quiz" ? lms.Quizzes.FirstOrDefault(q => q.Name == e.Name && q.Course == e.Course) : null;
                var opens = e.Kind == "quiz" && (e.Phase == "open" || (e.Phase is null && quiz?.Open == e.Time && quiz.Close != e.Time));
                var due = opens ? lms.Events.FirstOrDefault(x => x.Kind == "quiz" && x.Phase == "close" && x.Name == e.Name && x.Course == e.Course)?.Time ?? quiz?.Close : null;
                output.Add(new TimelineItem(e.Id, e.Kind, e.Name, e.Subject, e.Time, LmsLabel(e), e.Url, SourceIds.Lms, Done: e.Done, Course: e.Course, Opens: opens, Due: due,
                    State: e.Kind == "assign" ? SubmissionStates.Parse(e.State) : SubmissionState.Unknown));
                seen.Add(e.Name + "|" + e.Time);
            }
            foreach (var q in lms.Quizzes)
            {
                var odd = QuizAnomaly(q);
                string? warn = odd is { } o ? L.F("timeline.quizWarn", o.Text, Format.Local(q.Open ?? q.Close ?? 0)) : null;
                if (odd is { } a && q.Attempts.Count == 0)
                    output.Add(new TimelineItem($"qz{q.Id}named", "quiz", q.Name, q.Subject, a.Named, L.T("timeline.byName"), q.Url, SourceIds.Lms, Warn: warn, Course: q.Course));
                foreach (var (t, label, close) in new[] { (q.Open, L.T("timeline.quizOpen"), false), (q.Close, L.T("timeline.quizClose"), true) })
                    if (t is { } time && !seen.Contains(q.Name + "|" + time))
                        output.Add(new TimelineItem($"qz{q.Id}{(close ? "close" : "open")}", "quiz", q.Name, q.Subject, time, label, q.Url, SourceIds.Lms,
                            Done: close && q.Attempts.Count > 0, Warn: warn, Course: q.Course, Opens: !close, Due: close ? null : q.Close));
            }
        }
        if (Mybk is { } M)
        {
            foreach (var e in M.Exams)
            {
                var type = e.Type == "GK" ? L.T("timeline.examMid") : e.Type == "CK" ? L.T("timeline.examFinal") : "";
                var title = type.Length > 0 ? L.F("timeline.examTyped", type, e.Name) : L.F("timeline.exam", e.Name);
                // Ngày thi đọc không được thì vẫn giữ trong danh sách với chữ gốc của MyBK (xếp cuối), không rơi về 1970.
                // Đọc được ngày mà giờ thì không: xếp theo ngày, cột giờ ghi nguyên chữ gốc.
                var day = VnTime.ParseDate(e.Date);
                var clock = VnTime.ParseClock(e.Time);
                var time = day is { } d0 ? VnTime.FromWall(d0.AddMinutes(clock ?? 0)) : TimelineItem.UndatedTime;
                var raw = day is null ? $"{e.Date} {e.Time}".Trim() is { Length: > 0 } x ? x : L.T("timeline.undated")
                    : clock is null ? $"{Format.DateLong(day.Value)} {e.Time}".Trim() : null;
                output.Add(new TimelineItem($"mx-{e.Code}{e.Type}", "exam", Regex.Replace(title, @"\s+", " ").Trim(), e.Name,
                    time, e.Minutes is { } m ? L.F("timeline.roomMinutes", e.Room, m) : L.F("timeline.room", e.Room), null, SourceIds.Mybk, RawWhen: raw));
            }
            // Buổi học của cả kỳ, mỗi tuần MyBK liệt kê một buổi (khoảng 20 tuần, vài trăm mốc): trang tự chọn hiện gì, không cắt ở đây.
            // Ngày và giờ học là giờ VN (giờ trường), không theo múi giờ máy.
            var today = VnTime.Today;
            foreach (var c in M.Schedule)
            {
                // Giờ lạ ("", "--") không được làm hỏng cả timeline: không đọc được thì xếp ở 0 giờ, cột giờ ghi nguyên chữ gốc.
                var clock = VnTime.ParseClock(c.Start);
                foreach (var d in Ics.ClassDates(c.Year, c.Weeks ?? [], c.Day, today))
                    output.Add(new TimelineItem($"cl-{c.Code}-{d:yyyyMMdd}-{c.Start}", "class", c.Name, c.Name, VnTime.FromWall(d.AddMinutes(clock ?? 0)),
                        L.F("timeline.classRoom", c.Room, c.Start, c.End), null, SourceIds.Mybk, Done: true, RawWhen: clock is null ? $"{Format.DateLong(d)} {c.Start}".Trim() : null));
            }
            // Mọi đợt đăng ký MyBK trả về (cả đợt đã qua, đợt còn xa): trang chỉ hiện phần sắp tới.
            var url = Config.Str("sources.mybk.registration");
            foreach (var r in M.Registration ?? [])
            {
                output.Add(new TimelineItem("rg-o-" + r.Code, "reg", r.Name, "", r.Start, L.F("timeline.regOpen", r.Code), url, SourceIds.Mybk));
                output.Add(new TimelineItem("rg-c-" + r.Code, "reg", r.Name, "", r.End, L.F("timeline.regClose", r.Code), url, SourceIds.Mybk));
            }
        }
        AddCustomEvents(output);
        var codesOf = GroupFilter.Matcher(Settings.Lms.GroupPattern);
        return [.. output.Select(i => i with { ClassGroup = string.Join(", ", codesOf(i.Name)) }).OrderBy(i => i.Time)];
    }

    /// <summary>Sự kiện tự thêm sau lần sửa: dựng lại timeline, báo các trang (không đọc lại LMS/MyBK).</summary>
    public void CustomEventsChanged()
    {
        Timeline = BuildTimeline();
        Changed?.Invoke();
    }

    /// <summary>Mã và tên các môn trong thời khóa biểu, để tô màu buổi học bù theo môn có trong tiêu đề.</summary>
    public List<(string Code, string Name)> Courses() =>
        [.. (Mybk?.Schedule ?? []).Select(c => (c.Code, c.Name)).Where(c => c.Code.Length > 0 || c.Name.Length > 0).Distinct()];

    /// <summary>"H1-201, 07:00-08:50": địa điểm và giờ của sự kiện tự thêm.</summary>
    public static string CustomLabel(CustomEvent e) => string.Join(", ", new[] { e.Location, CustomTime(e) }.Where(x => x.Length > 0));

    /// <summary>"07:00-08:50", hoặc "07:00" nếu không ghi giờ kết thúc.</summary>
    public static string CustomTime(CustomEvent e) => e.End.Length > 0 ? $"{e.Start}-{e.End}" : e.Start;

    private void AddCustomEvents(List<TimelineItem> output)
    {
        var courses = Courses();
        foreach (var e in CustomEventsData.Store.All())
        {
            if (CustomEvents.StartTime(e) is not { } time) continue;
            // Học bù có tên môn trong tiêu đề thì ghi môn đó (cột Môn, trang môn); sự kiện riêng không gắn môn.
            var subject = e.IsMakeup && CustomEvents.MatchCourse(e.Title, courses) is { } c ? c.Name : "";
            output.Add(new TimelineItem(TimelineItem.CustomPrefix + e.Id, e.IsMakeup ? "makeup" : "custom", e.Title, subject, time, CustomLabel(e), null, "custom"));
        }
    }

    public IEnumerable<TimelineItem> Upcoming(double hours) => Timeline.Where(e => e.Time >= Format.Now && e.Time <= Format.Now + hours * 3600);
}
