using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SoHocTap.Shell;

namespace SoHocTap.Ui.Pages;

/// <summary>Trang Giới thiệu (nút ⓘ trên thanh trên cùng), tách khỏi Cài đặt.</summary>
public partial class AboutPage : UserControl, IPage
{
    public AboutPage()
    {
        InitializeComponent();
        AboutName.Text = $"{AppInfo.Name} {AppInfo.Version}";
        AboutLine.Text = L.F("about.line", AppInfo.Author, AppInfo.License);
        Official.Text = L.F("about.official", AppInfo.Repo.Replace("https://", ""));
    }

    public string Title => L.T("about.title");
    public string Subtitle => "";
    public void Refresh() { }

    private static void OpenLink(string url) => Links.Open(url);
    private void OnSource(object sender, RoutedEventArgs e) => OpenLink(AppInfo.Repo);
    private void OnIssues(object sender, RoutedEventArgs e) => OpenLink(AppInfo.Issues);
    private void OnPrivacy(object sender, RoutedEventArgs e) => OpenLink(AppInfo.Repo + "/blob/main/PRIVACY.md");
}
