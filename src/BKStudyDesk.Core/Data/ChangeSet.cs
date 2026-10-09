namespace SoHocTap.Data;

/// <summary>Trường nào của một buổi học đã đổi.</summary>
[Flags]
public enum ClassFields
{
    None = 0,
    Room = 1,
    Time = 2,
    Day = 4,
    Weeks = 8,
}

/// <summary>Trường nào của một lịch thi đã đổi.</summary>
[Flags]
public enum ExamFields
{
    None = 0,
    Date = 1,
    Time = 2,
    Room = 4,
}

/// <summary>Một buổi học thêm (Before null), bỏ (After null) hay đổi phòng/giờ/thứ/tuần.</summary>
public sealed record ScheduleChange(string Code, string Name, MybkClass? Before, MybkClass? After, ClassFields Fields);

/// <summary>Một lịch thi mới (Before null), bị gỡ (After null) hay đổi ngày/giờ/phòng.</summary>
public sealed record ExamChange(string Code, string Name, string Type, MybkExam? Before, MybkExam? After, ExamFields Fields);

/// <summary>Mục điểm LMS vừa có điểm hoặc đổi điểm; Before là điểm cũ (null = trước đó chưa có).</summary>
public sealed record LmsGradeChange(long Course, string Subject, LmsGradeItem Item, double? Before);

/// <summary>Môn vừa có điểm trên MyBK hoặc đổi điểm; Before là dòng cũ (null = trước đó chưa có điểm).</summary>
public sealed record MybkGradeChange(MybkGrade Grade, MybkGrade? Before);

