using System.Globalization;
using System.Text;

namespace SoHocTap.Files;

/// <summary>
/// So tên môn, tên thư mục. Tên trên đĩa có thể ở dạng NFD (dấu tách rời, vd. thư mục giải nén từ zip tạo trên macOS) trong khi
/// tên lấy từ LMS là NFC: so thẳng thì "Giải tích 2" khác "Giải tích 2". Mọi phép so đều đưa về NFC trước; chỗ khớp tên lỏng
/// (tên file tải về, ô lọc) thì bỏ luôn dấu.
/// </summary>
public static class NameMatch
{
    public static string Nfc(string? s) => (s ?? "").Normalize(NormalizationForm.FormC);

    /// <summary>So khóa theo NFC, không phân biệt hoa thường (từ điển tên môn).</summary>
    public static readonly IEqualityComparer<string> NfcIgnoreCase = new NfcComparer();

    private sealed class NfcComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => Same(x, y);
        public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(Nfc(obj));
    }

    /// <summary>Cùng tên (NFC, không phân biệt hoa thường).</summary>
    public static bool Same(string? a, string? b) => string.Equals(Nfc(a), Nfc(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Bỏ dấu, đ thành d, chữ thường: "Giải Tích 2" thành "giai tich 2".</summary>
    public static string Fold(string? s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c switch { 'đ' => 'd', 'Đ' => 'd', _ => char.ToLowerInvariant(c) });
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Cùng tên khi bỏ dấu.</summary>
    public static bool SameFolded(string? a, string? b) => Fold(a) == Fold(b);

    /// <summary><paramref name="text"/> có chứa <paramref name="part"/> khi bỏ dấu (ô lọc, tên file tải về).</summary>
    public static bool ContainsFolded(string? text, string? part) => Fold(text).Contains(Fold(part), StringComparison.Ordinal);

    /// <summary>
    /// Mốc thời gian thuộc môn <paramref name="subject"/>: tên môn của mốc là đúng tên môn, hoặc tên môn kèm phần phụ
    /// ("Vật lý 1 (Thí nghiệm)"). Không dùng StartsWith: "Giải tích 1" khớp nhầm cả "Giải tích 12".
    /// </summary>
    public static bool SubjectIs(string? itemSubject, string subject)
    {
        var item = Nfc(itemSubject).Trim();
        var name = Nfc(subject).Trim();
        if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase)) return true;
        return item.Length > name.Length + 3 && item.EndsWith(')')
               && item.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase);
    }
}
