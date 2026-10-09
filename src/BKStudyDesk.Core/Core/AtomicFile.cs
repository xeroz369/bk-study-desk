using System.Text;

namespace SoHocTap.Core;

/// <summary>
/// Ghi file chữ an toàn: ghi ra file tạm rồi mới thay file thật (không bao giờ có file ghi dở), và bỏ qua nếu nội dung y như cũ.
/// Bỏ qua khi giống: một lượt đồng bộ không có gì mới thì không ghi đĩa (đỡ đánh thức ổ, đỡ OneDrive/antivirus quét lại,
/// mtime không đổi nên cache theo mtime vẫn trúng). Không phụ thuộc Log/Config để test được.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>File đang có đúng nội dung <paramref name="text"/> (UTF-8, không BOM) chưa.</summary>
    public static bool HasContent(string path, string text)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return false;
            var bytes = Utf8.GetBytes(text);
            if (info.Length != bytes.Length) return false;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var old = new byte[bytes.Length];
            fs.ReadExactly(old);
            return old.AsSpan().SequenceEqual(bytes);
        }
        // Không đọc được thì cứ ghi (ghi lỗi sẽ báo ở bước ghi).
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Ghi nếu khác; trả true nếu đã ghi. <paramref name="retry"/> chạy một thao tác có thử lại khi file bận (JsonStore cài).</summary>
    public static bool WriteIfChanged(string path, string text, Action<Action>? retry = null)
    {
        if (HasContent(path, text)) return false;
        retry ??= a => a();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        // Tên file tạm riêng cho mỗi lần ghi: hai thread cùng ghi một file (LMS và MyBK cùng ghi sync-state.json) mà dùng chung
        // "x.tmp" thì lần này ghi đè file tạm của lần kia, hoặc Move mất file tạm của nhau.
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            retry(() => File.WriteAllText(tmp, text, Utf8));
            retry(() => File.Move(tmp, path, overwrite: true));
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return true;
    }
}
