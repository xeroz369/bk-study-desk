using System.Globalization;
using System.Text.Json.Nodes;
using SoHocTap.Files;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record SubjectRow(string Name, string Meta, bool Current, IReadOnlyList<LmsCourse> Courses, int? Files, string Term = "")
{
    public bool OnLms => Courses.Count > 0;
    public string ListSub => Meta;
    public override string ToString() => Name;
}

public sealed record SubjectTermHeader(string Title)
{
    public override string ToString() => Title;
}

public sealed record FileRow(string Name, bool Dir, string Kind, long Size, long Modified, int Count)
{
    public string SizeText => Dir ? L.F("subjects.items", Count) : Format.Size(Size);
    public string ModifiedText => Dir ? "" : Format.DateTime(Modified);
}

public sealed record RecentRow(string Name, string Folder, string Path, long Size, long Modified, bool Read = false)
{
    public string SizeText => Format.Size(Size);
    public string ModifiedText => Format.DateTime(Modified);
    public string ReadText => Read ? "" : L.T("subjects.unread");
}

public sealed record SubjectNews(string Title, string Forum, string Author, string When, string? Url, Reading? Read = null);

public sealed record MediaRow(string Name, string KindText, Sources.Lms.MediaKind Kind, string Group, string Url, bool Seen = false)
{
    public string SeenText => Seen ? "" : L.T("subjects.media.unseen");
}

public sealed record GradeRow(string Part, string Name, string GradeText, string MaxText, string Percent, bool Total);

