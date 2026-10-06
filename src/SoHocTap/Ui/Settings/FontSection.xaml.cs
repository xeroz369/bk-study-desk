using System.Windows;
using System.Windows.Controls;

namespace SoHocTap.Ui.SettingsCards;

public partial class FontSection : UserControl, ISettingsSection
{
    private sealed record SizeItem(double Size, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly List<FontItem> _families;
    private readonly List<SizeItem> _sizes;
    private bool _ready;

    /// <summary>Dòng đầu là Mặc định (Source rỗng = theo theme, tức phông của Windows), sau đó mọi phông trên máy.</summary>
    public FontSection()
    {
        InitializeComponent();
        _families = [new("", L.F("settings.font.default", AppFont.SystemFamily)), .. AppFont.Installed()];
        FontFamilyBox.ItemsSource = _families;
        _sizes = [new(0, L.F("settings.font.sizeDefault", Math.Round(AppFont.SystemSize, 1))), .. FontChoice.Sizes.Select(s => new SizeItem(s, L.F("settings.font.sizeN", s)))];
        FontSizeBox.ItemsSource = _sizes;
        Load();
    }

    public string TitleKey => "settings.font";

    public void Load()
    {
        _ready = false;
        try
        {
            var cur = AppFont.Current;
            FontFamilyBox.SelectedItem = _families.FirstOrDefault(f => string.Equals(f.Source, cur.Family, StringComparison.OrdinalIgnoreCase)) ?? _families[0];
            FontSizeBox.SelectedItem = _sizes.FirstOrDefault(s => s.Size == cur.Size) ?? _sizes[0];
        }
        finally { _ready = true; }
    }

    private void OnFontFamily(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && FontFamilyBox.SelectedItem is FontItem f) SetFont(AppFont.Current with { Family = f.Source });
    }

    private void OnFontSize(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && FontSizeBox.SelectedItem is SizeItem s) SetFont(AppFont.Current with { Size = s.Size });
    }

    private void OnFontReset(object sender, RoutedEventArgs e)
    {
        SetFont(FontChoice.Default);
        Load();
    }

    /// <summary>Áp ngay cho cả app và lưu app.font. Ghi config lỗi thì chữ vẫn đổi trong lần chạy này, báo ngay dưới thẻ.</summary>
    private void SetFont(FontChoice choice)
    {
        FontStatus.Text = "";
        try { AppFont.Set(choice); }
        catch (IOException x) { FontStatus.Text = L.F("settings.saveError", x.Message); }
    }
}
