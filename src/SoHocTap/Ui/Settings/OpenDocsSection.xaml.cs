using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SoHocTap.Core;

namespace SoHocTap.Ui.SettingsCards;

public partial class OpenDocsSection : UserControl, ISettingsSection
{
    public OpenDocsSection()
    {
        InitializeComponent();
        Load();
    }

    public string TitleKey => "settings.openDocs";

    public void Load()
    {
        SettingsUi.ShowText(PdfApp, Settings.OpenDocs.PdfApp);
        SettingsUi.ShowText(Root, Settings.OpenDocs.Root);
        RootNote.Text = L.F("settings.rootNote", Paths.StudyRoot);
    }

    public void Flush()
    {
        SavePdf();
        SaveRoot();
    }

    /// <summary>Đường dẫn dán từ Explorer hay có ngoặc kép hai đầu: bỏ đi trước khi lưu.</summary>
    private static string Clean(string s) => s.Trim().Trim('"');

    private void SavePdf()
    {
        var v = Clean(PdfApp.Text);
        if (PdfApp.Text != v) PdfApp.Text = v;
        if (v != Settings.OpenDocs.PdfApp) SettingsUi.Save(() => Settings.OpenDocs.PdfApp = v, Error);
    }

    private void SaveRoot()
    {
        var v = Clean(Root.Text);
        if (Root.Text != v) Root.Text = v;
        if (v != Settings.OpenDocs.Root) SettingsUi.Save(() => Settings.OpenDocs.Root = v, Error);
    }

    private void OnPdfCommit(object sender, RoutedEventArgs e) => SavePdf();
    private void OnRootCommit(object sender, RoutedEventArgs e) => SaveRoot();

    private void OnPdfLeave(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!SettingsUi.ToMenu(e)) SavePdf();
    }

    private void OnRootLeave(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!SettingsUi.ToMenu(e)) SaveRoot();
    }

    private void OnPdfKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SavePdf();
    }

    private void OnRootKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SaveRoot();
    }

    private void OnBrowsePdf(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = L.T("settings.exeFilter"), Title = L.T("settings.pickPdf") };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        PdfApp.Text = d.FileName;
        SavePdf();
    }

    private void OnBrowseRoot(object sender, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = L.T("settings.rootPick"), InitialDirectory = Paths.StudyRoot };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        Root.Text = d.FolderName;
        SaveRoot();
    }
}
