using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SoHocTap.Data;

/// <summary>
/// Một sự kiện người dùng tự thêm vào lịch (issue #22): sự kiện riêng hoặc buổi học bù. Giờ là giờ VN dạng chữ, giống MyBK:
/// Date "yyyy-MM-dd", Start "HH:mm", End "HH:mm" hoặc rỗng. Kind "event" hoặc "makeup" (học bù). CreatedAt là Unix seconds.
/// </summary>
public sealed record CustomEvent
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
    public string Location { get; init; } = "";
    public string Kind { get; init; } = CustomEvents.KindEvent;
    public string Note { get; init; } = "";
    public long CreatedAt { get; init; }

    [JsonIgnore] public bool IsMakeup => Kind == CustomEvents.KindMakeup;

    /// <summary>Ngày diễn ra; null nếu Date sai định dạng (file sửa tay).</summary>
    [JsonIgnore] public DateTime? Day => DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    [JsonIgnore] public int? StartMin => Core.VnTime.ParseClock(Start);

    /// <summary>Phút kết thúc; không ghi giờ kết thúc (hoặc sai) thì coi như dài một tiếng để còn đặt lên lưới tuần.</summary>
    [JsonIgnore] public int? EndMin => Core.VnTime.ParseClock(End) is { } e && StartMin is { } s && e > s ? e : StartMin is { } st ? Math.Min(st + 60, 24 * 60) : null;

    /// <summary>Giờ bắt đầu theo giờ đồng hồ VN; null nếu ngày hoặc giờ sai.</summary>
    [JsonIgnore] public DateTime? StartWall => Day is { } d && StartMin is { } m ? d.AddMinutes(m) : null;
}

/// <summary>Nội dung data/custom-events.json.</summary>
public sealed class CustomEventFile
{
    public int Version { get; set; } = 1;
    public List<CustomEvent?>? Events { get; set; } = [];
}

/// <summary>
/// Đọc/ghi data/custom-events.json: chỉ nằm trên máy, không gửi đi đâu (không đồng bộ lên LMS/MyBK).
/// Đọc qua <see cref="CachedFile{T}"/> (cache theo mtime), ghi atomic qua hàm <c>write</c> (app truyền JsonStore.Write).
/// Tách path và hàm ghi ra tham số để test round-trip với file tạm mà không cần Paths/Log.
/// </summary>
public sealed class CustomEventStore
{
    private readonly Func<string> _path;
    private readonly Action<string, JsonNode> _write;
    private readonly CachedFile<List<CustomEvent>> _file;
    private readonly object _gate = new();

    public CustomEventStore(Func<string> path, Action<string, JsonNode> write, Action<string>? onProblem = null)
    {
        _path = path;
        _write = write;
        _file = new CachedFile<List<CustomEvent>>(path, CustomEvents.Parse) { OnProblem = onProblem };
    }

    /// <summary>Lý do file có mà không đọc được (null = đọc được hoặc chưa có file).</summary>
    public string? Problem { get { _file.Get(); return _file.Problem; } }

    /// <summary>Mọi sự kiện, sắp theo ngày giờ. Chưa có file hay file hỏng thì danh sách rỗng.</summary>
    public IReadOnlyList<CustomEvent> All() => _file.Get() ?? [];

    public CustomEvent? Find(string id) => All().FirstOrDefault(e => e.Id == id);

    /// <summary>Thêm mới (Id rỗng thì tạo guid, CreatedAt rỗng thì lấy giờ hiện tại) hoặc thay bản cùng Id.</summary>
    public CustomEvent Save(CustomEvent e)
    {
        lock (_gate)
        {
            var item = e with
            {
                Id = e.Id.Length > 0 ? e.Id : Guid.NewGuid().ToString("N"),
                CreatedAt = e.CreatedAt > 0 ? e.CreatedAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            var list = All().Where(x => x.Id != item.Id).Append(item).ToList();
            WriteAll(list);
            return item;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var list = All().ToList();
            if (list.RemoveAll(x => x.Id == id) == 0) return false;
            WriteAll(list);
            return true;
        }
    }

    private void WriteAll(List<CustomEvent> list)
    {
        // Ghi đè file hỏng sẽ làm mất dữ liệu người dùng: không đọc được thì không ghi, để họ còn sửa tay hoặc gửi file báo lỗi.
        if (_file.Problem is { } p) throw new InvalidOperationException(p);
        _write(_path(), CustomEvents.Serialize(list));
        _file.Invalidate();
    }
}

/// <summary>Hàm thuần cho sự kiện tự thêm: parse/serialize file, tìm môn trong tiêu đề.</summary>
public static class CustomEvents
{
    public const string KindEvent = "event";
    public const string KindMakeup = "makeup";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Parse file chịu lỗi (DataJson): phần tử null, trường thiếu không làm hỏng cả file. Sắp theo ngày giờ.</summary>
    public static List<CustomEvent> Parse(string text)
    {
        var file = DataJson.Parse<CustomEventFile>(text);
        return Normalize(file?.Events ?? []);
    }

