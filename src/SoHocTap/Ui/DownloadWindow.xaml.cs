using System.ComponentModel;
using System.Windows;
using SoHocTap.Core;
using SoHocTap.Sources.Lms;

namespace SoHocTap.Ui;

/// <summary>Một mục trong danh sách. Số file và dung lượng tính theo các loại file đang chọn (<paramref name="kinds"/>).</summary>
internal sealed class SectionRow(LmsSource.SectionInfo s, Func<IReadOnlySet<LmsSource.FileKind>> kinds) : INotifyPropertyChanged
{
    private bool _selected = s.Items.Any(f => !f.Have);   // default tick sẵn mục còn thiếu file
    public LmsSource.SectionInfo Info { get; } = s;
    public string Name => Info.Name;
    public List<LmsSource.FileStat> Files => [.. Info.Of(kinds())];
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
    public void KindsChanged() => PropertyChanged?.Invoke(this, new(nameof(Meta)));
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
    private bool _ready;   // InitializeComponent đã xong

    internal DownloadWindow(LmsSource lms, LmsCourse course)
    {
        InitializeComponent();
        _ready = true;
        _lms = lms;
        _course = course;
        Title = L.T("download.title");
        Heading.Text = course.Subject + (course.Part is null ? "" : ", " + course.Part) + ", " + course.Term;
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
            _rows = list.Select(s => new SectionRow(s, Kinds)).ToList();
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

    /// <summary>Các loại file đang chọn ở hàng "Loại file".</summary>
    private HashSet<LmsSource.FileKind> Kinds()
    {
        var k = new HashSet<LmsSource.FileKind>();
        if (KindPdf.IsChecked == true) k.Add(LmsSource.FileKind.Pdf);
        if (KindSlide.IsChecked == true) k.Add(LmsSource.FileKind.Slide);
        if (KindOther.IsChecked == true) k.Add(LmsSource.FileKind.Other);
        return k;
    }

    private void Update()
    {
        var picked = _rows.Where(r => r.Selected).ToList();
        var files = picked.SelectMany(r => r.Files).ToList();
        Summary.Text = L.F("download.summary", _rows.Count, picked.Count, files.Count, Format.Size(files.Sum(f => f.Bytes)));
        Start.IsEnabled = _run is null && files.Count > 0;
    }

    private void OnPick(object sender, RoutedEventArgs e) => Update();

    private void OnKinds(object sender, RoutedEventArgs e)
    {
        // Ô chọn loại file đặt IsChecked="True" trong XAML nên Checked chạy ngay trong InitializeComponent,
        // lúc Summary/Start chưa được tạo (lỗi NullReferenceException ở 1.1.3). Chưa dựng xong cửa sổ thì bỏ qua.
        if (!_ready) return;
        _rows.ForEach(r => r.KindsChanged());
        Update();
    }
    private void OnAll(object sender, RoutedEventArgs e) { _rows.ForEach(r => r.Selected = true); Update(); }
    private void OnNone(object sender, RoutedEventArgs e) { _rows.ForEach(r => r.Selected = false); Update(); }
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        var picked = _rows.Where(r => r.Selected).Select(r => r.Info.Index).ToList();
        var extract = Extract.IsChecked == true;
        var kinds = Kinds();
        _run = new CancellationTokenSource();
        Start.IsEnabled = false;
        Sections.IsEnabled = false;
        Write(L.F("download.starting", picked.Count));
        try
        {
            var token = _run.Token;
            var n = await Task.Run(() => _lms.DownloadSectionsAsync(_course.Id, picked, extract, line => Dispatcher.InvokeAsync(() => Write(line)), token, kinds));
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
