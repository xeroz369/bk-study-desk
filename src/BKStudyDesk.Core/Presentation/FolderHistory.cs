namespace SoHocTap.Presentation;

public sealed class FolderHistory
{
    private const int Max = 50;
    private readonly List<string> _back = [];

    public string Root { get; private set; } = "";

    public void Reset(string root)
    {
        Root = root;
        _back.Clear();
    }

    public void Went(string from, string to)
    {
        if (Same(from, to)) return;
        _back.Add(from);
        if (_back.Count > Max) _back.RemoveAt(0);
    }

    public bool CanGoBack(string dir) => _back.Any(p => !Same(p, dir) && Inside(p)) || !Same(dir, Root);

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
