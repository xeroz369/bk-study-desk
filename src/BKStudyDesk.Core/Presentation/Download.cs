using System.ComponentModel;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

/// <summary>
/// Một mục LMS trong cửa sổ Tải tài liệu (bản 1.x: Ui/DownloadWindow). Số file và dung lượng tính theo các loại file đang chọn;
/// mặc định tích sẵn mục còn thiếu file trên máy.
/// </summary>
public sealed class SectionRow(LmsSource.SectionInfo info, Func<IReadOnlySet<LmsSource.FileKind>> kinds) : INotifyPropertyChanged
{
    private bool _selected = info.Items.Any(f => !f.Have);

    public LmsSource.SectionInfo Info { get; } = info;
    public string Name => Info.Name;
    public IReadOnlyList<LmsSource.FileStat> Files => [.. Info.Of(kinds())];

    public string Meta
    {
        get
        {
            var files = Files;
            if (files.Count == 0) return L.T("download.noneOfKind");
            var have = files.Count(f => f.Have);
            return L.F("download.meta", files.Count, Format.Size(files.Sum(f => f.Bytes)),
                have == files.Count ? L.T("download.haveAll") : have > 0 ? L.F("download.haveSome", have) : L.T("download.notYet"));
        }
    }

    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }

    /// <summary>Đổi loại file đang chọn: báo dòng phụ vẽ lại.</summary>
    public void KindsChanged() => PropertyChanged?.Invoke(this, new(nameof(Meta)));

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Dòng tóm tắt dưới danh sách: số mục, số mục chọn, số file, dung lượng.</summary>
    public static string Summary(IReadOnlyList<SectionRow> rows)
    {
        if (rows.Count == 0) return "";
        var picked = rows.Where(r => r.Selected).ToList();
        var files = picked.SelectMany(r => r.Files).ToList();
        return L.F("download.summary", rows.Count, picked.Count, files.Count, Format.Size(files.Sum(f => f.Bytes)));
    }
}