/// <summary>
/// Khác gì giữa dữ liệu trước và sau một lần đồng bộ: sự kiện mới/bị gỡ, đổi lịch học, đổi lịch thi, điểm mới.
/// Lần đầu (chưa có bản trước) thì không có thay đổi nào: mọi thứ đều "mới", báo hết là spam.
/// Hub tính sau mỗi lần sync thành công có dữ liệu đổi; tới 1.1.8 vẫn chưa tính năng nào dùng (chỉ ghi log số lượng).
/// </summary>
public sealed record ChangeSet(
    IReadOnlyList<LmsEvent> NewEvents,
    IReadOnlyList<LmsEvent> RemovedEvents,
    IReadOnlyList<ScheduleChange> Schedule,
    IReadOnlyList<ExamChange> Exams,
    IReadOnlyList<LmsGradeChange> LmsGrades,
    IReadOnlyList<MybkGradeChange> MybkGrades)
{
    public static readonly ChangeSet Empty = new([], [], [], [], [], []);

    public bool IsEmpty => NewEvents.Count + RemovedEvents.Count + Schedule.Count + Exams.Count + LmsGrades.Count + MybkGrades.Count == 0;

    /// <summary>Số lượng từng loại, cho dòng log.</summary>
    public string Counts =>
        $"sự kiện +{NewEvents.Count}/-{RemovedEvents.Count}, lịch học {Schedule.Count}, lịch thi {Exams.Count}, điểm LMS {LmsGrades.Count}, điểm MyBK {MybkGrades.Count}";

    public static ChangeSet Compare(LmsData? before, LmsData? after)
    {
        if (before is null || after is null) return Empty;
        var (added, removed) = CompareEvents(before.Events ?? [], after.Events ?? []);
        return Empty with { NewEvents = added, RemovedEvents = removed, LmsGrades = CompareLmsGrades(before.Grades ?? [], after.Grades ?? []) };
    }

    public static ChangeSet Compare(MybkData? before, MybkData? after)
    {
        if (before is null || after is null) return Empty;
        return Empty with
        {
            Schedule = CompareSchedule(before.Schedule ?? [], after.Schedule ?? []),
            Exams = CompareExams(before.Exams ?? [], after.Exams ?? []),
            MybkGrades = CompareMybkGrades(before.Grades ?? [], after.Grades ?? []),
        };
    }

    // ------------------------------------------------------------------ LMS

    /// <summary>Sự kiện khớp theo Id. Mục đã xong (bài đã nộp) không tính là mới.</summary>
    internal static (List<LmsEvent> Added, List<LmsEvent> Removed) CompareEvents(IEnumerable<LmsEvent> before, IEnumerable<LmsEvent> after)
    {
        var old = before.Where(e => e?.Id is not null).GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var now = after.Where(e => e?.Id is not null).GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        var added = now.Values.Where(e => !old.ContainsKey(e.Id) && !e.Done).OrderBy(e => e.Time).ToList();
        var removed = old.Values.Where(e => !now.ContainsKey(e.Id)).OrderBy(e => e.Time).ToList();
        return (added, removed);
    }

    /// <summary>Mục điểm khớp theo lớp + tên mục. Mới = trước chưa có điểm (hoặc chưa có mục), nay có; hoặc điểm đổi.</summary>
    internal static List<LmsGradeChange> CompareLmsGrades(IEnumerable<LmsGradeBook> before, IEnumerable<LmsGradeBook> after)
    {
        var old = new Dictionary<(long, string), double?>();
        foreach (var b in before.Where(b => b is not null))
            foreach (var i in (b.Items ?? []).Where(i => i is not null))
                old.TryAdd((b.Course, Key(i)), i.Grade);
        var list = new List<LmsGradeChange>();
        foreach (var b in after.Where(b => b is not null))
            foreach (var i in (b.Items ?? []).Where(i => i?.Grade is not null))
            {
                var had = old.TryGetValue((b.Course, Key(i)), out var prev) ? prev : null;
                if (had is null || Math.Abs(had.Value - i.Grade!.Value) > 1e-9) list.Add(new LmsGradeChange(b.Course, b.Subject, i, had));
            }
        return list;

        static string Key(LmsGradeItem i) => $"{i.Kind}|{i.Name}";
    }

    // ------------------------------------------------------------------ MyBK

    /// <summary>
    /// Buổi học khớp theo mã môn + nhóm: buổi giống hệt thì bỏ qua, buổi còn lại ghép theo thứ tự (thứ, giờ), thừa ra là thêm/bỏ.
    /// Một môn có thể có nhiều buổi một tuần (lý thuyết, bài tập) cùng mã cùng nhóm nên không khớp theo mã được.
    /// </summary>
    internal static List<ScheduleChange> CompareSchedule(IEnumerable<MybkClass> before, IEnumerable<MybkClass> after)
    {
        var list = new List<ScheduleChange>();
        var oldGroups = before.Where(c => c is not null).GroupBy(c => (c.Code, c.Group)).ToDictionary(g => g.Key, g => g.ToList());
        var newGroups = after.Where(c => c is not null).GroupBy(c => (c.Code, c.Group)).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var key in oldGroups.Keys.Union(newGroups.Keys))
        {
            var olds = Order(oldGroups.GetValueOrDefault(key) ?? []);
            var news = Order(newGroups.GetValueOrDefault(key) ?? []);
            // Bỏ cặp giống hệt trước, để một buổi đổi giờ không làm lệch cặp của các buổi khác.
            foreach (var n in news.ToList())
                if (olds.FindIndex(o => Diff(o, n) == ClassFields.None) is var i and >= 0) { olds.RemoveAt(i); news.Remove(n); }
            for (var i = 0; i < Math.Max(olds.Count, news.Count); i++)
            {
                var o = i < olds.Count ? olds[i] : null;
                var n = i < news.Count ? news[i] : null;
                var fields = o is not null && n is not null ? Diff(o, n) : ClassFields.None;
                var any = n ?? o!;
                list.Add(new ScheduleChange(any.Code, any.Name, o, n, fields));
            }
        }
        return list;

        static List<MybkClass> Order(List<MybkClass> l) => [.. l.OrderBy(c => c.Day).ThenBy(c => c.Start, StringComparer.Ordinal)];
    }

    internal static ClassFields Diff(MybkClass a, MybkClass b)
    {
        var f = ClassFields.None;
        if (!Same(a.Room, b.Room)) f |= ClassFields.Room;
        if (!Same(a.Start, b.Start) || !Same(a.End, b.End)) f |= ClassFields.Time;
        if (a.Day != b.Day) f |= ClassFields.Day;
        if (!(a.Weeks ?? []).SequenceEqual(b.Weeks ?? [])) f |= ClassFields.Weeks;
        return f;
    }

    /// <summary>Lịch thi khớp theo mã môn + loại thi (GK, CK).</summary>
    internal static List<ExamChange> CompareExams(IEnumerable<MybkExam> before, IEnumerable<MybkExam> after)
    {
        var old = before.Where(e => e is not null).GroupBy(e => (e.Code, e.Type)).ToDictionary(g => g.Key, g => g.First());
        var now = after.Where(e => e is not null).GroupBy(e => (e.Code, e.Type)).ToDictionary(g => g.Key, g => g.First());
        var list = new List<ExamChange>();
        foreach (var (key, n) in now)
        {
            if (!old.TryGetValue(key, out var o)) { list.Add(new ExamChange(n.Code, n.Name, n.Type, null, n, ExamFields.None)); continue; }
            var f = ExamFields.None;
            if (!Same(o.Date, n.Date)) f |= ExamFields.Date;
            if (!Same(o.Time, n.Time)) f |= ExamFields.Time;
            if (!Same(o.Room, n.Room)) f |= ExamFields.Room;
            if (f != ExamFields.None) list.Add(new ExamChange(n.Code, n.Name, n.Type, o, n, f));
        }
        foreach (var (key, o) in old)
            if (!now.ContainsKey(key)) list.Add(new ExamChange(o.Code, o.Name, o.Type, o, null, ExamFields.None));
        return list;
    }

    /// <summary>Điểm môn khớp theo học kỳ + mã môn. Mới = trước chưa có điểm (số, chữ hay mã đặc biệt), nay có; hoặc đổi điểm.</summary>
    internal static List<MybkGradeChange> CompareMybkGrades(IEnumerable<MybkGrade> before, IEnumerable<MybkGrade> after)
    {
        var old = before.Where(g => g is not null).GroupBy(g => (g.Term, g.Code)).ToDictionary(g => g.Key, g => g.First());
        var list = new List<MybkGradeChange>();
        foreach (var g in after.Where(g => g is not null && HasGrade(g)))
        {
            var o = old.GetValueOrDefault((g.Term, g.Code));
            if (o is null || !HasGrade(o)) list.Add(new MybkGradeChange(g, null));
            else if (o.Score != g.Score || !Same(o.Letter, g.Letter) || !Same(o.Special, g.Special)) list.Add(new MybkGradeChange(g, o));
        }
        return list;

        static bool HasGrade(MybkGrade g) => g.Score is not null || !string.IsNullOrWhiteSpace(g.Letter) || !string.IsNullOrWhiteSpace(g.Special);
    }

    /// <summary>So chuỗi bỏ khoảng trắng hai đầu, null coi như rỗng (MyBK lúc trả null lúc trả "").</summary>
    private static bool Same(string? a, string? b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.Ordinal);
}
