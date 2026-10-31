using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using BKStudyDesk.Desktop.Files;
using SoHocTap.Core;
using SoHocTap.Presentation;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Đọc một nội dung LMS đã lưu trên máy (bài thông báo, trang, đề bài): đọc được khi LMS sập hay mất mạng.
/// Chữ chọn và sao chép được; link và file đính kèm mở bằng trình duyệt trong app (cần mạng). Nút Mở trên LMS mở bản gốc.
/// </summary>
internal sealed class ReaderWindow : Window
{
    private ReaderWindow(Reading r)
    {
        Title = r.Title;
        Width = 720;
        Height = 640;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        using (var icon = AssetLoader.Open(new Uri("avares://BKStudyDesk/Assets/app.ico"))) Icon = new WindowIcon(icon);
        this.Bind(BackgroundProperty, this.GetResourceObservable("PageBg"));
        Content = new ScrollViewer { Content = Body(r, this), Padding = new Thickness(24) };
    }

    public static void Open(Control from, Reading r)
    {
        var w = new ReaderWindow(r);
        if (TopLevel.GetTopLevel(from) is Window owner) w.Show(owner);
        else w.Show();
    }

    /// <summary>Nội dung đọc: tiêu đề, dòng phụ, các đoạn, link, đính kèm, nút Mở trên LMS. Trang Tin nhắn dùng lại phần đoạn chữ.</summary>
    public static Control Body(Reading r, Control owner)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new SelectableTextBlock { Text = r.Title, Classes = { "title" }, TextWrapping = TextWrapping.Wrap });
        if (r.Meta.Length > 0) panel.Children.Add(new TextBlock { Text = r.Meta, Classes = { "meta" } });
        if (r.Paragraphs.Count == 0) panel.Children.Add(new TextBlock { Text = L.T("reader.empty"), Classes = { "meta" } });
        foreach (var p in r.Paragraphs) panel.Children.Add(new SelectableTextBlock { Text = p, TextWrapping = TextWrapping.Wrap });
        AddLinks(panel, L.T("reader.links"), r.Links, owner);
        AddLinks(panel, L.T("reader.attachments"), r.Attachments, owner);
        if (r.WebUrl is { Length: > 0 } url)
        {
            var open = new Button { Content = L.T("reader.openLms"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            open.Click += async (_, _) => await Links.OpenAsync(owner, url, r.Title);
            panel.Children.Add(open);
        }
        return panel;
    }

    private static void AddLinks(StackPanel panel, string heading, IReadOnlyList<SoHocTap.Data.LmsLink> links, Control owner)
    {
        var usable = links.Where(l => !string.IsNullOrEmpty(l.Url)).ToList();
        if (usable.Count == 0) return;
        panel.Children.Add(new TextBlock { Text = heading, Classes = { "meta" }, Margin = new Thickness(0, 8, 0, 0) });
        foreach (var l in usable)
        {
            var link = new HyperlinkButton { Content = new TextBlock { Text = l.Name ?? l.Url, TextWrapping = TextWrapping.Wrap }, Padding = new Thickness(0) };
            ToolTip.SetTip(link, l.Url);
            // File đã tải về máy (đính kèm thông báo): mở bản trên máy, không cần mạng.
            link.Click += async (_, _) =>
            {
                if (l.Path is { Length: > 0 } local && await Opener.OpenAsync(owner, local)) return;
                await Links.OpenAsync(owner, l.Url!, l.Name ?? "");
            };
            panel.Children.Add(link);
        }
    }
}
