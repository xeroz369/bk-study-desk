using System.Text.RegularExpressions;
using SoHocTap.Data;

namespace SoHocTap.Library;

// Hợp đồng /v1 của thư viện (repo bk-study-library). Mọi tên field JSON chỉ nằm ở file này và LibraryClient.cs:
// thư viện đổi hợp đồng thì chỉ sửa hai file này. Đọc bằng DataJson.Options (chịu lỗi, không phân biệt hoa thường) như lms.json.
// Luật chung của hợp đồng: field tùy chọn không có thì vắng hẳn (không phải null), field lạ thì bỏ qua, ngày dạng YYYY-MM-DD.

public sealed record LibraryName(string? Vi, string? En);

public sealed record LibraryFaculty(string Key, LibraryName? Name);

public sealed record LibraryCounts(int Courses, int Items);

/// <summary>Một môn trong index. <c>Detail</c> là đường dẫn tương đối so với thư mục v1/ (vd. "courses/MT1005.json").</summary>
public sealed record CourseRef(string Id, string Code, string Name, string? NameEn, double? Credits, string? Faculty,
    List<string>? Aliases, List<string>? OldNames, string? Status, string? ReplacedBy, int Items, string? Url, string? Detail)
{
    public bool Retired => string.Equals(Status, "retired", StringComparison.OrdinalIgnoreCase);

    /// <summary>Năm khóa khi mã bị dùng lại cho môn khác (id "GE4169-2024" thành "2024"); null nếu id không có hậu tố năm.</summary>
    public string? Edition => Id.Length > 5 && Id[^5] == '-' && Id[^4..].All(char.IsAsciiDigit) ? Id[^4..] : null;
}

public sealed record LibraryIndex(int SchemaVersion, string? Generated, string? Site, LibraryCounts? Counts,
    List<LibraryFaculty>? Faculties, List<CourseRef>? Courses);

/// <summary>Một file của mục: thử lần lượt <c>Urls</c>, kiểm <c>Sha256</c> (hex) sau khi tải. <c>Mime</c> tùy chọn (thêm sau, có thể vắng).</summary>
public sealed record FileRef(string Name, long Size, string Sha256, List<string>? Urls, string? Mime = null)
{
    /// <summary>Loại file theo mime, không có mime thì theo đuôi tên file.</summary>
    public LibraryFileKind Kind => Mime?.Split(';')[0].Trim().ToLowerInvariant() switch
    {
        "application/pdf" => LibraryFileKind.Pdf,
        "text/markdown" or "text/x-markdown" => LibraryFileKind.Markdown,
        "application/zip" or "application/x-zip-compressed" => LibraryFileKind.Zip,
        "application/json" => LibraryFileKind.Json,
        { Length: > 0 } => LibraryFileKind.Other,
        _ => Path.GetExtension(Name ?? "").ToLowerInvariant() switch
        {
            ".pdf" => LibraryFileKind.Pdf,
            ".md" or ".markdown" => LibraryFileKind.Markdown,
            ".zip" => LibraryFileKind.Zip,
            ".json" => LibraryFileKind.Json,
            _ => LibraryFileKind.Other,
        },
    };
}

public enum LibraryFileKind { Other, Pdf, Markdown, Zip, Json }

/// <summary>
/// Một mục tài liệu. Mục đã gỡ (<c>Removed</c>) chỉ còn id, type, removed, removedReason, added: app ẩn nó và xóa bản app đã tải.
/// Mục loại "link" có <c>Url</c> và không có file; loại khác có <c>Files</c>. <c>Updated</c> (YYYY-MM-DD, mục còn hiệu lực luôn có,
/// mặc định bằng added) là ngày file đổi gần nhất: bản đã tải cũ hơn thì báo có bản mới.
/// </summary>
public sealed record LibraryItem(string Id, string Type, string? Title, string? Description, string? Lang, string? Term, string? Teacher,
    string? ExamKind, string? Chapter, string? Lab, string? License, string? Origin, string? Source, List<string>? Authors, string? Added,
    string? Updated, bool Example, bool Removed, string? RemovedReason, string? Url, List<FileRef>? Files)
{
    public bool IsLink => Type == LibraryTypes.Link;
    public bool IsQuizPack => Type == LibraryTypes.QuizPack;
}

public sealed record CourseDetail(int SchemaVersion, string Id, string? Code, string? Name, List<LibraryItem>? Items);

/// <summary>Loại mục mà app biết. Loại lạ vẫn hiện, trong nhóm "Khác" (<see cref="Other"/>).</summary>
public static class LibraryTypes
{
    public const string Link = "link";
    public const string QuizPack = "quiz-pack";
    public const string Other = "other";

