namespace SoHocTap.Data;

public sealed class CachedFile<T>(Func<string> path, Func<string, T?> parse) where T : class
{
    private readonly object _gate = new();
    private DateTime _stamp;
    private long _length = -1;
    private T? _value;
    private string? _problem;

    public string Path => path();

    public string? Problem { get { lock (_gate) return _problem; } }

    public Action<string>? OnProblem { get; init; }

    public T? Get()
    {
        var file = path();
        FileInfo info;
        try { info = new FileInfo(file); info.Refresh(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return Fail(file, e.Message); }
        lock (_gate)
        {
            if (!info.Exists) { _value = null; _problem = null; _length = -1; return null; }
            if (info.LastWriteTimeUtc == _stamp && info.Length == _length) return _value;
        }
        string text;
        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            text = reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            OnProblem?.Invoke($"Chưa đọc được {System.IO.Path.GetFileName(file)}: {e.Message}");
            lock (_gate) return _value;
        }
        T? value;
        string? problem = null;
        try { value = parse(text); }
        catch (Exception e) when (e is DataReadException or System.Text.Json.JsonException) { value = null; problem = e.Message; }
        lock (_gate)
        {
            _stamp = info.LastWriteTimeUtc;
            _length = info.Length;
            _value = value;
            _problem = problem;
        }
        if (problem is not null) OnProblem?.Invoke($"Không đọc được {System.IO.Path.GetFileName(file)}: {problem}");
        return value;
    }

    public void Invalidate()
    {
        lock (_gate) { _length = -1; _stamp = default; }
    }

    private T? Fail(string file, string why)
    {
        OnProblem?.Invoke($"Không đọc được {System.IO.Path.GetFileName(file)}: {why}");
        lock (_gate) return _value;
    }
}

public sealed class DataReadException(string message, Exception? inner = null) : Exception(message, inner);
