namespace SoHocTap.Core;

/// <summary>
/// Log của app: data/app.log (tối đa ~2 MB, giữ thêm một file .1). Không bao giờ ghi mật khẩu, token, cookie (chỉ tên cookie)
/// hay dữ liệu cá nhân vào đây; URL thì chỉ ghi host + path qua <see cref="Where"/> vì query có thể chứa ticket/token.
/// Mặc định ghi mức Information (lỗi, cảnh báo, mỗi lần đồng bộ, đăng nhập), như khuyến nghị của Microsoft cho bản phát hành.
/// Debug (từng request, từng bước) chỉ ghi khi bật "Ghi log chẩn đoán" trong Cài đặt, tự tắt sau 7 ngày (CWE-532: không để log
/// debug bật mãi ở bản phát hành).
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string FilePath => Paths.DataFile("app.log");

    /// <summary>Đang ghi log chi tiết (app.debugLog). Đặt lúc mở app và khi đổi trong Cài đặt.</summary>
    public static bool Verbose { get; set; }

    /// <summary>Đường dẫn app.log (nút "Mở file log" trong Cài đặt).</summary>
    public static string LogFile => FilePath;

    public static void Debug(string message) { if (Verbose) Write("DEBUG", message); }
    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    /// <summary>Lỗi: bình thường ghi loại + message; khi bật log chi tiết thì ghi cả stack trace.</summary>
    public static void Error(string message, Exception? e = null) =>
        Write("ERROR", e is null ? message : Verbose ? $"{message}: {e}" : $"{message}: {e.GetType().Name}: {e.Message}");

    /// <summary>host + path của URL, bỏ query và fragment (có thể chứa ticket CAS, token LMS).</summary>
    public static string Where(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host + u.AbsolutePath : "?";

    /// <summary>Che dữ liệu nhạy cảm trong một dòng log (xem <see cref="LogRedactor"/>).</summary>
    public static string Redact(string message) => LogRedactor.Redact(message);

    private static void Write(string level, string message)
    {
        message = Redact(message);
        lock (Gate)
        {
            try
            {
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 2_000_000)
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{level}\t{message}{Environment.NewLine}");
            }
            catch (IOException) { /* lỗi ghi log thì bỏ qua, không được làm crash app */ }
        }
    }
}

/// <summary>
/// Log chẩn đoán (mức Debug): người dùng bật trong Cài đặt khi cần báo lỗi, tự tắt sau <see cref="Days"/> ngày (app.debugLogUntil).
/// </summary>
public static class DiagnosticLog
{
    public const int Days = 7;

    /// <summary>Đang bật và chưa hết hạn; hết hạn thì tự tắt luôn trong config.</summary>
    public static bool Active()
    {
        if (!Config.Bool("app.debugLog", false)) return false;
        var until = Config.Node("app.debugLogUntil")?.GetValue<long>() ?? 0;
        if (until == 0 || DateTimeOffset.UtcNow.ToUnixTimeSeconds() < until) return true;
        Config.Set("app.debugLog", false);
        Log.Info("Log chẩn đoán đã tự tắt sau " + Days + " ngày");
        return false;
    }

    public static void Set(bool on)
    {
        Config.Set("app.debugLogUntil", on ? DateTimeOffset.UtcNow.AddDays(Days).ToUnixTimeSeconds() : 0);
        Config.Set("app.debugLog", on);
        Log.Verbose = on;
        Log.Info(on ? $"Bật log chẩn đoán ({Days} ngày)" : "Tắt log chẩn đoán");
    }
}
