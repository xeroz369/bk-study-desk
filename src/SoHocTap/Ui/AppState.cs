using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Core;
using SoHocTap.Sources;

namespace SoHocTap.Ui;

/// <summary>Một mốc thời gian (hạn nộp, quiz, buổi học, thi, đợt đăng ký) gộp từ LMS và MyBK.</summary>
public sealed record TimelineItem(string Id, string Kind, string Name, string Subject, long Time, string Label, string? Url, string Source,
    bool Done = false, string? Warn = null, long? Course = null, bool Opens = false, long? Due = null)
{
    /// <summary>Loại mốc → key tên loại trong file ngôn ngữ.</summary>
    public static readonly Dictionary<string, string> KindText = new()
    {
        ["assign"] = "kind.assign",
        ["quiz"] = "kind.quiz",
        ["event"] = "kind.event",
        ["exam"] = "kind.exam",
        ["class"] = "kind.class",
        ["reg"] = "kind.reg",
    };
    public string KindName => Opens ? L.T("kind.quizOpen") : KindText.TryGetValue(Kind, out var k) ? L.T(k) : Kind;
    public string When => Format.DateTime(Time);
    /// <summary>Cột "Còn": mốc quiz mở thì ghi hạn đóng (mốc mở không phải hạn nộp, đếm ngược tới đó dễ hiểu nhầm).</summary>
    public string Left => Kind == "class" ? "" : Done ? L.T("timeline.done")
        : Opens ? (Due is { } d ? L.F("timeline.dueOn", Format.Local(d)) : L.T("timeline.opens")) : Format.Until(Time);
    public bool Urgent => !Done && !Opens && Kind != "class" && Time > Format.Now && Time - Format.Now < 86400;
    public string Group => Format.DayGroup(Time);
    public string Hour => Format.Hm(Time);
    /// <summary>Nhóm theo ngày: "Hôm nay · Thứ năm 01/10".</summary>
    public string Day => Format.DayDiff(Time) switch
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
        Lms = DataFiles.Read<LmsData>("lms.json");
        Mybk = DataFiles.Read<MybkData>("mybk.json");
        RefreshStatus();
    }

    /// <summary>Đang đồng bộ, chỉ đổi bước/tiến độ: đọc lại trạng thái nguồn, báo riêng cho thanh trạng thái.</summary>
    public event Action? Progress;

    public void RefreshProgress()
    {
        Sources = hub.StatusJson();
        Progress?.Invoke();
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

    /// <summary>Phần không đọc được ở lần đồng bộ gần nhất (đồng bộ vẫn xong): tên phần và chi tiết kỹ thuật.</summary>
    public IReadOnlyList<(string What, string Detail)> Warnings(string name) =>
        (Source(name)?["warnings"] as JsonArray ?? []).Select(w => w?.GetValue<string>() ?? "").Where(w => w.Length > 0)
            .Select(w => w.Split(SyncSignal.DetailMark, 2) is var p && p.Length == 2 ? (p[0], p[1]) : (w, "")).ToList();

    /// <summary>
    /// Lỗi của cả lượt đồng bộ, viết lại cho người dùng theo hướng dẫn thông báo lỗi của Windows (vấn đề, nguyên nhân, cách xử lý; chữ kỹ
    /// thuật để ở Chi tiết). Lỗi do app tự viết (mất mạng, server chậm, giới hạn, hết phiên) đã là câu dễ hiểu thì giữ nguyên.
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

    /// <summary>Bước đang làm của lần đồng bộ (key ngôn ngữ, số đã xong, tổng); null nếu không đồng bộ.</summary>
    public (string Key, int Done, int Total)? Step(string name) =>
        Source(name) is { } o && o["step"]?.GetValue<string>() is { } k ? (k, o["done"]?.GetValue<int>() ?? 0, o["total"]?.GetValue<int>() ?? 0) : null;

    /// <summary>
    /// Câu nói thật về dữ liệu của một nguồn khi trang không có gì để hiện: đang lấy lần đầu, lấy lỗi, hay chưa đăng nhập.
    /// null = đã có dữ liệu (trang trống nghĩa là thật sự không có gì).
    /// </summary>
    public string? NoDataReason(string name, string label)
    {
        if (SyncedAt(name) is not null) return null;
        if (Syncing(name)) return L.F("data.loading", label);
        if (Error(name) is { } err) return L.F("data.failed", label, err);
        return L.F("data.none", label);
    }

    [GeneratedRegex("hết hạn|chưa đăng nhập|cần đăng nhập|chưa sẵn sàng|invalidtoken", RegexOptions.IgnoreCase)] private static partial Regex LoginError();

    /// <summary>Một tài khoản HCMUT cho cả hai nguồn: nguồn nào đang cần đăng nhập lại.</summary>
    public AccountNeed Account
    {
        get
        {
            var lms = Source("lms") is { } l && (l["connected"]?.GetValue<bool>() != true || (Error("lms") is { } e && LoginError().IsMatch(e)));
            var mybk = Error("mybk") is { } m && LoginError().IsMatch(m);
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

    private List<TimelineItem> BuildTimeline()
    {
        var output = new List<TimelineItem>();
        if (Lms is { } lms)
        {
            var seen = new HashSet<string>();
            foreach (var e in lms.Events)
            {
                // Mốc mở của quiz: theo eventtype của Moodle; dữ liệu cũ chưa có eventtype thì so với giờ mở trong danh sách quiz.
                var quiz = e.Kind == "quiz" ? lms.Quizzes.FirstOrDefault(q => q.Name == e.Name && q.Course == e.Course) : null;
                var opens = e.Kind == "quiz" && (e.Phase == "open" || (e.Phase is null && quiz?.Open == e.Time && quiz.Close != e.Time));
                var due = opens ? lms.Events.FirstOrDefault(x => x.Kind == "quiz" && x.Phase == "close" && x.Name == e.Name && x.Course == e.Course)?.Time ?? quiz?.Close : null;
                output.Add(new TimelineItem(e.Id, e.Kind, e.Name, e.Subject, e.Time, e.Label, e.Url, "lms", Course: e.Course, Opens: opens, Due: due));
                seen.Add(e.Name + "|" + e.Time);
            }
            foreach (var q in lms.Quizzes)
            {
                var odd = QuizAnomaly(q);
                string? warn = odd is { } o ? L.F("timeline.quizWarn", o.Text, Format.Local(q.Open ?? q.Close ?? 0)) : null;
                if (odd is { } a && q.Attempts.Count == 0)
                    output.Add(new TimelineItem($"qz{q.Id}named", "quiz", q.Name, q.Subject, a.Named, L.T("timeline.byName"), q.Url, "lms", Warn: warn, Course: q.Course));
                foreach (var (t, label, close) in new[] { (q.Open, L.T("timeline.quizOpen"), false), (q.Close, L.T("timeline.quizClose"), true) })
                    if (t is { } time && !seen.Contains(q.Name + "|" + time))
                        output.Add(new TimelineItem($"qz{q.Id}{(close ? "close" : "open")}", "quiz", q.Name, q.Subject, time, label, q.Url, "lms",
                            Done: close && q.Attempts.Count > 0, Warn: warn, Course: q.Course, Opens: !close, Due: close ? null : q.Close));
            }
        }
        if (Mybk is { } M)
        {
            foreach (var e in M.Exams)
            {
                var type = e.Type == "GK" ? L.T("timeline.examMid") : e.Type == "CK" ? L.T("timeline.examFinal") : "";
                var title = type.Length > 0 ? L.F("timeline.examTyped", type, e.Name) : L.F("timeline.exam", e.Name);
                output.Add(new TimelineItem($"mx-{e.Code}{e.Type}", "exam", Regex.Replace(title, @"\s+", " ").Trim(), e.Name,
                    Format.ExamTime(e.Date, e.Time), e.Minutes is { } m ? L.F("timeline.roomMinutes", e.Room, m) : L.F("timeline.room", e.Room), null, "mybk"));
            }
            // Buổi học 14 ngày tới theo tuần học của từng môn.
            for (var k = 0; k < 14; k++)
            {
                var d = DateTime.Today.AddDays(k);
                var dow = Format.MybkDay(d);
                var wk = Format.IsoWeek(d);
                foreach (var c in M.Schedule.Where(c => c.Day == dow && c.Weeks.Contains(wk)))
                {
                    var parts = (c.Start ?? "0:0").Split(':');
                    var t = d.AddHours(int.Parse(parts[0])).AddMinutes(parts.Length > 1 ? int.Parse(parts[1]) : 0);
                    output.Add(new TimelineItem($"cl-{c.Code}-{d:yyyyMMdd}-{c.Start}", "class", c.Name, c.Name, Format.Sec(t), L.F("timeline.classRoom", c.Room, c.Start, c.End), null, "mybk", Done: true));
                }
            }
            var url = Config.Str("sources.mybk.registration");
            foreach (var r in (M.Registration ?? []).Where(r => r.End > Format.Now && r.Start < Format.Now + 14 * 86400))
            {
                if (r.Start > Format.Now) output.Add(new TimelineItem("rg-o-" + r.Code, "reg", r.Name, "", r.Start, L.F("timeline.regOpen", r.Code), url, "mybk"));
                output.Add(new TimelineItem("rg-c-" + r.Code, "reg", r.Name, "", r.End, L.F("timeline.regClose", r.Code), url, "mybk"));
            }
        }
        return [.. output.OrderBy(i => i.Time)];
    }

    public IEnumerable<TimelineItem> Upcoming(double hours) => Timeline.Where(e => e.Time >= Format.Now && e.Time <= Format.Now + hours * 3600);
}
