using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using SoHocTap.Core;
using SoHocTap.Data;

namespace SoHocTap.Ui;

/// <summary>
/// Form thêm hoặc sửa một sự kiện tự thêm (issue #22). Gõ ô Nhập nhanh thì các ô bên dưới điền theo (<see cref="QuickEntry.Parse"/>);
/// sửa thẳng các ô cũng được. Lỗi hiện ngay dưới ô sai; lúc đang gõ nhập nhanh chỉ báo phần gõ sai, chưa báo phần chưa gõ tới.
/// Form chỉ trả về sự kiện (<see cref="Result"/>), việc lưu file do trang Lịch làm.
/// </summary>
public partial class EventWindow : Window
{
    private readonly CustomEvent? _editing;
    private bool _filling;   // đang điền từ ô nhập nhanh: không chạy kiểm tra từng ô
    private bool _tried;     // đã bấm Lưu một lần: từ đó kiểm tra lại mỗi khi sửa ô

    public CustomEvent? Result { get; private set; }

    private EventWindow(CustomEvent? editing, DateTime? date)
    {
        InitializeComponent();
        Shell.WindowPlacement.FitToWorkArea(this);
        // DatePicker đọc, ghi ngày theo ngôn ngữ của app (dd/MM/yyyy với tiếng Việt), không theo ngôn ngữ của Windows.
        Language = XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
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
        Loaded += (_, _) => (editing is null ? Quick : (Control)TitleBox).Focus();
    }

    /// <summary>Mở form; trả về sự kiện đã điền (giữ Id, CreatedAt khi sửa) hoặc null nếu Hủy.</summary>
    internal static CustomEvent? Ask(Window? owner, CustomEvent? editing = null, DateTime? date = null)
    {
        var w = new EventWindow(editing, date) { Owner = owner };
        return w.ShowDialog() == true ? w.Result : null;
    }

    private void OnQuick(object sender, TextChangedEventArgs e)
    {
        if (Quick.Text.Trim().Length == 0) { ShowError(QuickError, null); return; }
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

    private void OnField(object sender, RoutedEventArgs e)
    {
        if (!_filling && _tried) Check();
    }

    /// <summary>
    /// Ngày đang chọn. Ô ngày của DatePicker chỉ cập nhật SelectedDate khi rời ô, nên gõ ngày rồi Enter ngay thì đọc chữ trong ô:
    /// rỗng là chưa chọn (null, đúng), gõ mà không đọc được thì báo sai.
    /// </summary>
    private (DateTime? Date, bool Bad) PickedDate()
    {
        var text = DateBox.Text?.Trim() ?? "";
        if (text.Length == 0) return (null, false);
        if (DateTime.TryParse(text, L.Culture, DateTimeStyles.None, out var typed)) return (typed.Date, false);
        return QuickEntry.ParseDate(text, VnTime.Today) is { } q ? (q, false) : (null, true);
    }

    private Dictionary<EntryField, string> Check()
    {
        var (date, dateBad) = PickedDate();
        var errors = QuickEntry.Validate(TitleBox.Text, date, StartBox.Text, EndBox.Text);
        if (dateBad) errors[EntryField.Date] = QuickEntry.ErrDateBad;
        ShowError(TitleError, errors.GetValueOrDefault(EntryField.Title));
        ShowError(DateError, errors.GetValueOrDefault(EntryField.Date));
        ShowError(StartError, errors.GetValueOrDefault(EntryField.Start));
        ShowError(EndError, errors.GetValueOrDefault(EntryField.End));
        return errors;
    }

    private static void ShowError(TextBlock target, string? keyOrText)
    {
        target.Text = keyOrText is null ? "" : keyOrText.StartsWith("events.err.", StringComparison.Ordinal) ? L.T(keyOrText) : keyOrText;
        target.Visibility = keyOrText is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _tried = true;
        var errors = Check();
        if (errors.Count > 0)
        {
            var first = errors.Keys.Min();
            (first switch { EntryField.Title => TitleBox, EntryField.Date => DateBox, EntryField.Start => StartBox, _ => (Control)EndBox }).Focus();
            return;
        }
        var date = PickedDate().Date ?? VnTime.Today;
        var start = VnTime.ParseClock(StartBox.Text) ?? 0;
        var end = VnTime.ParseClock(EndBox.Text);
        Result = new CustomEvent
        {
            Id = _editing?.Id ?? "",
            CreatedAt = _editing?.CreatedAt ?? 0,
            Title = TitleBox.Text.Trim(),
            Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Start = QuickEntry.Clock(start),
            End = end is { } en ? QuickEntry.Clock(en) : "",
            Location = LocationBox.Text.Trim(),
            Kind = KindBox.SelectedIndex == 1 ? CustomEvents.KindMakeup : CustomEvents.KindEvent,
            Note = NoteBox.Text.Trim(),
        };
        DialogResult = true;
    }
}
