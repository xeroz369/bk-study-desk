namespace SoHocTap.Sources.Lms;

public enum FileKind { Pdf, Slide, Other, Video }

public static class FileKinds
{
    public static FileKind Of(string? fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
    {
        ".pdf" => FileKind.Pdf,
        ".ppt" or ".pptx" or ".pps" or ".ppsx" or ".odp" or ".key" => FileKind.Slide,
        ".mp4" or ".m4v" or ".mkv" or ".mov" or ".avi" or ".webm" or ".wmv" or ".flv" => FileKind.Video,
        _ => FileKind.Other,
    };
}