    /// <summary>Thứ tự nhóm trên tab Thư viện. Nhãn ở lang/*.json: library.type.&lt;loại&gt;.</summary>
    private static readonly string[] KnownTypes =
    [
        "summary", "notes", "cheatsheet", "quiz-pack", "tips",
        "exercise-solution", "exam-solution", "exam-past",
        "prelab-template", "prelab-reference", "lab-report-reference", "project-reference",
        "link",
    ];

    public static IReadOnlyList<string> Known => KnownTypes;

    /// <summary>Loại dùng để nhóm: loại lạ thành <see cref="Other"/>.</summary>
    public static string GroupOf(string? type) => type is not null && KnownTypes.Contains(type) ? type : Other;

    /// <summary>Thứ tự nhóm (loại lạ đứng cuối).</summary>
    public static int Order(string? type)
    {
        var i = type is null ? -1 : Array.IndexOf(KnownTypes, type);
        return i < 0 ? KnownTypes.Length : i;
    }

    /// <summary>Loại bài kiểm tra mà app có nhãn (library.exam.&lt;loại&gt;); loại lạ hiện nguyên chữ.</summary>
    public static readonly IReadOnlyList<string> ExamKinds = ["gk", "ck", "quiz", "kt"];
}

/// <summary>Đọc index và chi tiết môn, bỏ phần không dùng được (thiếu id, id sai mẫu) thay vì làm hỏng cả file.</summary>
public static partial class LibraryJson
{
    /// <summary>Id môn: MT1005, GE4169-2024. Dùng làm tên file cache nên phải chặn chặt.</summary>
    [GeneratedRegex("^[A-Z0-9_]{3,12}(-[0-9]{4})?$")] public static partial Regex CourseId();

    /// <summary>Id mục: slug chữ thường, số, gạch ngang.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,127}$")] public static partial Regex ItemId();

    [GeneratedRegex("^[0-9a-fA-F]{64}$")] private static partial Regex Sha256Hex();

    public const int SchemaVersion = 1;

    /// <exception cref="DataReadException">JSON hỏng, hoặc schemaVersion không phải 1.</exception>
    public static LibraryIndex ParseIndex(string json)
    {
        var x = DataJson.Parse<LibraryIndex>(json) ?? throw new DataReadException("index rỗng");
        if (x.SchemaVersion != SchemaVersion) throw new DataReadException($"schemaVersion {x.SchemaVersion}, app chỉ đọc {SchemaVersion}");
        return x with
        {
            Faculties = Clean(x.Faculties).Where(f => f.Key is { Length: > 0 }).ToList(),
            Courses = Clean(x.Courses).Where(c => c.Id is not null && CourseId().IsMatch(c.Id))
                .Select(c => c with { Code = c.Code ?? c.Id, Name = c.Name ?? c.Id, Aliases = Strings(c.Aliases), OldNames = Strings(c.OldNames) })
                .ToList(),
        };
    }

    /// <exception cref="DataReadException">JSON hỏng, schemaVersion lạ, hoặc id môn sai mẫu.</exception>
    public static CourseDetail ParseCourse(string json)
    {
        var x = DataJson.Parse<CourseDetail>(json) ?? throw new DataReadException("chi tiết môn rỗng");
        if (x.SchemaVersion != SchemaVersion) throw new DataReadException($"schemaVersion {x.SchemaVersion}, app chỉ đọc {SchemaVersion}");
        if (x.Id is null || !CourseId().IsMatch(x.Id)) throw new DataReadException($"id môn không hợp lệ: {x.Id}");
        return x with { Items = Clean(x.Items).Where(i => i.Id is not null && ItemId().IsMatch(i.Id)).Select(CleanItem).ToList() };
    }

    private static LibraryItem CleanItem(LibraryItem i) => i with
    {
        Type = i.Type ?? LibraryTypes.Other,
        Updated = i.Updated ?? i.Added,
        Authors = Strings(i.Authors),
        // File thiếu tên, thiếu sha256 hay không có url thì không tải được: bỏ khỏi danh sách thay vì để nút Tải về báo lỗi.
        Files = Clean(i.Files).Where(f => f.Name is { Length: > 0 } && f.Sha256 is not null && Sha256Hex().IsMatch(f.Sha256))
            .Select(f => f with { Urls = Strings(f.Urls) }).Where(f => f.Urls!.Count > 0).ToList(),
    };

    private static List<T> Clean<T>(List<T>? list) where T : class => list is null ? [] : [.. list.Where(x => x is not null)];

    private static List<string> Strings(List<string>? list) => list is null ? [] : [.. list.Where(s => !string.IsNullOrWhiteSpace(s))];
}
