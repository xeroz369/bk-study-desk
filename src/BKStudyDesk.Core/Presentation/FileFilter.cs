using SoHocTap.Files;

namespace SoHocTap.Presentation;

public enum FileGroup { All, Docs, Images, Other }

public static class FileFilter
{
    private static readonly HashSet<string> DocExt = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".md", ".odt", ".odp", ".rtf", ".csv", ".tex", ".ipynb" };
    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic", ".heif", ".tif", ".tiff", ".svg" };

    public static FileGroup GroupOf(string name)
    {
        var ext = Path.GetExtension(name);
        return DocExt.Contains(ext) ? FileGroup.Docs : ImageExt.Contains(ext) ? FileGroup.Images : FileGroup.Other;
    }

    public static bool Matches(string name, string folder, string query, FileGroup group)
    {
        if (group != FileGroup.All && GroupOf(name) != group) return false;
        var words = NameMatch.Fold(query.Trim()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;
        var hay = NameMatch.Fold(name + " " + folder);
        return words.All(w => hay.Contains(w, StringComparison.Ordinal));
    }

    public static IReadOnlyList<RecentRow> Apply(IEnumerable<RecentRow> rows, string query, FileGroup group) =>
        [.. rows.Where(r => Matches(r.Name, r.Folder, query, group))];

    public static IReadOnlyList<FileRow> Apply(IEnumerable<FileRow> rows, string query, FileGroup group) =>
        [.. rows.Where(r => r.Dir ? query.Trim().Length == 0 && group == FileGroup.All || Matches(r.Name, "", query, FileGroup.All) && group == FileGroup.All
                                  : Matches(r.Name, "", query, group))];
}
