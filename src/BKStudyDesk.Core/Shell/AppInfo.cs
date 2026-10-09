using SoHocTap.Core;

namespace SoHocTap.Shell;

/// <summary>Tên và id của app.</summary>
internal static class AppInfo
{
    public const string Id = "BKStudyDesk";
    public const string Name = "BK Study Desk";

    /// <summary>Khóa chạy một instance: theo id và thư mục app, nên bản ở thư mục khác (bản demo, bản thử) chạy song song được.</summary>
    public static string InstanceKey { get; } =
        Id + "." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Paths.AppRoot.ToLowerInvariant())))[..8];

    // Credit: giữ nguyên khi fork hoặc build lại (giấy phép yêu cầu giữ dòng Required Notice ghi tác giả). Mục Giới thiệu đọc từ đây.
    public const string Author = "xeroz369";
    public const string Repo = "https://github.com/xeroz369/bk-study-desk";
    public const string Issues = Repo + "/issues";
    public const string License = "AGPL-3.0";
    public const string Support = "";

    /// <summary>Version của bản build (bỏ phần "+commit" mà SDK tự gắn).</summary>
    public static string Version { get; } =
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(System.Reflection.Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly)
            ?.InformationalVersion ?? "").Split('+')[0];
}
