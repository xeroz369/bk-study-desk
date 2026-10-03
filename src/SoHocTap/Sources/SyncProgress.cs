namespace SoHocTap.Sources;

/// <summary>
/// Tiến độ của một lượt đồng bộ tại một lúc, đã tính sẵn để gửi lên UI.
/// </summary>
/// <param name="Key">Key ngôn ngữ của câu đang làm (vd. "sync.lms.check"); câu có thể có {0}/{1} cho đếm và {2} cho tên.</param>
/// <param name="Count">Đếm trong bước đang làm (khóa học thứ mấy, tệp thứ mấy, API thứ mấy).</param>
/// <param name="Of">Tổng của phép đếm; 0 = bước này không đếm.</param>
/// <param name="Detail">Tên đi kèm (môn của tệp đang tải); chỉ có khi đang tải tệp.</param>
/// <param name="Permille">Phần đã xong theo trọng số, 0..1000; null = chưa biết tổng (thanh tiến độ chạy vô định).</param>
public readonly record struct SyncProgress(string Key, int Count = 0, int Of = 0, string? Detail = null, int? Permille = null)
{
    /// <summary>
    /// Gộp tiến độ các nguồn đang chạy (LMS và MyBK chạy song song) thành một giá trị cho thanh trạng thái, 0..1.
    /// Có nguồn chưa biết tổng thì null (thanh vô định): trung bình với một số đoán thì thanh nhảy lung tung.
    /// </summary>
    public static double? Overall(IEnumerable<SyncProgress?> running)
    {
        var list = running.ToList();
        if (list.Count == 0 || list.Any(p => p?.Permille is null)) return null;
        return list.Average(p => p!.Value.Permille!.Value) / 1000.0;
    }
}

/// <summary>
/// Kế hoạch các bước của một lượt đồng bộ, để thanh tiến độ chạy theo số thật thay vì vô định.
/// <list type="bullet">
/// <item>Mỗi bước có trọng số (xấp xỉ số request: 1 mỗi khóa học khi kiểm tra, 1 mỗi lần gọi API, 2 cho trang đăng ký môn MyBK).
/// Bước chưa biết trọng số (số khóa học trước khi có danh sách) thì tổng chưa biết, thanh vô định.</item>
/// <item>Bước "giữ chỗ" (tải tệp) chiếm sẵn một nửa thanh khi có thể có tệp: chưa biết số tệp thì các bước khác chỉ chạy tới nửa thanh,
/// biết rồi mà không có tệp nào thì phần giữ chỗ bỏ đi (thanh nhảy lên, không lùi).</item>
/// <item>Thanh không bao giờ lùi, chỉ tới 100% khi <see cref="Finish"/>; có thể chặn trần (33%) cho tới khi xong một bước.</item>
/// </list>
/// Hàm thuần, không I/O, để test được. Một lượt đồng bộ dùng một plan, chỉ một thread gọi.
/// </summary>
public sealed class SyncPlan
{
    /// <summary>Phần thanh dành cho bước giữ chỗ (tải tệp) khi có tệp.</summary>
    public const double ReservedShare = 0.5;

    private sealed class Phase(string key, double? weight, bool reserved)
    {
        public readonly string Key = key;
        public double? Weight = weight;
        public readonly bool Reserved = reserved;
    }

    private readonly List<Phase> _phases = [];
    private int _current = -1;
    private double _done;
    private string? _label;
    private int _count, _of;
    private string? _detail;
    private int _capIndex = -1;
    private double _cap = 1;
    private int _max;
    private bool _finished;

    /// <summary>Thêm một bước vào cuối kế hoạch. <paramref name="weight"/> null = chưa biết (gọi <see cref="SetWeight"/> khi biết).</summary>
    /// <param name="reserved">Bước giữ chỗ (tải tệp): chiếm <see cref="ReservedShare"/> của thanh khi trọng số &gt; 0.</param>
    public SyncPlan Add(string key, double? weight = 1, bool reserved = false)
    {
        _phases.Add(new Phase(key, weight is null ? null : Math.Max(0, weight.Value), reserved));
        return this;
    }

    /// <summary>Biết trọng số của một bước (số khóa học, tổng dung lượng tệp).</summary>
    public void SetWeight(string key, double weight)
    {
        if (_phases.FirstOrDefault(p => p.Key == key) is { } p) p.Weight = Math.Max(0, weight);
    }

    /// <summary>Thanh không vượt <paramref name="cap"/> (0..1) cho tới khi bước <paramref name="key"/> xong.</summary>
    public SyncPlan CapUntilDone(string key, double cap)
    {
        _capIndex = _phases.FindIndex(p => p.Key == key);
        _cap = Math.Clamp(cap, 0, 1);
        return this;
    }

    /// <summary>
    /// Bắt đầu bước <paramref name="key"/>: các bước đứng trước coi như đã xong (kể cả bước bị bỏ qua vì không có gì để làm).
    /// <paramref name="label"/> là key câu hiện trên UI (mặc định là key của bước).
    /// </summary>
    public SyncProgress Enter(string key, string? label = null, int count = 0, int of = 0, string? detail = null)
    {
        var i = _phases.FindIndex(p => p.Key == key);
        if (i > _current) { _current = i; _done = 0; }
        _label = label;
        (_count, _of, _detail) = (count, of, detail);
        return Current;
    }

    /// <summary>
    /// Đã xong <paramref name="done"/> (theo trọng số) của bước đang làm; đổi phép đếm và tên hiện trên UI. Câu trở về câu của bước
    /// (câu thay thế ở <see cref="Enter"/> chỉ dùng lúc chưa đếm được, vd. "đọc danh sách khóa học").
    /// </summary>
    public SyncProgress Advance(double done, int count = 0, int of = 0, string? detail = null)
    {
        _done = Math.Max(_done, done);
        _label = null;
        (_count, _of, _detail) = (count, of, detail);
        return Current;
    }

    /// <summary>Xong cả lượt: thanh lên 100%.</summary>
    public SyncProgress Finish()
    {
        _finished = true;
        _current = _phases.Count;
        return Current;
    }

    public SyncProgress Current
    {
        get
        {
            var key = _label ?? (_current >= 0 && _current < _phases.Count ? _phases[_current].Key : _phases.FirstOrDefault()?.Key ?? "");
            return new SyncProgress(key, _count, _of, _detail, Permille());
        }
    }

    private int? Permille()
    {
        if (_finished) return _max = 1000;
        if (_phases.Any(p => !p.Reserved && p.Weight is null)) return null;
        var reserved = _phases.FirstOrDefault(p => p.Reserved);
        var share = reserved is null || reserved.Weight is 0 ? 0 : ReservedShare;   // chưa biết số tệp: vẫn giữ chỗ

        double nonTotal = 0, nonDone = 0, resDone = 0;
        for (var i = 0; i < _phases.Count; i++)
        {
            var p = _phases[i];
            var w = p.Weight ?? 0;
            var done = i < _current ? w : i == _current ? Math.Min(_done, w) : 0;
            if (p.Reserved) resDone = done;
            else { nonTotal += w; nonDone += done; }
        }
        var f = (1 - share) * (nonTotal > 0 ? nonDone / nonTotal : _current >= 0 ? 1 : 0);
        if (share > 0 && reserved!.Weight is double rw and > 0) f += share * (resDone / rw);
        if (_capIndex >= 0 && _current <= _capIndex) f = Math.Min(f, _cap);
        // Chưa xong thì không hiện 100%; không bao giờ lùi.
        var permille = (int)Math.Clamp(Math.Floor(f * 1000), 0, 990);
        _max = Math.Max(_max, permille);
        return _max;
    }
}
