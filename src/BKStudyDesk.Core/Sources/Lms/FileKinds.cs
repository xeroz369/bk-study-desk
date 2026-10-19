namespace SoHocTap.Sources.Lms;

/// <summary>Loại file để người dùng chọn tải: PDF, slide, video (bài giảng ghi lại, nặng nhất, không tự tải), còn lại.</summary>
public enum FileKind { Pdf, Slide, Other, Video }

/// <summary>Xếp loại file LMS theo đuôi tên. Hàm thuần (LmsSource, LmsMedia, cửa sổ Tải dùng chung).</summary>
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
