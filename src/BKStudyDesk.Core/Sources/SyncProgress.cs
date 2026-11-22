namespace SoHocTap.Sources;

public readonly record struct SyncProgress(string Key, int Count = 0, int Of = 0, string? Detail = null, int? Permille = null)
{
    public static double? Overall(IEnumerable<SyncProgress?> running)
    {
        var list = running.ToList();
        if (list.Count == 0 || list.Any(p => p?.Permille is null)) return null;
        return list.Average(p => p!.Value.Permille!.Value) / 1000.0;
    }
}

public sealed class SyncPlan
{
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

    public SyncPlan Add(string key, double? weight = 1, bool reserved = false)
    {
        _phases.Add(new Phase(key, weight is null ? null : Math.Max(0, weight.Value), reserved));
        return this;
    }

    public void SetWeight(string key, double weight)
    {
        if (_phases.FirstOrDefault(p => p.Key == key) is { } p) p.Weight = Math.Max(0, weight);
    }

    public SyncPlan CapUntilDone(string key, double cap)
    {
        _capIndex = _phases.FindIndex(p => p.Key == key);
        _cap = Math.Clamp(cap, 0, 1);
        return this;
    }

    public SyncProgress Enter(string key, string? label = null, int count = 0, int of = 0, string? detail = null)
    {
        var i = _phases.FindIndex(p => p.Key == key);
        if (i > _current) { _current = i; _done = 0; }
        _label = label;
        (_count, _of, _detail) = (count, of, detail);
        return Current;
    }

    public SyncProgress Advance(double done, int count = 0, int of = 0, string? detail = null)
    {
        _done = Math.Max(_done, done);
        _label = null;
        (_count, _of, _detail) = (count, of, detail);
        return Current;
    }

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
        var share = reserved is null || reserved.Weight is 0 ? 0 : ReservedShare;

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
        var permille = (int)Math.Clamp(Math.Floor(f * 1000), 0, 990);
        _max = Math.Max(_max, permille);
        return _max;
    }
}
