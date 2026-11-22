using System.Text.RegularExpressions;

namespace SoHocTap.Library;

public sealed record LibraryName(string? Vi, string? En);

public sealed record LibraryFaculty(string Key, LibraryName? Name);

public sealed record LibraryCounts(int Courses, int Items);

public sealed record CourseRef(string Id, string Code, string Name, string? NameEn, double? Credits, string? Faculty,
    List<string>? Aliases, List<string>? OldNames, string? Status, string? ReplacedBy, int Items, string? Url, string? Detail)
{
    public bool Retired => string.Equals(Status, "retired", StringComparison.OrdinalIgnoreCase);

    public string? Edition => Id.Length > 5 && Id[^5] == '-' && Id[^4..].All(char.IsAsciiDigit) ? Id[^4..] : null;
}

public sealed record LibraryIndex(int SchemaVersion, string? Generated, string? Site, LibraryCounts? Counts,
    List<LibraryFaculty>? Faculties, List<CourseRef>? Courses);

public sealed record FileRef(string Name, long Size, string Sha256, List<string>? Urls, string? Mime = null)
{
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

public sealed record LibraryItem(string Id, string Type, string? Title, string? Description, string? Lang, string? Term, string? Teacher,
    string? ExamKind, string? Chapter, string? Lab, string? License, string? Origin, string? Source, List<string>? Authors, string? Added,
    string? Updated, bool Example, bool Removed, string? RemovedReason, string? Url, List<FileRef>? Files, BookRef? Book = null)
{
    public bool IsLink => Type == LibraryTypes.Link;
    public bool IsBook => Type == LibraryTypes.BookRef;
    public bool IsQuizPack => Type == LibraryTypes.QuizPack;
}

public sealed record BookRef(string? Title, List<string>? Authors, int? Year, string? Publisher, string? Isbn);

public sealed record CourseDetail(int SchemaVersion, string Id, string? Code, string? Name, List<LibraryItem>? Items);

public static class LibraryTypes
{
    public const string Link = "link";
    public const string BookRef = "book-ref";
    public const string QuizPack = "quiz-pack";
    public const string Other = "other";

    private static readonly string[] KnownTypes =
    [
        "summary", "notes", "cheatsheet", "quiz-pack", "tips",
        "exercise-solution", "exam-solution", "exam-past",
        "prelab-template", "prelab-reference", "lab-report-reference", "project-reference",
        "link", "book-ref",
    ];

    public static IReadOnlyList<string> Known => KnownTypes;

    public static string GroupOf(string? type) => type is not null && KnownTypes.Contains(type) ? type : Other;

    public static int Order(string? type)
    {
        var i = type is null ? -1 : Array.IndexOf(KnownTypes, type);
        return i < 0 ? KnownTypes.Length : i;
    }

    public static readonly IReadOnlyList<string> ExamKinds = ["gk", "ck", "quiz", "kt"];
}

public static partial class LibraryJson
{
    [GeneratedRegex("^[A-Z0-9_]{3,12}(-[0-9]{4})?$")] public static partial Regex CourseId();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,127}$")] public static partial Regex ItemId();

    [GeneratedRegex("^[0-9a-fA-F]{64}$")] private static partial Regex Sha256Hex();

    public const int SchemaVersion = 1;

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
        Book = i.Book is { } b ? b with { Authors = Strings(b.Authors) } : null,
        Title = i.Title ?? i.Book?.Title,
        Files = Clean(i.Files).Where(f => f.Name is { Length: > 0 } && f.Sha256 is not null && Sha256Hex().IsMatch(f.Sha256))
            .Select(f => f with { Urls = Strings(f.Urls) }).Where(f => f.Urls!.Count > 0).ToList(),
    };

    private static List<T> Clean<T>(List<T>? list) where T : class => list is null ? [] : [.. list.Where(x => x is not null)];

    private static List<string> Strings(List<string>? list) => list is null ? [] : [.. list.Where(s => !string.IsNullOrWhiteSpace(s))];
}
