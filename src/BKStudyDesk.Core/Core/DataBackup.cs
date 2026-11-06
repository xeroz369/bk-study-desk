using System.IO.Compression;
using System.Text;

namespace SoHocTap.Core;

/// <summary>
/// Sao lưu dữ liệu đã đồng bộ ra một file .zip (Cài đặt, mục Dữ liệu): điểm, lịch, CTĐT, thông báo, trang LMS, bài nộp, quiz đã lưu,
/// kết quả luyện tập, sự kiện tự thêm, cài đặt. Chỉ lấy theo danh sách cho phép, nên file mới thêm vào thư mục data không tự lọt vào.
/// Không kèm tin nhắn riêng (lms-messages.json, ảnh trong lms-media vì có ảnh của tin nhắn), phiên đăng nhập, token LMS (secrets),
/// cookie (webview), log. Hàm thuần theo thư mục gốc, có test.
/// </summary>
public static class DataBackup
{
    /// <summary>File ở gốc thư mục data được sao lưu.</summary>
    public static readonly string[] Files =
    [
        "config.json", "mybk.json", "lms.json", "lms-courses.json", "lms-files.json", "lms-groups.json", "lms-pages.json",
        "lms-submissions.json", "ket-qua.json", "custom-events.json", "doc-read.json", "library-downloads.json", "notified-custom.json",
    ];

    /// <summary>Thư mục con được sao lưu cả cây: nội dung lớp đã lưu, quiz đã lưu, gói luyện tập tự nhập.</summary>
    public static readonly string[] Folders = ["lms-course", "lms-quiz", "packs"];

    /// <summary>Đường dẫn tương đối (dấu /) của mọi file sẽ vào bản sao lưu, theo thứ tự.</summary>
    public static IReadOnlyList<string> Entries(string root)
    {
        var list = Files.Where(f => File.Exists(Path.Combine(root, f))).ToList();
        foreach (var dir in Folders)
        {
            var full = Path.Combine(root, dir);
            if (!Directory.Exists(full)) continue;
            list.AddRange(Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).Order(StringComparer.Ordinal));
        }
        return list;
    }

    /// <summary>
    /// Ghi bản sao lưu ra <paramref name="dest"/> (ghi file tạm rồi đổi tên). File đang bị ghi dở hay khóa thì bỏ qua và trả về trong Skipped.
    /// Kèm DOC-TRUOC.txt ghi phiên bản app, giờ sao lưu và phần không kèm.
    /// </summary>
    public static (int Count, IReadOnlyList<string> Skipped) Write(string root, string dest, string version, DateTime now)
    {
        var skipped = new List<string>();
        var count = 0;
        var tmp = dest + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var rel in Entries(root))
            {
                try
                {
                    using var src = new FileStream(Path.Combine(root, rel), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var dst = zip.CreateEntry(rel, CompressionLevel.Optimal).Open();
                    src.CopyTo(dst);
                    count++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped.Add(rel); }
            }
            using var w = new StreamWriter(zip.CreateEntry("DOC-TRUOC.txt").Open(), new UTF8Encoding(false));
            w.Write(Readme(version, now, count));
        }
        File.Move(tmp, dest, overwrite: true);
        return (count, skipped);
    }

    public static string Readme(string version, DateTime now, int count) =>
        $"BK Study Desk {version}, sao lưu lúc {now:dd/MM/yyyy HH:mm}, {count} file.\n" +
        "Có: điểm, lịch, chương trình đào tạo (MyBK), thông báo, trang, bài nộp, quiz đã lưu (LMS), kết quả luyện tập, sự kiện tự thêm, cài đặt.\n" +
        "Không có: tin nhắn riêng và ảnh, phiên đăng nhập, token LMS, cookie, log.\n";
}
