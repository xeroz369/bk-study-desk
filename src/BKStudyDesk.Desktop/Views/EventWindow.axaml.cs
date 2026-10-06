using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SoHocTap.Core;
using SoHocTap.Data;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Thêm, sửa một sự kiện tự thêm (cùng logic với 1.x, Ui/EventWindow): ô nhập nhanh đọc bằng QuickEntry.Parse và điền các ô; bấm Lưu thì
/// kiểm bằng QuickEntry.Validate, từ đó kiểm lại mỗi khi sửa ô. Kết quả của ShowDialog: sự kiện đã điền (giữ Id, CreatedAt khi sửa), null là Hủy.
/// </summary>
public partial class EventWindow : Window
{
    private readonly CustomEvent? _editing;
    private bool _filling;   // đang điền từ ô nhập nhanh: không kiểm từng ô
    private bool _tried;     // đã bấm Lưu một lần

    public EventWindow() => InitializeComponent();

    internal EventWindow(CustomEvent? editing, DateTime? date) : this()
    {
        _editing = editing;
        Title = Heading.Text = L.T(editing is null ? "events.titleNew" : "events.titleEdit");
        KindBox.ItemsSource = new[] { L.T("events.kindEvent"), L.T("events.kindMakeup") };
        KindBox.SelectedIndex = 0;
        _filling = true;
        if (editing is not null)
        {
            TitleBox.Text = editing.Title;
            DateBox.SelectedDate = editing.Day;
            StartBox.Text = editing.Start;
            EndBox.Text = editing.End;
            LocationBox.Text = editing.Location;
            KindBox.SelectedIndex = editing.IsMakeup ? 1 : 0;
            NoteBox.Text = editing.Note;
        }
        else DateBox.SelectedDate = date;
        _filling = false;
        Opened += (_, _) => (editing is null ? Quick : TitleBox).Focus();
    }

    private void OnQuick(object? sender, TextChangedEventArgs e)
    {
        if ((Quick.Text ?? "").Trim().Length == 0) { ShowError(QuickError, null); return; }
        var r = QuickEntry.Parse(Quick.Text, VnTime.Today);
        _filling = true;
        try
        {
            TitleBox.Text = r.Title;
            if (r.Date is { } d) DateBox.SelectedDate = d;
            if (r.Start is { } s) StartBox.Text = QuickEntry.Clock(s);
            EndBox.Text = r.End is { } en ? QuickEntry.Clock(en) : "";
            LocationBox.Text = r.Location;
            KindBox.SelectedIndex = r.Kind == CustomEvents.KindMakeup ? 1 : 0;
        }
        finally { _filling = false; }
        var bad = r.Errors.Where(p => !r.Missing.Contains(p.Key)).Select(p => L.T(p.Value)).Distinct().ToList();
        ShowError(QuickError, bad.Count == 0 ? null : string.Join(" ", bad));
        if (_tried) Check();
    }

    private void OnField(object? sender, TextChangedEventArgs e)
    {
        if (!_filling && _tried) Check();
    }

    private void OnDate(object? sender, SelectionChangedEventArgs e)
    {
        if (!_filling && _tried) Check();
    }

    private Dictionary<EntryField, string> Check()
    {
        var errors = QuickEntry.Validate(TitleBox.Text, DateBox.SelectedDate, StartBox.Text, EndBox.Text);
        ShowError(TitleError, errors.GetValueOrDefault(EntryField.Title));
        ShowError(DateError, errors.GetValueOrDefault(EntryField.Date));
        ShowError(StartError, errors.GetValueOrDefault(EntryField.Start));
        ShowError(EndError, errors.GetValueOrDefault(EntryField.End));
        return errors;
    }

    /// <summary>Lỗi là khóa ngôn ngữ (events.err.*) hay câu đã dịch sẵn; null thì ẩn.</summary>
    private static void ShowError(TextBlock target, string? keyOrText)
    {
        target.Text = keyOrText is null ? "" : keyOrText.StartsWith("events.err.", StringComparison.Ordinal) ? L.T(keyOrText) : keyOrText;
        target.IsVisible = keyOrText is not null;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _tried = true;
        var errors = Check();
        if (errors.Count > 0)
        {
            (errors.Keys.Min() switch { EntryField.Title => TitleBox, EntryField.Date => DateBox, EntryField.Start => StartBox, _ => (Control)EndBox }).Focus();
            return;
        }
        var start = VnTime.ParseClock(StartBox.Text) ?? 0;
        var end = VnTime.ParseClock(EndBox.Text);
        Close(new CustomEvent
        {
            Id = _editing?.Id ?? "",
            CreatedAt = _editing?.CreatedAt ?? 0,
            Title = (TitleBox.Text ?? "").Trim(),
            Date = (DateBox.SelectedDate ?? VnTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Start = QuickEntry.Clock(start),
            End = end is { } en ? QuickEntry.Clock(en) : "",
            Location = (LocationBox.Text ?? "").Trim(),
            Kind = KindBox.SelectedIndex == 1 ? CustomEvents.KindMakeup : CustomEvents.KindEvent,
            Note = (NoteBox.Text ?? "").Trim(),
        });
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
