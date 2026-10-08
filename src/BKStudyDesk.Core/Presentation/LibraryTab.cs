using System.Globalization;
using SoHocTap.Data;
using SoHocTap.Files;
using SoHocTap.Library;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>Một mục thư viện trên bảng. Group là nhãn loại (nhóm của DataGrid), GroupOrder giữ thứ tự nhóm.</summary>
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
    public override string ToString() => Title;
}

/// <summary>Một lựa chọn của bộ lọc; Value null là "tất cả".</summary>
public sealed record FilterOption(string? Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Tab Thư viện của trang Môn học (cùng quy tắc với bản 1.x, Ui/Pages/SubjectsPage.Library): tài liệu chung của môn (repo
/// bk-study-library), nhóm theo loại, lọc theo giảng viên, học kỳ, loại kiểm tra (bộ lọc chỉ hiện khi có giá trị).
/// </summary>
public static class LibraryPresenter
{
    private static readonly StringComparer ViOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);

    /// <summary>Mã của môn: mã lớp LMS (phần trước "_"), mã trong thời khóa biểu và đăng ký môn trên MyBK (cùng tên môn).</summary>
    public static IEnumerable<string?> Codes(SubjectRow s, MybkData? mybk)
    {
        foreach (var c in s.Courses) yield return LibraryMatch.LmsCode(c.Code);
        foreach (var c in mybk?.Schedule ?? [])
            if (NameMatch.Same(c.Name, s.Name)) yield return c.Code;
        foreach (var r in mybk?.Registered ?? [])
            if (NameMatch.Same(r.Name, s.Name)) yield return r.Code;
    }

    /// <summary>Một dòng; <paramref name="state"/> là bản trên máy (LibraryService.LocalState, đọc đĩa nên View gọi trên thread nền).</summary>
    public static LibraryRow Row(CourseRef course, LibraryItem i, bool multi, ItemLocal state)
    {
        var type = LibraryTypes.GroupOf(i.Type);
        var chapter = i.Chapter is { Length: > 0 } ch ? L.F("library.chapter", ch) : i.Lab is { Length: > 0 } lab ? L.F("library.lab", lab) : "";
        var exam = i.ExamKind is { Length: > 0 } k ? LibraryTypes.ExamKinds.Contains(k) ? L.T("library.exam." + k) : k : "";
        var edition = multi ? course.Edition is { } y ? L.F("library.edition", y) : course.Id : "";
        return new LibraryRow(course, i, L.T("library.type." + type), LibraryTypes.Order(type), Shown(i), edition, i.Term ?? "", i.Teacher ?? "",
            chapter, exam, (i.Files ?? []).Sum(f => f.Size), state);
    }

    /// <summary>Thứ tự bảng: theo nhóm loại, học kỳ mới trước, rồi tên.</summary>
    public static IReadOnlyList<LibraryRow> Sort(IEnumerable<LibraryRow> rows) =>
        [.. rows.OrderBy(r => r.GroupOrder).ThenByDescending(r => r.Term, StringComparer.Ordinal).ThenBy(r => r.Title, ViOrder)];

    /// <summary>Tên hiện trên bảng. Mục mẫu có nhãn "(mẫu)" do app gắn, trừ khi tên đã tự ghi "mẫu", "ví dụ".</summary>
    public static string Shown(LibraryItem i)
    {
        var title = BookTitle(i) ?? i.Title ?? i.Id;
        if (!i.Example || title.Contains(L.T("library.example"), StringComparison.OrdinalIgnoreCase)
            || title.Contains("ví dụ", StringComparison.OrdinalIgnoreCase) || title.Contains("example", StringComparison.OrdinalIgnoreCase)) return title;
        return $"{title} ({L.T("library.example")})";
    }

    /// <summary>Sách tham khảo: "Tên sách (tác giả, năm, NXB)" để nhận ra sách mà không cần mở web.</summary>
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

    /// <summary>Ghi chú trên bảng: môn đã ngừng có môn thay thế, mã dùng chung nhiều môn, đang xem bản cache vì lỗi mạng.</summary>
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

    /// <summary>Lựa chọn của một bộ lọc: "tất cả" đứng đầu; không có giá trị nào thì chỉ có "tất cả" (View ẩn bộ lọc).</summary>
    public static IReadOnlyList<FilterOption> Options(IEnumerable<string> values, string allKey, bool newestFirst = false)
    {
        var distinct = values.Where(v => v.Length > 0).Distinct(StringComparer.Ordinal);
        var opts = (newestFirst ? distinct.OrderByDescending(v => v, StringComparer.Ordinal) : distinct.Order(ViOrder)).Select(v => new FilterOption(v, v)).ToList();
        opts.Insert(0, new FilterOption(null, L.T(allKey)));
        return opts;
    }

    public static IReadOnlyList<LibraryRow> Filter(IReadOnlyList<LibraryRow> rows, string? teacher, string? term, string? exam) =>
        [.. rows.Where(r => (teacher is null || r.Teacher == teacher) && (term is null || r.Term == term) && (exam is null || r.Exam == exam))];

    /// <summary>Chữ của lệnh trên menu và nút.</summary>
    public static string Label(LibraryAction a, LibraryRow r) => a switch
    {
        LibraryAction.OpenWeb => L.T("library.openWeb"),
        LibraryAction.Install => L.T("library.install"),
        LibraryAction.Open => L.T("library.openFile"),
        _ => L.T(r.State == ItemLocal.Outdated ? "library.downloadNew" : "library.download"),
    };
}
