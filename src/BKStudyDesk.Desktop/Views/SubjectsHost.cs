using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Trang Môn học gồm hai phần chọn ở đầu trang: Môn của bạn (SubjectsView) và Thư viện (LibraryView, tài liệu chung của mọi môn).
/// Thư viện thôi là một trang riêng trên thanh trên cùng (người dùng chốt 05/11/2026). Phần nào đang xem thì MainWindow giữ qua lần dựng
/// lại; tab bên trong thẻ môn (ITabbedPage) vẫn là tab của SubjectsView.
/// </summary>
internal sealed class SubjectsHost : UserControl, IFillPage, ITabbedPage
{
    private readonly TabStrip _switch = new() { Margin = new Thickness(0, 0, 0, 16) };
    private readonly ContentControl _body = new();
    private readonly Func<SubjectsView> _subjects;
    private readonly Func<LibraryView> _library;
    private readonly Action<bool> _remember;
    private SubjectsView? _shownSubjects;

    public SubjectsHost(bool library, Func<SubjectsView> subjects, Func<LibraryView> libraryView, Action<bool> remember)
    {
        (_subjects, _library, _remember) = (subjects, libraryView, remember);
        _switch.ItemsSource = new[]
        {
            new TabStripItem { Content = new TextBlock { Text = L.T("nav.subjects"), Classes = { "title" } } },
            new TabStripItem { Content = new TextBlock { Text = L.T("nav.library"), Classes = { "title" } } },
        };
        _switch.SelectedIndex = library ? 1 : 0;
        _switch.SelectionChanged += (_, _) => Fill();
        var page = new DockPanel();
        DockPanel.SetDock(_switch, Dock.Top);
        page.Children.Add(_switch);
        page.Children.Add(_body);
        Content = page;
        Fill();
    }

    public bool ShowsLibrary => _switch.SelectedIndex == 1;

    private void Fill()
    {
        _remember(ShowsLibrary);
        if (ShowsLibrary)
        {
            var lib = _library();
            lib.HideTitle();
            _body.Content = lib;
            _shownSubjects = null;
        }
        else
        {
            var s = _subjects();
            s.HideTitle();
            _body.Content = s;
            _shownSubjects = s;
        }
    }

    /// <summary>Tab của thẻ môn (MainWindow nhớ theo ui.tab.mon); đang xem Thư viện thì không có tab.</summary>
    public int Tab
    {
        get => _shownSubjects?.Tab ?? 0;
        set { if (_shownSubjects is { } s) s.Tab = value; }
    }
}
