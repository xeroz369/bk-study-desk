using Avalonia.Controls;
using SoHocTap.Shell;
using SoHocTap.Sources;
using SoHocTap.Sources.Lms;
using SoHocTap.Sources.Mybk;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

/// <summary>Cửa sổ chính bản đa nền tảng. Bước 2: chỉ đọc dữ liệu đã đồng bộ trong data\ (chưa đăng nhập, chưa đồng bộ).</summary>
public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly (string Key, Func<AppState, Control> Build)[] _pages =
    [
        ("nav.today", Pages.Today),
        ("nav.calendar", Pages.Calendar),
        ("nav.subjects", Pages.Subjects),
        ("nav.grades", Pages.Grades),
        ("about.title", _ => Pages.About()),
    ];

    public MainWindow()
    {
        InitializeComponent();
        // MyBK cần trình duyệt nhúng (bước 3–4); tạm thời chưa có runner, nên chỉ đọc dữ liệu đã có.
        var hub = new SourceHub([new LmsSource(), new MybkSource(() => null)]);
        _state = new AppState(hub);
        _state.Reload();

        Nav.ItemsSource = _pages.Select(p => L.T(p.Key)).ToList();
        Nav.SelectionChanged += (_, _) => Show(Nav.SelectedIndex);
        Nav.SelectedIndex = 0;

        StVersion.Text = $"{AppInfo.Name} {AppInfo.Version} · {Pages.OsName()}";
        StStatus.Text = _state.Lms is { } l ? L.F("status.syncedAt", "LMS", SoHocTap.Ui.Format.DateTime(l.SyncedAt)) : L.F("status.neverSynced", "LMS");
    }

    private void Show(int i)
    {
        if (i < 0 || i >= _pages.Length) return;
        Page.Content = _pages[i].Build(_state);
    }
}
