using System.Globalization;
using System.Text;

namespace SoHocTap.Files;

public static class NameMatch
{
    public static string Nfc(string? s) => (s ?? "").Normalize(NormalizationForm.FormC);

    public static readonly IEqualityComparer<string> NfcIgnoreCase = new NfcComparer();

    private sealed class NfcComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => Same(x, y);
        public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(Nfc(obj));
    }

    public static bool Same(string? a, string? b) => string.Equals(Nfc(a), Nfc(b), StringComparison.OrdinalIgnoreCase);

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

    public static bool SameFolded(string? a, string? b) => Fold(a) == Fold(b);

    public static bool ContainsFolded(string? text, string? part) => Fold(text).Contains(Fold(part), StringComparison.Ordinal);

    public static bool SubjectIs(string? itemSubject, string subject)
    {
        var item = Nfc(itemSubject).Trim();
        var name = Nfc(subject).Trim();
        if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase)) return true;
        return item.Length > name.Length + 3 && item.EndsWith(')')
               && item.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase);
    }
}
