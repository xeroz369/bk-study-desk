using System.Text.Json.Nodes;
using SoHocTap.Api;

namespace SoHocTap.Library;

/// <summary>Cài gói quiz từ thư viện đi đường nào.</summary>
public enum HandOffKind
{
    /// <summary>Gói .json mới: app cài thẳng qua đường POST /api/packs (PackImport + ApiRouter.InstallPack).</summary>
    Install,
    /// <summary>
    /// .md, .zip, hay gói cùng id đã cài: mở Luyện tập > Nhập file... (khung Luyện tập đọc Study Markdown, giải zip, và khi cập nhật
    /// thì chuyển kết quả cũ sang câu mới theo fingerprint). App không viết lại các bước đó bằng C#.
    /// </summary>
    PracticeImport,
    /// <summary>File không phải gói luyện tập dùng được.</summary>
    Invalid,
}

public sealed record HandOff(HandOffKind Kind, JsonObject? Pack, string? Error);

/// <summary>Quyết định cho nút "Cài vào Luyện tập" của mục quiz-pack (file Study Pack v1 đã tải và đã kiểm sha256).</summary>
public static class QuizPackHandOff
{
    /// <param name="installed">đã có gói cùng id trong content/packs chưa (ApiRouter.PackInstalled).</param>
    public static HandOff Decide(string path, Func<string, bool> installed)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".md" or ".zip") return new(HandOffKind.PracticeImport, null, null);
        if (ext != ".json") return new(HandOffKind.Invalid, null, "không phải gói luyện tập (.json, .md, .zip)");
        string text;
        try
        {
            if (new FileInfo(path).Length > PackImport.MaxBytes) return new(HandOffKind.Invalid, null, "gói quá 20 MB");
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(HandOffKind.Invalid, null, e.Message); }
        if (PackImport.Check(text, out var pack) is { } error) return new(HandOffKind.Invalid, null, error);
        return installed(pack!["id"]!.GetValue<string>()) ? new(HandOffKind.PracticeImport, pack, null) : new(HandOffKind.Install, pack, null);
    }
}
