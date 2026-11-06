using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using BKStudyDesk.Desktop.Files;
using SoHocTap.Core;
using SoHocTap.Presentation;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views;

/// <summary>
/// Vẽ nội dung đọc (ReadingBlock): đoạn chữ chọn và sao chép được; ảnh LMS đã tải về (LmsImages) hiện thẳng, xem được khi LMS sập.
/// Ảnh chưa tải thì là link "[ảnh]" mở bản trên LMS (cần mạng). Ảnh lớn thu lại vừa khung, giữ tỉ lệ.
/// </summary>
internal static class ReadingBlocks
{
    public static void Add(Panel panel, IEnumerable<ReadingBlock> blocks, Control owner)
    {
        foreach (var b in blocks)
        {
            if (b.Image is not { Length: > 0 } url)
            {
                panel.Children.Add(b.Spans is { } spans ? WithLinks(spans, owner) : new SelectableTextBlock { Text = b.Text, TextWrapping = TextWrapping.Wrap });
                continue;
            }
            if (LmsImages.LocalFile(url) is { } file)
            {
                try
                {
                    // Ảnh chụp màn hình, ảnh điện thoại thường rất lớn: giải mã thu nhỏ còn 1200 điểm ảnh ngang cho đỡ tốn RAM.
                    var bitmap = new Bitmap(file);
                    if (bitmap.PixelSize.Width > 1200)
                    {
                        bitmap.Dispose();
                        using var stream = File.OpenRead(file);
                        bitmap = Bitmap.DecodeToWidth(stream, 1200);
                    }
                    panel.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 600, MaxHeight = 800, HorizontalAlignment = HorizontalAlignment.Left });
                    continue;
                }
                catch (Exception e) when (e is not OutOfMemoryException) { Log.Debug($"Ảnh {Path.GetFileName(file)}: {e.Message}"); }
            }
            var link = new HyperlinkButton { Content = L.T("reader.image"), Padding = new Thickness(0) };
            var web = LmsReading.Browsable(new SoHocTap.Data.LmsLink(null, url)).Url!;
            ToolTip.SetTip(link, web);
            link.Click += async (_, _) => await Links.OpenAsync(owner, web);
            panel.Children.Add(link);
        }
    }

    /// <summary>
    /// Đoạn chữ có link như trên LMS: chữ thường là Run, link là nút link nằm trong dòng (bấm mở bằng trình duyệt theo Cài đặt, rê chuột
    /// thấy địa chỉ). Đoạn này không chọn chữ được như đoạn thường (SelectableTextBlock không nhận control trong dòng).
    /// </summary>
    private static TextBlock WithLinks(IReadOnlyList<ReadingSpan> spans, Control owner)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var s in spans)
        {
            if (s.Url is not { } url) { text.Inlines!.Add(new Avalonia.Controls.Documents.Run(s.Text)); continue; }
            var web = LmsReading.Browsable(new SoHocTap.Data.LmsLink(s.Text, url)).Url!;
            var link = new HyperlinkButton
            {
                Content = new TextBlock { Text = s.Text, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 },   // vừa ngăn Hộp thư
                Padding = new Thickness(0), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Bottom,
            };
            ToolTip.SetTip(link, web);
            link.Click += async (_, _) => await Links.OpenAsync(owner, web, s.Text);
            text.Inlines!.Add(new Avalonia.Controls.Documents.InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom });
        }
        return text;
    }
}
