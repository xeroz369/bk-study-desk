using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Một buổi đặt lên lưới tuần (như 1.x, Ui/WeekGrid). Day theo MyBK: 2 là Thứ hai, 8 là Chủ nhật. Key chọn màu (mã môn, học bù có tên môn
/// thì cùng màu môn đó). Tag khác null là sự kiện tự thêm (Tự thêm, Học bù): View vẽ khác để không lẫn với buổi học của MyBK;
/// CustomId là Id trong data/custom-events.json (bấm đúp để sửa).
/// </summary>
public sealed record WeekBlock(int Day, int StartMin, int EndMin, string Title, string Detail, string Tip, string Key, string? Tag = null, string? CustomId = null);

/// <summary>Lưới tuần kiểu Google Calendar: khối của tuần, chia làn khi trùng giờ, màu theo môn. Thuần, có test; View chỉ vẽ.</summary>
public static class WeekGridPresenter
{
    /// <summary>
    /// Buổi học của tuần (môn có học tuần này, giờ đọc được) và sự kiện tự thêm có giờ trong tuần. Buổi giờ không đọc được không đặt
    /// lên lưới (WeekPresenter.NoSlot ghi tên riêng).
    /// </summary>
    public static IReadOnlyList<WeekBlock> Blocks(MybkData? mybk, IEnumerable<CustomEvent> custom, DateTime monday, IEnumerable<(string Code, string Name)> courses)
    {
        var week = Format.IsoWeek(monday);
        var blocks = new List<WeekBlock>();
        foreach (var c in (mybk?.Schedule ?? []).Where(c => c.Day is >= 2 and <= 8 && c.Weeks.Contains(week)))
        {
            if (VnTime.ParseClock(c.Start) is not { } s || VnTime.ParseClock(c.End) is not { } e || e <= s) continue;
            var when = $"{c.Start}-{c.End}";
            var tip = string.Join("\n", new[] { c.Name, $"{Format.MybkDays.GetValueOrDefault(c.Day)} {when}, {c.Room}", c.Teacher ?? "", c.Code + (c.Group is { } g ? ", " + g : "") }
                .Where(x => x.Length > 0));
            blocks.Add(new WeekBlock(c.Day, s, e, c.Name, $"{when}, {c.Room}", tip, c.Code));
        }
        var known = courses.ToList();
        foreach (var x in custom.Where(x => x.Day is { } d && d >= monday.Date && d < monday.Date.AddDays(7) && x.StartMin is not null))
        {
            var day = Format.MybkDay(x.Day!.Value);
            var (start, end) = (x.StartMin!.Value, x.EndMin!.Value);   // không ghi giờ kết thúc: CustomEvent.EndMin là bắt đầu cộng một giờ
            var tag = L.T(x.IsMakeup ? "kind.makeup" : "kind.custom");
            var key = x.IsMakeup && CustomEvents.MatchCourse(x.Title, known) is { } m ? m.Code : "custom:" + x.Title;
            var tip = string.Join("\n", new[] { x.Title, $"{tag}, {Format.MybkDays.GetValueOrDefault(day)} {AppState.CustomTime(x)}", x.Location, x.Note }.Where(t => t.Length > 0));
            blocks.Add(new WeekBlock(day, start, end, x.Title, AppState.CustomLabel(x), tip, key, tag, x.Id));
        }
        return blocks;
    }

    /// <summary>Làn của mỗi buổi trong một ngày: các buổi chồng giờ nhau chia đều bề ngang (Count là số làn của cụm).</summary>
    public static IReadOnlyDictionary<WeekBlock, (int Lane, int Count)> Lanes(IEnumerable<WeekBlock> day)
    {
        var result = new Dictionary<WeekBlock, (int, int)>();
        var cluster = new List<(WeekBlock B, int Lane)>();
        var laneEnds = new List<int>();
        var clusterEnd = -1;
        void Flush()
        {
            foreach (var (b, l) in cluster) result[b] = (l, laneEnds.Count);
            cluster.Clear();
            laneEnds.Clear();
        }
        foreach (var b in day.OrderBy(b => b.StartMin).ThenByDescending(b => b.EndMin))
        {
            if (b.StartMin >= clusterEnd) Flush();
            var lane = laneEnds.FindIndex(end => end <= b.StartMin);
            if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(b.EndMin); } else laneEnds[lane] = b.EndMin;
            cluster.Add((b, lane));
            clusterEnd = Math.Max(clusterEnd, b.EndMin);
        }
        Flush();
        return result;
    }

    /// <summary>Màu thứ mấy trong bảng màu: hash ổn định giữa các lần chạy (string.GetHashCode đổi theo process) để mỗi môn giữ một màu.</summary>
    public static int ColorIndex(string key, int colors)
    {
        unchecked
        {
            var h = 17;
            foreach (var c in key) h = h * 31 + c;
            return (int)((uint)h % (uint)Math.Max(1, colors));
        }
    }

    /// <summary>Khoảng có buổi học (giờ chẵn), để chọn tỉ lệ dọc; lưới vẫn đủ 0:00 tới 24:00.</summary>
    public static (int From, int To) Span(IReadOnlyCollection<WeekBlock> blocks) =>
        blocks.Count == 0 ? (7 * 60, 17 * 60) : (blocks.Min(b => b.StartMin) / 60 * 60, (blocks.Max(b => b.EndMin) + 59) / 60 * 60);
}
