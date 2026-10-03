using System.Diagnostics;
using System.Text.Json.Nodes;
using SoHocTap.Files;
using Xunit.Abstractions;

namespace SoHocTap.Tests;

/// <summary>
/// Đo ListSubjects trên cây giả (thư mục tạm): cách cũ (1.1.7: EnumerateFiles cả cây, tạo FileInfo cho mọi file, chạy trên UI thread
/// mỗi lần mở trang), cách mới lần đầu (JunkFilter, không vào .git/db), và lần mở lại trang khi không đổi gì (chỉ đi qua thư mục).
/// In số đo ra output của test; chỉ kiểm tra thứ tự lớn bé, không kiểm số tuyệt đối (máy CI chậm nhanh khác nhau).
/// </summary>
public class ListSubjectsBenchmark(ITestOutputHelper output)
{
    private static JsonArray OldListSubjects(string root)
    {
        var list = new JsonArray();
        foreach (var d in Directory.GetDirectories(root).Order())
        {
            var files = Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)).Select(f => new FileInfo(f)).ToList();
            list.Add(new JsonObject
            {
                ["name"] = Path.GetFileName(d),
                ["files"] = files.Count,
                ["modified"] = files.Count == 0 ? 0 : files.Max(f => new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds()),
            });
        }
        return list;
    }

    private static double Ms(Action a, int runs = 5)
    {
        a();   // làm nóng (cache hệ thống file, JIT)
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < runs; i++) a();
        return sw.Elapsed.TotalMilliseconds / runs;
    }

    [Fact]
    public void MeasureListSubjects()
    {
        using var t = new TempStudy();
        // 12 môn, mỗi môn 6 thư mục x 40 tệp, cộng một dự án KiCad có .history\.git với 400 tệp rác.
        for (var s = 0; s < 12; s++)
        {
            for (var d = 0; d < 6; d++)
                for (var f = 0; f < 40; f++) t.Put($"Môn {s}/Chương {d}/tệp {f}.pdf", "x");
            for (var g = 0; g < 400; g++) t.Put($"Môn {s}/Lab/.history/.git/objects/{g % 20}/{g}", "rác");
        }

        var old = Ms(() => OldListSubjects(t.Subjects));
        var cold = Ms(() => DocumentScan.ListSubjects(t.Subjects));
        DocumentScan.Cached("bench|" + t.Subjects, t.Subjects, () => DocumentScan.ListSubjects(t.Subjects), default, out _);
        var hit = false;
        var cached = Ms(() => DocumentScan.Cached("bench|" + t.Subjects, t.Subjects, () => DocumentScan.ListSubjects(t.Subjects), default, out hit));

        output.WriteLine($"ListSubjects 12 môn, 2880 tệp + 4800 tệp rác: cũ {old:0.0} ms, mới {cold:0.0} ms, mở lại (cache) {cached:0.0} ms");
        Assert.True(hit);
        Assert.True(cached < old, $"cache {cached:0.0} ms phải nhanh hơn quét cũ {old:0.0} ms");
        // Kết quả mới không đếm tệp rác.
        Assert.All(DocumentScan.ListSubjects(t.Subjects).OfType<JsonObject>(), s => Assert.Equal(240, s["files"]!.GetValue<int>()));
    }
}
