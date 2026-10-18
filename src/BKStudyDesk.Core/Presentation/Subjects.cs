using System.Globalization;
using System.Text.Json.Nodes;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Files;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một môn trong danh sách bên trái. Files: số tài liệu trên máy (null = chưa quét xong).</summary>
/// <param name="Term">Học kỳ gần nhất có lớp LMS của môn (HK261); rỗng khi chỉ có thư mục trên máy.</param>
public sealed record SubjectRow(string Name, string Meta, bool Current, IReadOnlyList<LmsCourse> Courses, int? Files, string Term = "")
{
    public bool OnLms => Courses.Count > 0;
    /// <summary>Dòng phụ trong danh sách (học kỳ đã ghi ở dòng nhóm phía trên).</summary>
    public string ListSub => Meta;
    public override string ToString() => Name;   // tên cho trình đọc màn hình
}

/// <summary>Dòng nhóm học kỳ trong danh sách môn (không chọn được).</summary>
public sealed record SubjectTermHeader(string Title)
{
    public override string ToString() => Title;
}

/// <summary>Một dòng của tab Tài liệu (thư mục đứng trước file, như trình quản lý file).</summary>
public sealed record FileRow(string Name, bool Dir, string Kind, long Size, long Modified, int Count)
{
    public string SizeText => Dir ? L.F("subjects.items", Count) : Format.Size(Size);
    public string ModifiedText => Dir ? "" : Format.DateTime(Modified);
}

/// <summary>Một file trong danh sách Mới cập nhật của môn. Path: đường dẫn tương đối trong thư mục học.</summary>
/// <param name="Read">Đã mở hay đánh dấu đã đọc từ lần file đổi gần nhất (Files.ReadMarks).</param>
public sealed record RecentRow(string Name, string Folder, string Path, long Size, long Modified, bool Read = false)
{
    public string SizeText => Format.Size(Size);
    public string ModifiedText => Format.DateTime(Modified);
    public string ReadText => Read ? "" : L.T("subjects.unread");
}

public sealed record SubjectNews(string Title, string Forum, string Author, string When, string? Url);

