namespace SoHocTap.Core;

/// <summary>Log của app: data/app.log (tối đa ~1 MB). Không bao giờ ghi token, cookie hay dữ liệu cá nhân vào đây.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string FilePath => Paths.DataFile("app.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? e = null) => Write("ERROR", e is null ? message : $"{message}: {e.GetType().Name}: {e.Message}");

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1_000_000)
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{level}\t{message}{Environment.NewLine}");
            }
            catch (IOException) { /* lỗi ghi log thì bỏ qua, không được làm crash app */ }
        }
    }
}
