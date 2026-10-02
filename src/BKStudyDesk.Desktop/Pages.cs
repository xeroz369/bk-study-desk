using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using SoHocTap.Core;
using SoHocTap.Shell;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop;

/// <summary>
/// Các trang chỉ đọc của bản đa nền tảng (bước 2). Dựng bằng code cho gọn; dữ liệu lấy từ AppState (lõi chung).
/// Trang có tương tác (đăng nhập, Luyện tập, Cài đặt) làm ở bước 4–5 của PLAN-DA-NEN-TANG.md.
/// </summary>
internal static class Pages
{
    public static string OsName() =>
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : RuntimeInformation.OSDescription;

    // ------------------------------------------------------------------ khối dựng chung

    private static TextBlock Title(string text) => new() { Text = text, Classes = { "title" }, Margin = new(0, 0, 0, 8) };
    private static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Empty(string text) => new() { Text = text, Classes = { "muted" }, Margin = new(0, 8) };

    private static Border Card(Control child) => new() { Classes = { "card" }, Child = child, Margin = new(0, 0, 0, 12) };

    private static ScrollViewer Scroll(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 0 };
        foreach (var c in children) panel.Children.Add(c);
        return new ScrollViewer { Content = panel };
    }

    /// <summary>Một hàng: cột trái cố định, cột giữa co giãn, cột phải căn phải.</summary>
    private static Grid Row(string left, string main, string sub, string right, bool strong = false)
    {
        var g = new Grid { ColumnDefinitions = new("70,*,Auto"), Margin = new(0, 3) };
        g.Children.Add(new TextBlock { Text = left, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
        var mid = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        mid.Inlines!.Add(new Run(main) { FontWeight = strong ? FontWeight.SemiBold : FontWeight.Normal });
        if (sub.Length > 0) mid.Inlines.Add(new Run("   " + sub) { FontSize = 12, Foreground = Brushes.Gray });
        Grid.SetColumn(mid, 1);
        g.Children.Add(mid);
        var r = new TextBlock { Text = right, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, Margin = new(12, 0, 0, 0) };
        Grid.SetColumn(r, 2);
        g.Children.Add(r);
        return g;
    }

    // ------------------------------------------------------------------ trang

    public static Control Today(AppState s)
    {
        var week = s.Upcoming(24 * 7).Where(e => e.Kind != "class" || Format.DayDiff(e.Time) == 0).ToList();
        var list = new StackPanel();
        foreach (var g in week.GroupBy(e => e.Group))
        {
            list.Children.Add(new TextBlock { Text = g.Key, FontWeight = FontWeight.SemiBold, Margin = new(0, 10, 0, 2) });
            foreach (var e in g) list.Children.Add(Row(e.Hour, e.Name, $"{e.KindName} · {e.Subject}", e.Left, e.Urgent));
        }
        if (week.Count == 0) list.Children.Add(Empty(L.T("home.agendaEmpty")));
        var exams = s.Timeline.Where(e => e.Kind == "exam" && e.Time > Format.Now).Take(5).ToList();
        var examList = new StackPanel();
        foreach (var e in exams) examList.Children.Add(Row(Format.DateTime(e.Time).Split(' ')[0], e.Name, "", Format.Until(e.Time)));
        if (exams.Count == 0) examList.Children.Add(Empty(L.T("home.examsEmpty")));
        return Scroll(Card(new StackPanel { Children = { Title(L.T("home.next7")), list } }),
                      Card(new StackPanel { Children = { Title(L.T("home.exams")), examList } }));
    }

    public static Control Calendar(AppState s)
    {
        var items = s.Timeline.Where(e => e.Time >= Format.Now - 86400 && e.Kind != "class").Take(200).ToList();
        var list = new StackPanel();
        foreach (var g in items.GroupBy(e => e.Group))
        {
            list.Children.Add(new TextBlock { Text = g.Key, FontWeight = FontWeight.SemiBold, Margin = new(0, 10, 0, 2) });
            foreach (var e in g) list.Children.Add(Row(e.Hour, e.Name, $"{e.KindName} · {e.Subject}", e.Left));
        }
        if (items.Count == 0) list.Children.Add(Empty(L.T("home.agendaEmpty")));
        return Scroll(Card(new StackPanel { Children = { Title(L.T("calendar.tabUpcoming")), list } }));
    }

    public static Control Subjects(AppState s)
    {
        var list = new StackPanel();
        var courses = s.Lms?.Courses.Where(c => c.Term == s.Lms.Term).GroupBy(c => c.Subject).OrderBy(g => g.Key).ToList() ?? [];
        foreach (var g in courses)
        {
            var folder = Path.Combine(SoHocTap.Files.Organizer.SubjectsRoot, g.Key);
            var count = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count() : 0;
            var row = new DockPanel { Margin = new(0, 4) };
            var open = new Button { Content = L.T("subjects.openFolder"), IsEnabled = Directory.Exists(folder) };
            open.Click += (_, _) => OpenPath(folder);
            DockPanel.SetDock(open, Dock.Right);
            row.Children.Add(open);
            row.Children.Add(new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { new TextBlock { Text = g.Key, FontWeight = FontWeight.SemiBold }, Muted($"{string.Join(", ", g.Select(c => c.Code).Distinct())} · {L.F("subjects.docs", count)}") },
            });
            list.Children.Add(row);
        }
        if (courses.Count == 0) list.Children.Add(Empty(L.T("subjects.folderMissing")));
        return Scroll(Card(new StackPanel { Children = { Title(L.T("subjects.current")), list } }));
    }

    public static Control Grades(AppState s)
    {
        var list = new StackPanel();
        var grades = s.Mybk?.Grades.OrderByDescending(g => g.Term).ThenBy(g => g.Name).ToList() ?? [];
        foreach (var g in grades.GroupBy(g => g.Term))
        {
            list.Children.Add(new TextBlock { Text = g.Key, FontWeight = FontWeight.SemiBold, Margin = new(0, 10, 0, 2) });
            foreach (var x in g) list.Children.Add(Row(x.Code, x.Name, x.Credits is { } c ? $"{c:0.#} TC" : "", $"{Format.Score(x.Score)}  {x.Letter}"));
        }
        if (grades.Count == 0) list.Children.Add(Empty(L.T("grades.noData")));
        var head = s.Mybk?.GradeTerms.FirstOrDefault();
        return Scroll(Card(new StackPanel
        {
            Children = { Title(L.T("grades.tabGrades")), Muted(head is null ? "" : $"{L.T("grades.gpaAll")}: {head.GpaAll}"), list },
        }));
    }

    public static Control About()
    {
        var links = new WrapPanel { Margin = new(0, 12, 0, 0) };
        foreach (var (label, url) in new[] { (L.T("about.source"), AppInfo.Repo), (L.T("about.issues"), AppInfo.Issues) })
        {
            var b = new Button { Content = label, Margin = new(0, 0, 8, 0) };
            b.Click += (_, _) => OpenUrl(url);
            links.Children.Add(b);
        }
        var body = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = $"{AppInfo.Name} {AppInfo.Version}", FontSize = 20, FontWeight = FontWeight.SemiBold },
                Muted(L.F("about.line", AppInfo.Author, AppInfo.License)),
                new TextBlock { Text = L.T("about.unofficial"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) },
                Muted($"Bản đa nền tảng (thử nghiệm) · {OsName()} · thư mục app: {Paths.AppRoot}"),
                links,
            },
        };
        if (Updater.Panel() is { } update) body.Children.Add(update);   // chỉ bản cài (AppImage), kiểm tra khi người dùng bấm
        return Scroll(Card(body));
    }

    // ------------------------------------------------------------------ mở file, link theo từng hệ điều hành

    private static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return;
        Launch(u.AbsoluteUri);
    }

    private static void OpenPath(string path)
    {
        if (Directory.Exists(path)) Launch(path);
    }

    private static void Launch(string target)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { ArgumentList = { target } });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { Log.Warn($"Không mở được {target}: {e.Message}"); }
    }
}
