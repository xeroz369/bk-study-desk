using System.Globalization;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một môn trong danh sách bên trái. Files: số tài liệu trên máy (null = chưa quét xong).</summary>
public sealed record SubjectRow(string Name, string Meta, bool Current, IReadOnlyList<LmsCourse> Courses, int? Files)
{
    public bool OnLms => Courses.Count > 0;
    /// <summary>Dòng phụ trong danh sách: môn không học kỳ này ghi rõ "Môn đã học và môn khác" trước.</summary>
    public string ListSub => Current ? Meta : string.Join(", ", new[] { L.T("subjects.past"), Meta }.Where(x => x.Length > 0));
    public override string ToString() => Name;   // tên cho trình đọc màn hình
}

/// <summary>Một dòng của tab Tài liệu (thư mục đứng trước file, như trình quản lý file).</summary>
public sealed record FileRow(string Name, bool Dir, string Kind, long Size, long Modified, int Count)
{
    public string SizeText => Dir ? L.F("subjects.items", Count) : Format.Size(Size);
    public string ModifiedText => Dir ? "" : Format.DateTime(Modified);
}

/// <summary>Một file trong danh sách Mới cập nhật của môn. Path: đường dẫn tương đối trong thư mục học.</summary>
public sealed record RecentRow(string Name, string Folder, string Path, long Size, long Modified)
{
    public string SizeText => Format.Size(Size);
    public string ModifiedText => Format.DateTime(Modified);
}

public sealed record SubjectNews(string Title, string Forum, string Author, string When, string? Url);

/// <summary>Một mục điểm; Total là dòng tổng (của môn hay của nhóm), View in đậm.</summary>
public sealed record GradeRow(string Part, string Name, string GradeText, string MaxText, string Percent, bool Total);

