namespace SoHocTap.Data;

/// <summary>
/// Một file dữ liệu đọc có cache trong RAM, khóa theo mtime + kích thước: file không đổi thì không đọc lại, không parse lại
/// (Status() và UI hỏi rất nhiều lần trong một lượt đồng bộ). File đổi (sync ghi, người dùng nhập dữ liệu) thì lần hỏi sau
/// đọc lại. Không đọc được thì giữ lại lý do ở <see cref="Problem"/> để UI báo "dữ liệu không đọc được" thay vì trang trống.
/// </summary>
public sealed class CachedFile<T>(Func<string> path, Func<string, T?> parse) where T : class
{
    private readonly object _gate = new();
    private DateTime _stamp;
    private long _length = -1;
    private T? _value;
    private string? _problem;

    public string Path => path();

    /// <summary>Lý do lần đọc gần nhất thất bại (đã có chi tiết như JsonException.Path); null nếu đọc được hoặc chưa có file.</summary>
    public string? Problem { get { lock (_gate) return _problem; } }

    /// <summary>Báo lỗi đọc file (store truyền Log.Warn vào; test để null).</summary>
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
            // FileShare.ReadWrite | Delete: sync có thể đang thay file ngay lúc UI đọc.
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            text = reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // File đang bận (ghi dở, antivirus): giữ bản cũ trong cache, lần sau đọc lại.
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

    /// <summary>Bỏ cache (vừa ghi file xong): lần hỏi sau đọc lại dù mtime trùng (hệ thống file làm tròn mtime).</summary>
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

/// <summary>Dữ liệu trong file không đọc được; message đã có vị trí lỗi (JsonException.Path) để ghi log.</summary>
public sealed class DataReadException(string message, Exception? inner = null) : Exception(message, inner);
