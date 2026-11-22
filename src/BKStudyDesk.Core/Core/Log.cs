namespace SoHocTap.Core;

public static class Log
{
    private static readonly object Gate = new();
    private static string FilePath => Paths.DataFile("app.log");

    public static bool Verbose { get; set; }

    public static string LogFile => FilePath;

    public static void Debug(string message) { if (Verbose) Write("DEBUG", message); }
    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? e = null) => Write("ERROR", e is null ? message : $"{message}: {e}");

    public static string Where(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host + u.AbsolutePath : "?";

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
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
    }
}

public static class DiagnosticLog
{
    public const int Days = 7;

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