/// <summary>
/// Trang Môn học hiện gì (cùng quy tắc với bản 1.x, Ui/Pages/SubjectsPage): môn từ LMS và thư mục trên máy gộp theo tên (so NFC);
/// mọi môn đều hiện (cả môn kỳ trước chỉ có trên LMS, dòng phụ ghi "Môn đã học"); môn đang học trước, rồi theo tên tiếng Việt.
/// </summary>
public static class SubjectsPresenter
{
    private static readonly StringComparer ViOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);

    public static IReadOnlyList<SubjectRow> Rows(LmsData? lms, JsonArray? scan)
    {
        var term = lms?.Term;
        var map = new Dictionary<string, (string Name, List<LmsCourse> Courses, int Files)>(NameMatch.NfcIgnoreCase);
        foreach (var c in lms?.Courses ?? [])
        {
            if (!map.TryGetValue(c.Subject, out var v)) v = (c.Subject, [], 0);
            v.Courses.Add(c);
            map[c.Subject] = v;
        }
        foreach (var d in (scan ?? []).OfType<JsonObject>())
        {
            var n = d["name"]!.GetValue<string>();
            var files = d["files"]?.GetValue<int>() ?? 0;
            map[n] = map.TryGetValue(n, out var v) ? v with { Files = files } : (n, [], files);
        }
        return [.. map.Values.Select(v =>
            {
                var codes = string.Join(", ", v.Courses.Select(c => c.Code.Split('_')[0]).Distinct());
                var docs = scan is null ? "" : L.F("subjects.docs", v.Files);   // chưa quét xong thì không ghi "0 tài liệu" sai
                return new SubjectRow(v.Name, Join(codes, docs), v.Courses.Any(c => c.Term == term), v.Courses, scan is null ? null : v.Files);
            })
            .OrderByDescending(r => r.Current).ThenBy(r => r.Name, ViOrder)];
    }

    /// <summary>Lọc bỏ dấu: gõ "giai tich" vẫn ra "Giải tích 2", tên NFD trên đĩa cũng khớp.</summary>
    public static IReadOnlyList<SubjectRow> Filter(IReadOnlyList<SubjectRow> rows, string q)
    {
        q = q.Trim();
        return q.Length == 0 ? rows : [.. rows.Where(r => NameMatch.ContainsFolded(r.Name, q) || NameMatch.ContainsFolded(r.Meta, q))];
    }

    /// <summary>
    /// Mốc thuộc môn: mốc LMS khớp theo id lớp, mốc khác (lịch thi MyBK) khớp đúng tên môn sau khi chuẩn hóa.
    /// Không dùng StartsWith: "Giải tích 1" khớp nhầm cả "Giải tích 12".
    /// </summary>
    public static bool Mine(TimelineItem x, SubjectRow s) =>
        x.Course is { } id ? s.Courses.Any(c => c.Id == id) : NameMatch.SubjectIs(x.Subject, s.Name);

    /// <summary>Dòng dưới tên môn: mã môn, số tài liệu, mốc gần nhất, tổng điểm LMS.</summary>
    public static string Meta(SubjectRow s, IReadOnlyList<TimelineItem> timeline, LmsData? lms, long now)
    {
        var next = timeline.FirstOrDefault(x => Mine(x, s) && x.Time > now && !x.Done && x.Kind != "class");
        var total = Books(lms, s).SelectMany(b => b.Items).FirstOrDefault(i => i.Kind == "course" && i.Grade is not null);
        return Join(
            string.Join(", ", s.Courses.Select(c => c.Code.Split('_')[0]).Distinct()),
            s.Files is { } n ? L.F("subjects.docs", n) : "",
            // Tên đã mở đầu bằng loại ("Quiz 3: ...") thì bỏ chữ loại để khỏi lặp "Quiz Quiz 3".
            next is null ? "" : L.F("subjects.next", next.Name.StartsWith(next.KindName, StringComparison.CurrentCultureIgnoreCase) ? "" : next.KindName, next.Name, next.Left).Trim(),
            total is null ? "" : L.F("subjects.lmsScore", Format.Score(total.Grade), Format.Score(total.Max)));
    }

    /// <summary>Tab Hạn nộp: mọi mốc sắp tới của môn (không cắt theo số ngày) và hạn LMS đã qua mà chưa làm.</summary>
    public static IReadOnlyList<CalendarRow> Due(IReadOnlyList<TimelineItem> timeline, SubjectRow s, long now) =>
        [.. timeline.Where(x => Mine(x, s) && x.Kind != "class" && (x.Time > now - 86400 || x.Overdue))
            .OrderBy(x => x.GroupRank).ThenBy(x => x.Time).Select(CalendarPresenter.Row)];

    public static IReadOnlyList<SubjectNews> News(LmsData? lms, SubjectRow s) =>
        [.. (lms?.Announcements ?? []).Where(a => NameMatch.Same(a.Subject, s.Name)).OrderByDescending(a => a.Time)
            .Select(a => new SubjectNews(a.Title, a.Forum, a.Author ?? "", Format.Ago(a.Time), a.Url))];

    public static IReadOnlyList<GradeRow> Grades(LmsData? lms, SubjectRow s) =>
        [.. Books(lms, s).SelectMany(b => b.Items.Select(i => new GradeRow(b.Part ?? L.T("common.theory"), i.Name, Format.Score(i.Grade),
            Format.Score(i.Max), i.Grade is null ? "" : i.Percent ?? "", i.Kind is "course" or "category")))];

    /// <summary>Một tầng thư mục (kết quả Documents.ListDir): thư mục trước, rồi file.</summary>
    public static IReadOnlyList<FileRow> FileRows(JsonObject dir) =>
        [.. (dir["dirs"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), true, L.T("files.folder"), 0, 0, x["count"]?.GetValue<int>() ?? 0))
            .Concat((dir["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), false,
                Format.FileKind(x["ext"]?.GetValue<string>() ?? ""), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0, 0)))];

    /// <summary>Danh sách Mới cập nhật (kết quả Documents.SubjectFiles).</summary>
    public static IReadOnlyList<RecentRow> RecentRows(JsonObject recent) =>
        [.. (recent["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new RecentRow(x["name"]!.GetValue<string>(),
            x["folder"]?.GetValue<string>() is { } fd && fd != "." ? fd : "", x["path"]!.GetValue<string>(), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0))];

    private static IEnumerable<LmsGradeBook> Books(LmsData? lms, SubjectRow s) => (lms?.Grades ?? []).Where(b => NameMatch.Same(b.Subject, s.Name));

    private static string Join(params string[] parts) => string.Join(", ", parts.Where(x => x.Length > 0));
}
