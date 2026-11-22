using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

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

    public int Tab
    {
        get => _shownSubjects?.Tab ?? 0;
        set { if (_shownSubjects is { } s) s.Tab = value; }
    }
}
