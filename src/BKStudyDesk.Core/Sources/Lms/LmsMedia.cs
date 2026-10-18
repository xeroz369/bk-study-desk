using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SoHocTap.Sources.Lms;

/// <summary>Loại mục ở tab Video và liên kết: video (file hay link xem được), link ngoài, trang chữ của LMS, công cụ ngoài (LTI).</summary>
public enum MediaKind { Video, Link, Page, Tool }

/// <param name="Part">Phần của lớp (Kho chung, Thí nghiệm...), rỗng là lớp lý thuyết.</param>
/// <param name="Url">Mở khi bấm: trang của mục trên LMS, hay link ngoài (YouTube...) khi LMS có.</param>
public sealed record MediaItem(string Part, string Section, string Name, MediaKind Kind, string Url);

/// <summary>
/// Những gì trên LMS không phải file tài liệu: video bài giảng (file mp4, link YouTube, video nhúng trong nhãn), link ngoài, trang, công cụ ngoài.
/// Đọc từ cấu trúc khóa đã lưu khi đồng bộ (data/lms-course/&lt;id&gt;.json, LmsSource.SaveStructure), không gọi mạng. Hàm thuần.
/// </summary>
public static partial class LmsMedia
{
    [GeneratedRegex(@"(?:href|src)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)] private static partial Regex LinkRx();

    [GeneratedRegex(@"youtube\.com|youtu\.be|vimeo\.com|drive\.google\.com/file|stream|\.mp4\b|video|record|bài giảng", RegexOptions.IgnoreCase)]
    private static partial Regex VideoHintRx();

    /// <summary>Link http(s) trong HTML của nhãn, bỏ link vào chính trang module của LMS (đã có sẵn ở mục khác) và link trùng.</summary>
    public static IEnumerable<string> LinksIn(string html) =>
        LinkRx().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim())
            .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !u.Contains("/pluginfile.php/", StringComparison.OrdinalIgnoreCase)
                        && !u.Contains("/theme/image.php", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<MediaItem> Items(JsonObject? course)
    {
        var list = new List<MediaItem>();
        if (course is null) return list;
        var part = course["part"]?.GetValue<string>() ?? "";
        foreach (var sec in (course["sections"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var section = sec["name"]?.GetValue<string>() ?? "";
            foreach (var mod in (sec["modules"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var name = mod["name"]?.GetValue<string>() ?? "";
                var url = mod["url"]?.GetValue<string>() ?? "";
                var files = (mod["files"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                switch (mod["type"]?.GetValue<string>())
                {
                    case "resource" or "folder" when files.Any(f => FileKinds.Of(f["name"]?.GetValue<string>()) == FileKind.Video):
                        list.Add(new(part, section, name, MediaKind.Video, url));
                        break;
                    case "url":
                        var target = files.Select(f => f["url"]?.GetValue<string>()).FirstOrDefault(u => !string.IsNullOrEmpty(u)) ?? url;
                        // Link sang một khóa LMS khác (lớp chính trỏ tới lớp kho chung "..._Video") là liên kết, không phải video.
                        var toCourse = target.Contains("/course/view.php", StringComparison.OrdinalIgnoreCase);
                        list.Add(new(part, section, name, !toCourse && VideoHintRx().IsMatch(name + " " + target) ? MediaKind.Video : MediaKind.Link, target));
                        break;
                    case "page":
                        list.Add(new(part, section, name, MediaKind.Page, url));
                        break;
                    case "lti":
                        list.Add(new(part, section, name, MediaKind.Tool, url));
                        break;
                    case "label":
                        var text = mod["text"]?.GetValue<string>() ?? "";
                        foreach (var link in (mod["links"] as JsonArray ?? []).Select(l => l?.GetValue<string>()).OfType<string>())
                            list.Add(new(part, section, text.Length > 0 ? (text.Length > 80 ? text[..80] : text) : link,
                                VideoHintRx().IsMatch(link) ? MediaKind.Video : MediaKind.Link, link));
                        break;
                }
            }
        }
        return list;
    }
}
