using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Tải tài liệu của một lớp theo từng mục (bản 1.x: Ui/DownloadWindow). Danh sách mục lấy thẳng từ LMS; file lưu vào thư mục môn như
/// lúc đồng bộ (bỏ file trùng nội dung, bản cũ chuyển vào _Lưu trữ). Đóng cửa sổ khi đang tải thì dừng tải.
/// </summary>
public partial class DownloadWindow : Window
{
    private readonly LmsSource _lms = null!;
    private readonly LmsCourse _course = null!;
    private List<SectionRow> _rows = [];
    private CancellationTokenSource? _run;

    public DownloadWindow() => InitializeComponent();

    internal DownloadWindow(LmsSource lms, LmsCourse course) : this()
    {
        _lms = lms;
        _course = course;
        Heading.Text = course.Subject + (course.Part is null ? "" : ", " + course.Part) + ", " + course.Term;
        Sub.Text = L.F("download.saveTo", $"{Config.Str("folders.subjects", "Môn học")}/{course.Subject}");
        Extract.IsChecked = SoHocTap.Core.Settings.Archives.Extract;
        Start.IsEnabled = false;
        Opened += async (_, _) => await LoadAsync();
        Closing += (_, _) => _run?.Cancel();
    }

    /// <summary>Sau khi đóng: có thể vừa thêm file, trang Môn học đọc lại thư mục.</summary>
    public event Action? Downloaded;

    private async Task LoadAsync()
    {
        if (_rows.Count == 0) State(L.T("download.reading"));
        Summary.Text = "";
        try
        {
            var list = await Task.Run(() => LmsSource.SectionsAsync(_course.Id, CancellationToken.None));
            _rows = [.. list.Select(s => new SectionRow(s, Kinds))];
            Sections.ItemsSource = _rows;
            Update();
            if (_rows.Count == 0) State(L.T("download.empty"));
            else ListState.IsVisible = false;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Error("Lỗi đọc danh sách mục LMS", e);
            _rows = [];
            Sections.ItemsSource = null;
            Update();
            State(L.F("download.readError", e.Message));
        }
    }

    private void State(string text)
    {
        ListState.Text = text;
        ListState.IsVisible = true;
    }

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
        Summary.Text = SectionRow.Summary(_rows);
        Start.IsEnabled = _run is null && _rows.Where(r => r.Selected).SelectMany(r => r.Files).Any();
        AllButton.IsEnabled = NoneButton.IsEnabled = _run is null && _rows.Count > 0;
    }

    private void OnPick(object? sender, RoutedEventArgs e) => Update();

    private void OnKinds(object? sender, RoutedEventArgs e)
    {
        _rows.ForEach(r => r.KindsChanged());
        Update();
    }

    private void OnAll(object? sender, RoutedEventArgs e) { _rows.ForEach(r => r.Selected = true); Update(); }
    private void OnNone(object? sender, RoutedEventArgs e) { _rows.ForEach(r => r.Selected = false); Update(); }
    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private async void OnStart(object? sender, RoutedEventArgs e)
    {
        var picked = _rows.Where(r => r.Selected).Select(r => r.Info.Index).ToList();
        var extract = Extract.IsChecked == true;
        var kinds = Kinds();
        _run = new CancellationTokenSource();
        Start.IsEnabled = Sections.IsEnabled = AllButton.IsEnabled = NoneButton.IsEnabled = false;
        Output.IsVisible = true;
        Write(L.F("download.starting", picked.Count));
        try
        {
            var token = _run.Token;
            var n = await Task.Run(() => _lms.DownloadSectionsAsync(_course.Id, picked, extract, line => Dispatcher.UIThread.Post(() => Write(line)), token, kinds));
            Write(n == 0 ? L.T("download.doneNone") : L.F("download.done", n));
            Downloaded?.Invoke();
        }
        catch (OperationCanceledException) { Write(L.T("download.stopped")); }
        catch (Exception x) when (x is not OutOfMemoryException)
        {
            Log.Error("Lỗi tải file theo mục", x);
            Write(L.F("download.error", x.Message));
        }
        finally
        {
            _run.Dispose();
            _run = null;
            Sections.IsEnabled = true;
            if (IsVisible) await LoadAsync();
        }
    }

    private void Write(string line)
    {
        Output.Text += line.TrimStart() + Environment.NewLine;
        Output.CaretIndex = Output.Text.Length;
    }
}
