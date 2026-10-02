namespace SoHocTap.Core;

/// <summary>
/// Chép dữ liệu từ bản zip cũ (thư mục data\ cạnh app) sang bản cài. Hàm thuần: chỉ chọn file nào cần chép.
/// Không chép hồ sơ trình duyệt (webview\: lớn, đang bị app giữ, đăng nhập lại là có), log, file tạm và trạng thái cập nhật
/// của bản kia. Token LMS (secrets\) chép được vì cùng tài khoản Windows trên cùng máy vẫn giải mã được.
/// </summary>
public static class DataImport
{
    private static readonly string[] SkipFolders = ["webview", "tmp"];
    private static readonly string[] SkipFiles = ["update-state.json"];

    /// <param name="relativeFiles">đường dẫn tương đối trong thư mục data\ của bản cũ</param>
    public static List<string> Plan(IEnumerable<string> relativeFiles) => relativeFiles.Where(f =>
    {
        var parts = f.Split('\\', '/');
        return !SkipFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
            && !f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
            && !SkipFiles.Contains(f, StringComparer.OrdinalIgnoreCase);
    }).ToList();
}
