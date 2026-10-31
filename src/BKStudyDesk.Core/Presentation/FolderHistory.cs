namespace SoHocTap.Presentation;

/// <summary>
/// Nút Quay lại của tab Tài liệu: về thư mục vừa xem trước đó (như nút Back của File Explorer), chưa đi đâu thì lên thư mục cha.
/// Không ra ngoài thư mục môn. Đổi môn thì xóa lịch sử. Đường dẫn dùng '/', so không phân biệt hoa thường như thư mục Windows.
/// </summary>
public sealed class FolderHistory
{
    private const int Max = 50;
    private readonly List<string> _back = [];   // cuối danh sách là thư mục vừa xem

    public string Root { get; private set; } = "";

    /// <summary>Bắt đầu ở thư mục môn mới: xóa lịch sử.</summary>
    public void Reset(string root)
    {
        Root = root;
        _back.Clear();
    }

    /// <summary>Đi từ <paramref name="from"/> sang <paramref name="to"/> (mở thư mục con, bấm một cấp trên thanh đường dẫn).</summary>
    public void Went(string from, string to)
    {
        if (Same(from, to)) return;
        _back.Add(from);
        if (_back.Count > Max) _back.RemoveAt(0);
    }

    public bool CanGoBack(string dir) => _back.Any(p => !Same(p, dir) && Inside(p)) || !Same(dir, Root);

    /// <summary>Thư mục để quay lại từ <paramref name="dir"/>; null nếu đang ở thư mục môn và chưa đi đâu.</summary>
    public string? Back(string dir)
    {
        while (_back.Count > 0)
        {
            var prev = _back[^1];
            _back.RemoveAt(_back.Count - 1);
            if (!Same(prev, dir) && Inside(prev)) return prev;
        }
        var i = dir.LastIndexOf('/');
        return Same(dir, Root) || i <= 0 ? null : dir[..i];
    }

    private bool Inside(string dir) => Same(dir, Root) || dir.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase);

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
