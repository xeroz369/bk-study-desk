using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;

namespace SoHocTap.Ui;

/// <summary>
/// Hỏi folder lưu tài liệu ở lần mở đầu (bản Store), gợi ý sẵn <see cref="Paths.SuggestedRoot"/>.
/// Đóng bằng X cũng lưu folder gợi ý để không hỏi lại; đổi sau trong Cài đặt.
/// </summary>
public partial class FolderSetupWindow : Window
{
    private bool _saved;

    public FolderSetupWindow()
    {
        InitializeComponent();
        Folder.Text = Paths.SuggestedRoot;
        Closing += OnClosing;
    }

    /// <summary>Bản Store chưa chọn folder lần nào thì cần hỏi.</summary>
    public static bool Needed => AppPackage.IsPackaged && Config.Str("folders.root").Trim().Length == 0;

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var start = Directory.Exists(Folder.Text) ? Folder.Text : Path.GetPathRoot(Folder.Text) ?? "";
        var d = new Microsoft.Win32.OpenFolderDialog { Title = L.T("settings.rootPick"), InitialDirectory = start };
        if (d.ShowDialog(this) != true) return;
        // Chọn một folder có sẵn (vd. D:\) thì tạo folder con của app bên trong, khỏi bày file ra gốc ổ.
        var picked = d.FolderName;
        Folder.Text = Path.GetPathRoot(picked)?.Equals(picked, StringComparison.OrdinalIgnoreCase) == true
            ? Path.Combine(picked, AppPackage.Product) : picked;
    }

    private void OnChanged(object sender, TextChangedEventArgs e)
    {
        var path = Folder.Text.Trim();
        try
        {
            var root = Path.GetPathRoot(path);
            var drive = string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
            Note.Text = drive is { IsReady: true } ? L.F("setup.free", FreeText(drive.AvailableFreeSpace), drive.Name.TrimEnd('\\')) : "";
        }
        catch (ArgumentException) { Note.Text = ""; }
    }

    /// <summary>Dung lượng ổ đĩa: GB cho dễ đọc (Format.Size chỉ tới MB, hợp với file).</summary>
    private static string FreeText(long bytes) =>
        bytes >= 1L << 30 ? $"{(bytes / (double)(1L << 30)).ToString("0", L.Culture)} GB" : Format.Size(bytes);

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (TrySave(Folder.Text.Trim(), out var error)) Close();
        else Note.Text = L.F("setup.invalid", error);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_saved) TrySave(Paths.SuggestedRoot, out _);
    }

    /// <summary>Tạo folder, thử ghi một file tạm, rồi lưu folders.root.</summary>
    private bool TrySave(string path, out string error)
    {
        error = "";
        try
        {
            if (!Path.IsPathFullyQualified(path)) { error = L.T("setup.notFull"); return false; }
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            var c = (JsonObject)Config.Current.DeepClone();
            (c["folders"] as JsonObject ?? (JsonObject)(c["folders"] = new JsonObject()))["root"] = full;
            Config.Save(c);
            _saved = true;
            Log.Info($"Folder tài liệu: {full}");
            return true;
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = x.Message;
            return false;
        }
    }
}
