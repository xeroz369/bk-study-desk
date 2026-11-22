namespace SoHocTap.Presentation.Practice;

public enum QuizKey { None, Choose, Check, Prev, Next }

public static class QuizKeys
{
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
