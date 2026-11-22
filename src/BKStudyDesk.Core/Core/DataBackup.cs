using System.IO.Compression;
using System.Text;

namespace SoHocTap.Core;

public static class DataBackup
{
    public static readonly string[] Files =
    [
        "config.json", "mybk.json", "lms.json", "lms-courses.json", "lms-files.json", "lms-groups.json", "lms-pages.json",
        "lms-submissions.json", "ket-qua.json", "custom-events.json", "doc-read.json", "library-downloads.json", "notified-custom.json",
    ];

    public static readonly string[] Folders = ["lms-course", "lms-quiz", "packs"];

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
