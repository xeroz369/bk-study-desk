using System.Globalization;
using SoHocTap.Files;
using SoHocTap.Library;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record LibraryRow(CourseRef Course, LibraryItem Item, string Group, int GroupOrder, string Title, string Edition, string Term,
    string Teacher, string Chapter, string Exam, long Size, ItemLocal State)
{
    public string SizeText => Size > 0 ? Format.Size(Size) : "";
    public string Local => State switch
    {
        ItemLocal.Downloaded => L.T("library.local.downloaded"),
        ItemLocal.Outdated => L.T("library.local.outdated"),
        _ => "",
    };
    public bool HasLocal => State == ItemLocal.Downloaded;
    public bool CanDownload => State != ItemLocal.Downloaded && Item.Files is { Count: > 0 };
    public bool WebOnly => State != ItemLocal.Downloaded && Item.Files is not { Count: > 0 };
    public string ActionTip => State switch
    {
        ItemLocal.Downloaded => L.T("library.tip.open"),
        ItemLocal.Outdated => L.T("library.tip.update"),
        _ => Item.Files is { Count: > 0 } ? L.T("library.tip.download") : L.T("library.tip.web"),
    };
    public override string ToString() => Title;
}

public sealed record FilterOption(string? Value, string Label)
{
    public override string ToString() => Label;
}

public static class LibraryPresenter
{
    private static readonly StringComparer ViOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);

    public static IEnumerable<string?> Codes(SubjectRow s, MybkData? mybk)
    {
        foreach (var c in s.Courses) yield return LibraryMatch.LmsCode(c.Code);
        foreach (var c in mybk?.Schedule ?? [])
            if (NameMatch.Same(c.Name, s.Name)) yield return c.Code;
        foreach (var r in mybk?.Registered ?? [])
            if (NameMatch.Same(r.Name, s.Name)) yield return r.Code;
    }

    public static LibraryRow Row(CourseRef course, LibraryItem i, bool multi, ItemLocal state)
    {
        var type = LibraryTypes.GroupOf(i.Type);
        var chapter = i.Chapter is { Length: > 0 } ch ? L.F("library.chapter", ch) : i.Lab is { Length: > 0 } lab ? L.F("library.lab", lab) : "";
        var exam = i.ExamKind is { Length: > 0 } k ? LibraryTypes.ExamKinds.Contains(k) ? L.T("library.exam." + k) : k : "";
        var edition = multi ? course.Edition is { } y ? L.F("library.edition", y) : course.Id : "";
        return new LibraryRow(course, i, L.T("library.type." + type), LibraryTypes.Order(type), Shown(i), edition, i.Term ?? "", i.Teacher ?? "",
            chapter, exam, (i.Files ?? []).Sum(f => f.Size), state);
    }

    public static IReadOnlyList<LibraryRow> Sort(IEnumerable<LibraryRow> rows) =>
        [.. rows.OrderBy(r => r.GroupOrder).ThenByDescending(r => r.Term, StringComparer.Ordinal).ThenBy(r => r.Title, ViOrder)];

    public static string Shown(LibraryItem i)
    {
        var title = BookTitle(i) ?? i.Title ?? i.Id;
        if (!i.Example || title.Contains(L.T("library.example"), StringComparison.OrdinalIgnoreCase)
            || title.Contains("ví dụ", StringComparison.OrdinalIgnoreCase) || title.Contains("example", StringComparison.OrdinalIgnoreCase)) return title;
        return $"{title} ({L.T("library.example")})";
    }

    public static string? BookTitle(LibraryItem i)
    {
        if (!i.IsBook || i.Book is not { } b) return null;
        var bits = new List<string>();
        if (b.Authors is { Count: > 0 } a) bits.Add(string.Join(", ", a));
        if (b.Year is { } y) bits.Add(y.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(b.Publisher)) bits.Add(b.Publisher!);
        var title = i.Title ?? b.Title ?? i.Id;
        return bits.Count == 0 ? title : $"{title} ({string.Join(", ", bits)})";
    }

    public static string Note(IReadOnlyList<CourseMatch> matches, string? staleError)
    {
        var notes = new List<string>();
        foreach (var m in matches.Where(m => m.ReplacementOf is not null))
            notes.Add(L.F("library.replacement", m.ReplacementOf!.Code, m.Course.Code));
        foreach (var g in matches.Where(m => m.ReplacementOf is null).GroupBy(m => LibraryMatch.Key(m.Course.Code)).Where(g => g.Count() > 1))
            notes.Add(L.F("library.sharedCode", g.First().Course.Code, g.Count()));
        if (staleError is not null) notes.Add(L.F("library.stale", staleError));
        return string.Join(" ", notes);
    }

    public static IReadOnlyList<FilterOption> Options(IEnumerable<string> values, string allKey, bool newestFirst = false)
    {
        var distinct = values.Where(v => v.Length > 0).Distinct(StringComparer.Ordinal);
        var opts = (newestFirst ? distinct.OrderByDescending(v => v, StringComparer.Ordinal) : distinct.Order(ViOrder)).Select(v => new FilterOption(v, v)).ToList();
        opts.Insert(0, new FilterOption(null, L.T(allKey)));
        return opts;
    }

    public static IReadOnlyList<LibraryRow> Filter(IReadOnlyList<LibraryRow> rows, string? teacher, string? term, string? exam) =>
        [.. rows.Where(r => (teacher is null || r.Teacher == teacher) && (term is null || r.Term == term) && (exam is null || r.Exam == exam))];

    public static string Label(LibraryAction a, LibraryRow r) => a switch
    {
        LibraryAction.OpenWeb => L.T("library.openWeb"),
        LibraryAction.Install => L.T("library.install"),
        LibraryAction.Open => L.T("library.openFile"),
        _ => L.T(r.State == ItemLocal.Outdated ? "library.downloadNew" : "library.download"),
    };
}
