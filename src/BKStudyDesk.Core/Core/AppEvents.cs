namespace SoHocTap.Core;

/// <summary>
/// Sự kiện toàn app mà Core/Shell báo lên còn UI tự quyết cách hiện. Core không biết gì về WPF nên chỉ bắn event,
/// UI (MainWindow) đăng ký để hiện ở thanh trạng thái hay InfoBar.
/// </summary>
public static class AppEvents
{
    /// <summary>
    /// Lỗi không ai bắt (UI thread, task chạy nền không ai await). Đã ghi log trước khi bắn event; handler chỉ cần báo
    /// cho người dùng, ví dụ câu <c>error.unhandled</c> trong file ngôn ngữ. Có thể bắn từ thread bất kỳ.
    /// </summary>
    public static event Action<Exception>? UnhandledError;

    public static void RaiseUnhandled(Exception e)
    {
        try { UnhandledError?.Invoke(e); }
        // Handler của UI lỗi tiếp thì chỉ ghi log, không ném lại: đang ở trong handler lỗi, ném nữa là crash app.
        catch (Exception x) when (x is not OutOfMemoryException) { Log.Error("Handler của AppEvents.UnhandledError lỗi", x); }
    }
}
