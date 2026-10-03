namespace SoHocTap.Library;

/// <summary>Lệnh chính (bấm đúp, Enter) của một mục thư viện.</summary>
public enum LibraryAction { OpenWeb, Install, Open, Download }

/// <summary>Chọn lệnh chính cho mục: hàm thuần theo loại mục, loại file (mime hoặc đuôi) và bản trên máy.</summary>
public static class LibraryActions
{
    /// <summary>
    /// link: Mở trên web. quiz-pack là .md, .zip, .json: Cài vào Luyện tập. PDF, Markdown: Mở (chưa tải thì tải rồi mở).
    /// Còn lại, hay đã tải mà thư viện có bản mới: Tải về.
    /// </summary>
    public static LibraryAction Primary(LibraryItem item, ItemLocal local)
    {
        if (item.IsLink) return LibraryAction.OpenWeb;
        var kind = item.Files is [var first, ..] ? first.Kind : LibraryFileKind.Other;
        if (item.IsQuizPack && kind is LibraryFileKind.Markdown or LibraryFileKind.Zip or LibraryFileKind.Json) return LibraryAction.Install;
        if (local == ItemLocal.Outdated) return LibraryAction.Download;
        if (kind is LibraryFileKind.Pdf or LibraryFileKind.Markdown) return LibraryAction.Open;
        return local == ItemLocal.Downloaded ? LibraryAction.Open : LibraryAction.Download;
    }

    /// <summary>Tải xong thì mở luôn (lệnh Mở với file chưa có trên máy).</summary>
    public static bool OpensAfterDownload(LibraryItem item) =>
        item.Files is [var first, ..] && first.Kind is LibraryFileKind.Pdf or LibraryFileKind.Markdown;
}
