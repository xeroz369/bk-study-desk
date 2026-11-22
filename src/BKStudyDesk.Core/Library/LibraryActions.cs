namespace SoHocTap.Library;

public enum LibraryAction { OpenWeb, Install, Open, Download }

public static class LibraryActions
{
    public static LibraryAction Primary(LibraryItem item, ItemLocal local)
    {
        if (item.IsLink || item.IsBook) return LibraryAction.OpenWeb;
        var kind = item.Files is [var first, ..] ? first.Kind : LibraryFileKind.Other;
        if (item.IsQuizPack && kind is LibraryFileKind.Markdown or LibraryFileKind.Zip or LibraryFileKind.Json) return LibraryAction.Install;
        if (local == ItemLocal.Outdated) return LibraryAction.Download;
        if (kind is LibraryFileKind.Pdf or LibraryFileKind.Markdown) return LibraryAction.Open;
        return local == ItemLocal.Downloaded ? LibraryAction.Open : LibraryAction.Download;
    }

    public static bool OpensAfterDownload(LibraryItem item) =>
        item.Files is [var first, ..] && first.Kind is LibraryFileKind.Pdf or LibraryFileKind.Markdown;
}
