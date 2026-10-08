using SoHocTap.Shell;
using SoHocTap.Ui.Pages;

namespace SoHocTap.Ui;

/// <summary>
/// Một trang của cửa sổ chính. <paramref name="Key"/> là route ("lich/thi": trang lich, mở tab thi). <paramref name="InNav"/>: có mục trên thanh
/// điều hướng; <paramref name="Hotkey"/>: có phím Ctrl+số (theo thứ tự trong bảng).
/// </summary>
internal sealed record PageEntry(string Key, string LabelKey, string TipKey, string Glyph, Func<AppHost, MainWindow, IPage> Create, bool InNav, bool Hotkey);

/// <summary>
/// Bảng các trang: thanh điều hướng, phím Ctrl+số, route (kể cả route từ khung Luyện tập) đều đọc từ đây. Thêm trang = thêm một dòng.
/// </summary>
internal static class PageRegistry
{
    public const string Home = "hom-nay";
    public const string Calendar = "lich";
    public const string Subjects = "mon";
    public const string Practice = "luyen-tap";
    public const string Grades = "diem";
    public const string Services = "dich-vu";
    public const string Settings = "cai-dat";
    public const string About = "gioi-thieu";

    public static readonly IReadOnlyList<PageEntry> All =
    [
        new(Home, "nav.today", "nav.today.tip", "", (h, m) => new HomePage(h, m), InNav: true, Hotkey: true),
        new(Calendar, "nav.calendar", "nav.calendar.tip", "", (h, m) => new CalendarPage(h, m), InNav: true, Hotkey: true),
        new(Subjects, "nav.subjects", "nav.subjects.tip", "", (h, m) => new SubjectsPage(h, m), InNav: true, Hotkey: true),
        new(Practice, "nav.practice", "nav.practice.tip", "", (h, m) => new PracticePage(h, m), InNav: true, Hotkey: true),
        new(Grades, "nav.grades", "nav.grades.tip", "", (h, m) => new GradesPage(h, m), InNav: true, Hotkey: true),
        new(Services, "nav.services", "nav.services.tip", "", (h, m) => new ServicesPage(h, m), InNav: true, Hotkey: true),
        // Cài đặt, Giới thiệu là nút bên phải thanh trên cùng; Cài đặt vẫn có phím Ctrl+số ngay sau các mục điều hướng.
        new(Settings, "nav.settings", "nav.settings.tip", "", (h, _) => new SettingsPage(h), InNav: false, Hotkey: true),
        new(About, "about.title", "about.tip", "", (_, _) => new AboutPage(), InNav: false, Hotkey: false),
    ];

    /// <summary>Tên khác của trang: route cũ, route của khung Luyện tập ("tl" là tài liệu, "mybk" là trang MyBK cũ). KeepArg: giữ phần sau "/".</summary>
    private static readonly Dictionary<string, (string To, bool KeepArg)> Aliases = new(StringComparer.Ordinal)
    {
        ["mon-hoc"] = ("mon", true),
        ["tl"] = ("mon", false),
        ["mybk"] = ("diem", false),
    };

    public static IEnumerable<PageEntry> Nav => All.Where(p => p.InNav);

    /// <summary>Các trang có phím Ctrl+1 đến Ctrl+N, theo thứ tự.</summary>
    public static IReadOnlyList<PageEntry> Hotkeys { get; } = [.. All.Where(p => p.Hotkey)];

    public static PageEntry? Find(string key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>
    /// Route ("mon/Giải tích 2", "lich/thi", "tl", "mybk"...) thành (trang, phần còn lại). Không nhận ra thì về Hôm nay, như trước giờ.
    /// </summary>
    public static (PageEntry Page, string Arg) Resolve(string route)
    {
        var parts = (route ?? "").Split('/', 2);
        var key = parts[0];
        var arg = parts.Length > 1 ? parts[1] : "";
        if (Aliases.TryGetValue(key, out var alias))
        {
            key = alias.To;
            if (!alias.KeepArg) arg = "";
        }
        return Find(key) is { } page ? (page, arg) : (Find(Home)!, "");
    }

    /// <summary>Ctrl+n: trang thứ n trong <see cref="Hotkeys"/>, ngoài khoảng thì null.</summary>
    public static PageEntry? ByHotkey(int n) => n >= 1 && n <= Hotkeys.Count ? Hotkeys[n - 1] : null;
}
