namespace SoHocTap.Presentation.Practice;

/// <summary>Việc của một phím khi đang làm câu hỏi (QuizRunView).</summary>
public enum QuizKey { None, Choose, Check, Prev, Next }

/// <summary>
/// Phím tắt khi làm quiz (thuần, test được): 1-9 (cả bàn phím số) chọn đáp án thứ n, Enter là Kiểm tra, mũi tên trái phải sang câu
/// trước sau. Đang gõ trong ô nhập (câu điền, ghi chú) thì không bắt phím nào, để gõ số, Enter, di chuyển con trỏ như thường.
/// </summary>
public static class QuizKeys
{
    /// <param name="key">Tên phím như Avalonia Key.ToString(): "D1", "NumPad1", "Enter", "Left", "Right".</param>
    /// <param name="option">Chỉ số đáp án (từ 0) khi là Choose; phím vượt số đáp án thì None.</param>
    public static QuizKey Of(string key, bool typing, int options, out int option)
    {
        option = -1;
        if (typing) return QuizKey.None;
        switch (key)
        {
            case "Enter": return QuizKey.Check;
            case "Left": return QuizKey.Prev;
            case "Right": return QuizKey.Next;
        }
        var digit = key.StartsWith("NumPad", StringComparison.Ordinal) ? key[6..] : key.StartsWith('D') ? key[1..] : "";
        if (digit.Length != 1 || digit[0] is < '1' or > '9' || digit[0] - '1' >= options) return QuizKey.None;
        option = digit[0] - '1';
        return QuizKey.Choose;
    }
}