public sealed record SubjectOverview(IReadOnlyList<CalendarRow> Due, IReadOnlyList<SubjectNews> News, IReadOnlyList<string> Grades);

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
                var docs = scan is null ? "" : L.F("subjects.docs", v.Files);
                var latest = v.Courses.Select(c => c.Term).Where(t => t.Length > 0).DefaultIfEmpty("").Max(StringComparer.Ordinal)!;
                return new SubjectRow(v.Name, Join(codes, docs), v.Courses.Any(c => c.Term == term), v.Courses, scan is null ? null : v.Files, latest);
            })
            .OrderByDescending(r => r.Current).ThenByDescending(r => r.Term, StringComparer.Ordinal).ThenBy(r => r.Name, ViOrder)];
    }

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

    public const string AllTerms = "*";

    private static string TermKey(SubjectRow r) => r.Current ? "\u0001" : r.Term;

    public static IReadOnlyList<(string Key, string Title)> TermOptions(IReadOnlyList<SubjectRow> rows) =>
        [.. rows.GroupBy(TermKey).Select(g => (g.Key, TermTitle(g.First().Term, g.First().Current, shortForm: true))),
            (AllTerms, L.F("subjects.termCount", L.T("subjects.termAll"), rows.Count))];

    public static IReadOnlyList<SubjectRow> InTerm(IReadOnlyList<SubjectRow> rows, string key) =>
        key == AllTerms ? rows : [.. rows.Where(r => TermKey(r) == key)];

    public static string TermOf(SubjectRow r) => TermKey(r);

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

    public static IReadOnlyList<SubjectRow> WithDue(IReadOnlyList<SubjectRow> rows, IReadOnlyList<TimelineItem> timeline, long now, int days) =>
        [.. rows.Select(r =>
        {
            var n = timeline.Count(x => Mine(x, r) && x.Kind != "class" && !x.Done && x.Time > now && x.Time <= now + days * 86400L);
            return n == 0 ? r : r with { Meta = Join(r.Meta, L.F("subjects.dueSoon", n, days)) };
        })];

    public static IReadOnlyList<SubjectRow> Filter(IReadOnlyList<SubjectRow> rows, string q)
    {
        q = q.Trim();
        return q.Length == 0 ? rows : [.. rows.Where(r => NameMatch.ContainsFolded(r.Name, q) || NameMatch.ContainsFolded(r.Meta, q))];
    }

    public static bool Mine(TimelineItem x, SubjectRow s) =>
        x.Course is { } id ? s.Courses.Any(c => c.Id == id) : NameMatch.SubjectIs(x.Subject, s.Name);

    public static string Meta(SubjectRow s, IReadOnlyList<TimelineItem> timeline, LmsData? lms, long now)
    {
        var next = timeline.FirstOrDefault(x => Mine(x, s) && x.Time > now && !x.Done && x.Kind != "class");
        var total = Books(lms, s).SelectMany(b => b.Items).FirstOrDefault(i => i.Kind == "course" && i.Grade is not null);
        return Join(
            string.Join(", ", s.Courses.Select(c => c.Code.Split('_')[0]).Distinct()),
            s.Files is { } n ? L.F("subjects.docs", n) : "",
            next is null ? "" : L.F("subjects.next", next.Name.StartsWith(next.KindName, StringComparison.CurrentCultureIgnoreCase) ? "" : next.KindName, Short(TodoGroups.Clean(next.Name)), next.Left).Trim(),
            total is null ? "" : L.F("subjects.lmsScore", Format.Score(total.Grade), Format.Score(total.Max)));
    }

    public static SubjectOverview Overview(IReadOnlyList<TimelineItem> timeline, LmsData? lms, MybkData? mybk, SubjectRow s, long now, int take = 3)
    {
        var opens = timeline.Where(t => t.Opens).Select(t => t.Id).ToHashSet();
        var due = Due(timeline, s, now).Where(r => r.State != L.T("timeline.done") && !opens.Contains(r.Id))
            .Take(take).Select(r => r with { Name = TodoGroups.Clean(r.Name) }).ToList();
        var news = News(lms, s).Take(take).ToList();
        var grades = Grades(lms, s).Where(g => g.Total && g.GradeText.Length > 0)
            .Select(g => L.F("subjects.overview.lmsGrade", g.Part, g.GradeText, g.MaxText)).ToList();
        foreach (var g in (mybk?.Grades ?? []).Where(g => g.Score is not null && NameMatch.Same(g.Name, s.Name)).OrderByDescending(g => g.Term).Take(1))
            grades.Add(L.F("subjects.overview.mybkGrade", Format.Score(g.Score!.Value), g.Letter ?? ""));
        return new(due, news, grades);
    }

    public static IReadOnlyList<CalendarRow> Due(IReadOnlyList<TimelineItem> timeline, SubjectRow s, long now) =>
        [.. timeline.Where(x => Mine(x, s) && x.Kind != "class" && (x.Time > now - 86400 || x.Overdue))
            .OrderBy(x => x.GroupRank).ThenBy(x => x.Time).Select(CalendarPresenter.Row)];

    public static IReadOnlyList<MediaRow> Media(SubjectRow s, Func<long, JsonObject?> read, ReadMarks? seen = null, Sources.Lms.MediaKind? only = null) =>
        [.. s.Courses.OrderByDescending(c => c.Term, StringComparer.Ordinal).ThenBy(c => c.Part is null ? 0 : 1)
            .SelectMany(c => Sources.Lms.LmsMedia.Items(read(c.Id)))
            .Where(i => only is null || i.Kind == only)
            .Select(i => new MediaRow(i.Name, L.T("subjects.media." + i.Kind.ToString().ToLowerInvariant()), i.Kind,
                Join(i.Part.Length > 0 ? i.Part : L.T("common.theory"), i.Section), i.Url, seen?.IsRead(MediaKey(i.Url), 0) == true))];

    public static string MediaKey(string url) => "media:" + url;

    public static IReadOnlyList<SubjectNews> News(LmsData? lms, SubjectRow s) =>
        [.. (lms?.Announcements ?? []).Where(a => NameMatch.Same(a.Subject, s.Name)).OrderByDescending(a => a.Time)
            .Select(a => new SubjectNews(a.Title, a.Forum, a.Author ?? "", Format.Ago(a.Time), a.Url, ReadingOf(a)))];

    public static Reading? PageReading(string url, string name, string subject)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !u.AbsolutePath.EndsWith("/mod/page/view.php", StringComparison.OrdinalIgnoreCase)) return null;
        var id = System.Web.HttpUtility.ParseQueryString(u.Query)["id"];
        if (!long.TryParse(id, out var cmid) || LmsPagesStore.Read(cmid) is not { } p) return null;
        var meta = p.Time is { } t ? $"{subject}, {L.F("reader.updated", Format.DateTime(t))}" : subject;
        return LmsReading.Of(p.Name.Length > 0 ? p.Name : name, meta, p.Html, null, url);
    }

    public static Reading? IntroReading(LmsEvent e) =>
        string.IsNullOrWhiteSpace(e.Intro) ? null
            : LmsReading.Of(e.Name, $"{e.Subject}, {L.F("reader.due", Format.DateTime(e.Time))}", e.Intro, null, e.Url);

    public static Reading ReadingOf(LmsAnnouncement a)
    {
        var main = LmsReading.Of(a.Title, string.Join(", ", new[] { a.Subject, a.Forum, a.Author ?? "", Format.DateTime(a.Time) }.Where(x => x.Length > 0)), a.Message, a.Attachments, a.Url);
        if (a.Posts is not { Count: > 0 } posts) return main;
        var paragraphs = main.Paragraphs.ToList();
        var blocks = (main.Blocks ?? []).ToList();
        var links = main.Links.ToList();
        var files = main.Attachments.ToList();
        foreach (var p in posts.Where(p => p is not null))
        {
            var head = L.F("reader.reply", p.Author ?? "", Format.DateTime(p.Time));
            paragraphs.Add(head);
            paragraphs.AddRange(LmsReading.Paragraphs(p.Message));
            blocks.Add(new ReadingBlock(head));
            blocks.AddRange(LmsReading.Blocks(p.Message));
            links.AddRange(LmsReading.Links(p.Message).Select(LmsReading.Browsable));
            files.AddRange((p.Attachments ?? []).Where(f => f is not null).Select(LmsReading.Browsable));
        }
        return main with { Paragraphs = paragraphs, Links = [.. links.DistinctBy(l => l.Url)], Attachments = files, Blocks = blocks };
    }

    public static IReadOnlyList<GradeRow> Grades(LmsData? lms, SubjectRow s) =>
        [.. Books(lms, s).SelectMany(b => b.Items.Select(i => new GradeRow(b.Part ?? L.T("common.theory"), i.Name, Format.Score(i.Grade),
            Format.Score(i.Max), i.Grade is null ? "" : i.Percent ?? "", i.Kind is "course" or "category")))];

    public static IReadOnlyList<FileRow> FileRows(JsonObject dir) =>
        [.. (dir["dirs"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), true, L.T("files.folder"), 0, 0, x["count"]?.GetValue<int>() ?? 0))
            .Concat((dir["files"] as JsonArray ?? []).OfType<JsonObject>().Select(x => new FileRow(x["name"]!.GetValue<string>(), false,
                Format.FileKind(x["ext"]?.GetValue<string>() ?? ""), x["size"]?.GetValue<long>() ?? 0, x["modified"]?.GetValue<long>() ?? 0, 0)))];

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

    private static string Short(string s) => s.Length <= 48 ? s : s[..45].TrimEnd() + "...";

    private static string Join(params string[] parts) => string.Join(", ", parts.Where(x => x.Length > 0));
}
