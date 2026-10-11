using SoHocTap.Files;

namespace SoHocTap.Presentation;

/// <param name="Target">Việc khi chọn, dạng "loại:giá trị" (page:2, subject:Tên môn, lesson:id); MainWindow hiểu và mở.</param>
public sealed record QuickItem(string Title, string Sub, string Target);

/// <summary>Tìm nhanh Ctrl+K: xếp hạng mục khớp chữ gõ (bỏ dấu, không phân biệt hoa thường). Không đụng control.</summary>
public static class QuickSearch
{
    /// <summary>
    /// Hạng: tên bắt đầu bằng chữ gõ, rồi một chữ trong tên bắt đầu bằng chữ gõ, rồi tên chứa chữ gõ, cuối cùng dòng phụ chứa chữ gõ.
    /// Cùng hạng giữ thứ tự đưa vào (trang trước, môn, rồi bài). Ô trống: các mục đầu danh sách.
    /// </summary>
    public static IReadOnlyList<QuickItem> Match(IReadOnlyList<QuickItem> all, string query, int limit = 8)
    {
        var q = NameMatch.Fold(query.Trim());
        if (q.Length == 0) return [.. all.Take(limit)];
        return [.. all.Select(i => (Item: i, Rank: Rank(i, q))).Where(x => x.Rank >= 0).OrderBy(x => x.Rank).Take(limit).Select(x => x.Item)];
    }

    private static int Rank(QuickItem i, string q)
    {
        var title = NameMatch.Fold(i.Title);
        if (title.StartsWith(q, StringComparison.Ordinal)) return 0;
        if (title.Contains(" " + q, StringComparison.Ordinal)) return 1;
        if (title.Contains(q, StringComparison.Ordinal)) return 2;
        return NameMatch.Fold(i.Sub).Contains(q, StringComparison.Ordinal) ? 3 : -1;
    }
}