    public static List<CustomEvent> Normalize(IEnumerable<CustomEvent?> events) =>
        [.. events.OfType<CustomEvent>()
            .Select(e => e with
            {
                Id = (e.Id ?? "").Trim(),
                Title = (e.Title ?? "").Trim(),
                Date = (e.Date ?? "").Trim(),
                Start = (e.Start ?? "").Trim(),
                End = (e.End ?? "").Trim(),
                Location = (e.Location ?? "").Trim(),
                Kind = e.Kind == KindMakeup ? KindMakeup : KindEvent,
                Note = e.Note ?? "",
            })
            .Where(e => e.Id.Length > 0)
            .OrderBy(e => e.Date, StringComparer.Ordinal).ThenBy(e => e.StartMin ?? 0).ThenBy(e => e.CreatedAt)];

    public static JsonNode Serialize(IEnumerable<CustomEvent> events) =>
        JsonSerializer.SerializeToNode(new CustomEventFile { Events = [.. Normalize(events)] }, WriteOptions)!;

    /// <summary>Giờ bắt đầu dạng Unix seconds (giờ VN như MyBK, không theo múi giờ máy); null nếu ngày hoặc giờ sai.</summary>
    public static long? StartTime(CustomEvent e) => e.StartWall is { } w ? Core.VnTime.FromWall(w) : null;

    /// <summary>
    /// Sự kiện để xuất .ics. Không ghi giờ kết thúc thì dài một tiếng (như trên lưới tuần). UID theo Id: nhập lại file thì
    /// ứng dụng lịch cập nhật sự kiện cũ thay vì tạo bản trùng. <paramref name="makeupLabel"/> ("Học bù") ghi vào mô tả.
    /// </summary>
    public static Core.IcsEvent? ToIcs(CustomEvent e, string makeupLabel)
    {
        if (e.Day is not { } day || e.StartMin is not { } s || e.EndMin is not { } end) return null;
        var description = string.Join("\n", new[] { e.IsMakeup ? makeupLabel : "", e.Note.Trim() }.Where(x => x.Length > 0));
        return new Core.IcsEvent($"ce-{e.Id}", day.AddMinutes(s), day.AddMinutes(end), e.Title, e.Location, description);
    }

    /// <summary>
    /// Sự kiện sắp bắt đầu trong <paramref name="window"/> giây mà chưa nhắc. Key nhắc gồm Id và ngày giờ: sửa giờ sự kiện thì
    /// nhắc lại theo giờ mới, còn nhắc rồi thì thôi (timer chạy 5 phút một lần, không được nhắc lặp).
    /// </summary>
    public static List<(CustomEvent Event, long Time, string Key)> DueSoon(IEnumerable<CustomEvent> events, long now, long window, Func<string, bool> alreadySent) =>
        [.. events.Select(e => (Event: e, Time: StartTime(e) ?? 0, Key: $"ce-{e.Id}@{e.Date}T{e.Start}"))
            .Where(x => x.Time > now && x.Time - now <= window && !alreadySent(x.Key))
            .OrderBy(x => x.Time)];

    /// <summary>
    /// Môn có trong tiêu đề (tên môn hoặc mã môn, không phân biệt hoa thường): tên dài nhất khớp được chọn, để
    /// "Học bù Giải tích 2" ra Giải tích 2 chứ không phải Giải tích 1 nếu cả hai cùng có. Không khớp thì null.
    /// </summary>
    public static (string Code, string Name)? MatchCourse(string title, IEnumerable<(string Code, string Name)> courses)
    {
        var t = title ?? "";
        (string Code, string Name)? best = null;
        var bestLen = 0;
        foreach (var c in courses)
        {
            var len = 0;
            if (c.Name.Length > 0 && t.Contains(c.Name, StringComparison.OrdinalIgnoreCase)) len = c.Name.Length;
            else if (c.Code.Length >= 3 && t.Contains(c.Code, StringComparison.OrdinalIgnoreCase)) len = c.Code.Length;
            if (len > bestLen) { best = c; bestLen = len; }
        }
        return best;
    }
}
