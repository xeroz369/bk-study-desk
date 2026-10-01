using System.ComponentModel;
using System.Windows;
using SoHocTap.Core;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Ui;

internal sealed class SectionRow(LmsSource.SectionInfo s) : INotifyPropertyChanged
{
    private bool _selected = s.Have < s.Files;   // default tick sẵn mục còn thiếu file
    public LmsSource.SectionInfo Info { get; } = s;
    public string Name => Info.Name;
    public string Meta => L.F("download.meta", Info.Files, Format.Size(Info.Bytes),
        Info.Have == Info.Files ? L.T("download.haveAll") : Info.Have > 0 ? L.F("download.haveSome", Info.Have) : L.T("download.notYet"));
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Tải tài liệu của một khóa theo từng mục. Danh sách mục lấy thẳng từ LMS; file lưu vào folder môn giống lúc sync
/// (bỏ file trùng nội dung, bản cũ chuyển vào _Lưu trữ). Có giải nén zip hay không tùy checkbox (default lấy từ config archives.extract).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "_run chỉ sống trong một lần tải, hủy ở finally của OnStart.")]
public partial class DownloadWindow : Window
{
    private readonly LmsSource _lms;
    private readonly LmsCourse _course;
    private List<SectionRow> _rows = [];
    private CancellationTokenSource? _run;

    internal DownloadWindow(LmsSource lms, LmsCourse course)
    {
        InitializeComponent();
        _lms = lms;
        _course = course;
        Title = L.T("download.title");
        Heading.Text = course.Subject + (course.Part is null ? "" : " · " + course.Part) + " · " + course.Term;
        Sub.Text = L.F("download.saveTo", $"{Config.Str("folders.subjects", "Môn học")}\\{course.Subject}");
        Extract.IsChecked = Config.Bool("archives.extract", true);
        Start.IsEnabled = false;
        Loaded += async (_, _) => await LoadAsync();
        Closing += (_, e) =>
        {
            if (_run is null) return;
            if (MessageBox.Show(this, L.T("download.confirmStop"), L.T("download.title"), MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) _run.Cancel();
            else e.Cancel = true;
        };
    }

    private async Task LoadAsync()
    {
        Summary.Text = L.T("download.reading");
        try
        {
            var list = await Task.Run(() => LmsSource.SectionsAsync(_course.Id, CancellationToken.None));
            _rows = list.Select(s => new SectionRow(s)).ToList();
            Sections.ItemsSource = _rows;
            Update();
            if (_rows.Count == 0) Summary.Text = L.T("download.empty");
        }
        catch (Exception e)
        {
            Log.Error("Lỗi đọc danh sách mục LMS", e);
            Summary.Text = L.F("download.readError", e.Message);
        }
    }

    private void Update()
    {
        var picked = _rows.Where(r => r.Selected).ToList();
        Summary.Text = L.F("download.summary", _rows.Count, picked.Count, picked.Sum(r => r.Info.Files), Format.Size(picked.Sum(r => r.Info.Bytes)));
        Start.IsEnabled = _run is null && picked.Count > 0;
    }

    private void OnPick(object sender, RoutedEventArgs e) => Update();
    private void OnAll(object sender, RoutedEventArgs e) { _rows.ForEach(r => r.Selected = true); Update(); }
    private void OnNone(object sender, RoutedEventArgs e) { _rows.ForEach(r => r.Selected = false); Update(); }
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        var picked = _rows.Where(r => r.Selected).Select(r => r.Info.Index).ToList();
        var extract = Extract.IsChecked == true;
        _run = new CancellationTokenSource();
        Start.IsEnabled = false;
        Sections.IsEnabled = false;
        Write(L.F("download.starting", picked.Count));
        try
        {
            var token = _run.Token;
            var n = await Task.Run(() => _lms.DownloadSectionsAsync(_course.Id, picked, extract, line => Dispatcher.InvokeAsync(() => Write(line)), token));
            Write(n == 0 ? L.T("download.doneNone") : L.F("download.done", n));
        }
        catch (OperationCanceledException) { Write(L.T("download.stopped")); }
        catch (Exception x)
        {
            Log.Error("Lỗi tải file theo mục", x);
            Write(L.F("download.error", x.Message));
        }
        finally
        {
            _run.Dispose();
            _run = null;
            Sections.IsEnabled = true;
            await LoadAsync();
        }
    }

    private void Write(string line)
    {
        Output.AppendText(line.TrimStart() + Environment.NewLine);
        Output.ScrollToEnd();
    }
}