/// <summary>Một dòng của tab Video và liên kết. Group: phần của lớp và mục (chương) trên LMS, là dải nhóm của bảng.</summary>
/// <param name="Seen">Đã mở (đánh dấu đã xem, data/doc-read.json khóa "media:" + url).</param>
public sealed record MediaRow(string Name, string KindText, Sources.Lms.MediaKind Kind, string Group, string Url, bool Seen = false)
{
    public string SeenText => Seen ? "" : L.T("subjects.media.unseen");
}

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
                var latest = v.Courses.Select(c => c.Term).Where(t => t.Length > 0).DefaultIfEmpty("").Max(StringComparer.Ordinal)!;
                return new SubjectRow(v.Name, Join(codes, docs), v.Courses.Any(c => c.Term == term), v.Courses, scan is null ? null : v.Files, latest);
            })
            .OrderByDescending(r => r.Current).ThenByDescending(r => r.Term, StringComparer.Ordinal).ThenBy(r => r.Name, ViOrder)];
    }

    /// <summary>
    /// Danh sách có dòng nhóm theo học kỳ gần nhất của môn: kỳ đang học trước, rồi các kỳ cũ mới trước, cuối cùng môn chỉ có thư mục trên máy.
    /// Môn học nhiều kỳ nằm ở kỳ gần nhất. Nhận danh sách đã lọc (Filter) và đã sắp (Rows).
    /// </summary>
    public static IReadOnlyList<object> Grouped(IReadOnlyList<SubjectRow> rows)
    {
        var list = new List<object>();
        string? last = null;
        foreach (var r in rows)
        {
            var key = TermKey(r);
            if (key != last) list.Add(new SubjectTermHeader(TermTitle(r.Term, r.Current)));
            last = key;
            list.Add(r);
        }
        return list;
    }

    /// <summary>Lựa chọn "Tất cả học kỳ" trong ô chọn kỳ của trang Môn học.</summary>
    public const string AllTerms = "*";

    /// <summary>Kỳ của môn trong danh sách: kỳ đang học, kỳ gần nhất có lớp LMS, hay rỗng (chỉ có thư mục trên máy).</summary>
    private static string TermKey(SubjectRow r) => r.Current ? "\u0001" : r.Term;

    /// <summary>
    /// Các lựa chọn ô chọn kỳ: kỳ đang học (nếu có), các kỳ cũ mới trước, môn chỉ có trên máy (nếu có), cuối cùng Tất cả học kỳ.
    /// Mỗi lựa chọn kèm số môn. Thứ tự giống Grouped.
    /// </summary>
    public static IReadOnlyList<(string Key, string Title)> TermOptions(IReadOnlyList<SubjectRow> rows) =>
        [.. rows.GroupBy(TermKey).Select(g => (g.Key, TermTitle(g.First().Term, g.First().Current, shortForm: true))),
            (AllTerms, L.F("subjects.termCount", L.T("subjects.termAll"), rows.Count))];

    /// <summary>Môn thuộc lựa chọn <paramref name="key"/> của ô chọn kỳ (AllTerms: mọi môn).</summary>
    public static IReadOnlyList<SubjectRow> InTerm(IReadOnlyList<SubjectRow> rows, string key) =>
        key == AllTerms ? rows : [.. rows.Where(r => TermKey(r) == key)];

    /// <summary>Lựa chọn của một môn (để chuyển ô chọn kỳ khi mở môn từ chỗ khác, ví dụ Ctrl+K).</summary>
    public static string TermOf(SubjectRow r) => TermKey(r);

    /// <summary>
    /// "HK261" thành "Học kỳ 1 năm học 2026-2027" (dòng nhóm), hay "HK1 2026-2027" khi <paramref name="shortForm"/> (ô chọn kỳ hẹp);
    /// kỳ 3 là học kỳ hè; rỗng là môn chỉ có trên máy.
    /// </summary>
    public static string TermTitle(string term, bool current = false, bool shortForm = false)
    {
        if (term.Length != 5 || !term.StartsWith("HK", StringComparison.Ordinal) || !int.TryParse(term.AsSpan(2, 2), out var yy) || term[4] is < '1' or > '3')
            return term.Length == 0 ? L.T(shortForm ? "subjects.termLocalShort" : "subjects.termLocal") : term;
        var years = $"{2000 + yy}-{2001 + yy}";
        var summer = term[4] == '3';
        var title = shortForm ? (summer ? L.F("subjects.termSummerShort", years) : L.F("subjects.termShort", term[4], years))
            : summer ? L.F("subjects.termSummer", years) : L.F("subjects.term", term[4], years);
        return current ? L.F("subjects.termCurrent", title) : title;
    }

    /// <summary>
    /// Thanh đường dẫn tab Tài liệu: từ thư mục môn (<paramref name="root"/>, hiện bằng tên môn) xuống thư mục đang xem; mỗi cấp kèm
    /// đường dẫn để bấm lên thẳng cấp đó. Thư mục ngoài môn thì chỉ có một cấp là chính nó.
    /// </summary>
    public static IReadOnlyList<(string Name, string Path)> Crumbs(string root, string dir)
    {
        var name = root[(root.LastIndexOf('/') + 1)..];
        if (!dir.StartsWith(root, StringComparison.OrdinalIgnoreCase) || (dir.Length > root.Length && dir[root.Length] != '/'))
            return [(dir[(dir.LastIndexOf('/') + 1)..], dir)];
        var list = new List<(string, string)> { (name, root) };
        var path = root;
        foreach (var part in dir[root.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            path += "/" + part;
            list.Add((part, path));
        }
        return list;
    }

    /// <summary>
    /// Dòng phụ của môn trong danh sách thêm số hạn chưa xong trong <paramref name="days"/> ngày tới (bài tập, quiz, thi; không tính buổi học),
    /// để nhìn danh sách là biết môn nào gấp. Môn không có hạn thì giữ nguyên.
    /// </summary>
    public static IReadOnlyList<SubjectRow> WithDue(IReadOnlyList<SubjectRow> rows, IReadOnlyList<TimelineItem> timeline, long now, int days) =>
        [.. rows.Select(r =>
        {
            var n = timeline.Count(x => Mine(x, r) && x.Kind != "class" && !x.Done && x.Time > now && x.Time <= now + days * 86400L);
            return n == 0 ? r : r with { Meta = Join(r.Meta, L.F("subjects.dueSoon", n, days)) };
        })];

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

    /// <summary>
    /// Tab Video và liên kết: video bài giảng, link ngoài, trang, công cụ ngoài của mọi lớp của môn (cả kho chung), lớp kỳ mới trước.
    /// <paramref name="read"/> đọc cấu trúc khóa đã lưu khi đồng bộ (LmsCourseStore.Read); lớp chưa đọc thì bỏ qua.
    /// </summary>
    public static IReadOnlyList<MediaRow> Media(SubjectRow s, Func<long, JsonObject?> read, ReadMarks? seen = null, Sources.Lms.MediaKind? only = null) =>
        [.. s.Courses.OrderByDescending(c => c.Term, StringComparer.Ordinal).ThenBy(c => c.Part is null ? 0 : 1)
            .SelectMany(c => Sources.Lms.LmsMedia.Items(read(c.Id)))
            .Where(i => only is null || i.Kind == only)
            .Select(i => new MediaRow(i.Name, L.T("subjects.media." + i.Kind.ToString().ToLowerInvariant()), i.Kind,
                Join(i.Part.Length > 0 ? i.Part : L.T("common.theory"), i.Section), i.Url, seen?.IsRead(MediaKey(i.Url), 0) == true))];

    /// <summary>Khóa đánh dấu đã xem của một mục (dùng chung ReadMarks với tài liệu, tiền tố riêng để không lẫn đường dẫn file).</summary>
    public static string MediaKey(string url) => "media:" + url;

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

    /// <summary>Danh sách Mới cập nhật (kết quả Documents.SubjectFiles); <paramref name="unreadOnly"/> chỉ giữ file chưa đọc.</summary>
    public static IReadOnlyList<RecentRow> RecentRows(JsonObject recent, ReadMarks? marks = null, bool unreadOnly = false) =>
        [.. (recent["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x =>
            {
                var path = x["path"]!.GetValue<string>();
                var modified = x["modified"]?.GetValue<long>() ?? 0;
                return new RecentRow(x["name"]!.GetValue<string>(), x["folder"]?.GetValue<string>() is { } fd && fd != "." ? fd : "", path,
                    x["size"]?.GetValue<long>() ?? 0, modified, marks?.IsRead(path, modified) == true);
            })
            .Where(r => !unreadOnly || !r.Read)];

    private static IEnumerable<LmsGradeBook> Books(LmsData? lms, SubjectRow s) => (lms?.Grades ?? []).Where(b => NameMatch.Same(b.Subject, s.Name));

    private static string Join(params string[] parts) => string.Join(", ", parts.Where(x => x.Length > 0));
}
