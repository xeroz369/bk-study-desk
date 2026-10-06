using System.Windows;
using System.Windows.Controls;
using SoHocTap.Core;

namespace SoHocTap.Ui.SettingsCards;

internal sealed record LanguageItem(string Code, string Name)
{
    public override string ToString() => Name;   // tên cho screen reader đọc
}

public partial class LanguageSection : UserControl, ISettingsSection
{
    private readonly List<LanguageItem> _langs = L.Available().Select(x => new LanguageItem(x.Code, x.Name)).ToList();
    private bool _ready;

    public LanguageSection()
    {
        InitializeComponent();
        LanguageBox.ItemsSource = _langs;
        Load();
    }

    public string TitleKey => "settings.language";

    public void Load()
    {
        _ready = false;
        try { LanguageBox.SelectedItem = _langs.FirstOrDefault(x => x.Code == Settings.App.Language) ?? _langs.FirstOrDefault(x => x.Code == L.Code); }
        finally { _ready = true; }
        ShowRestart();
    }

    /// <summary>Chọn ngôn ngữ: lưu ngay vào app.language, UI đổi sau khi restart app.</summary>
    private void OnLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || LanguageBox.SelectedItem is not LanguageItem item) return;
        SettingsUi.Save(() => Settings.App.Language = item.Code, Error);
        ShowRestart();
    }

    private void ShowRestart()
    {
        var pending = Settings.App.Language != L.Code;
        LanguageNote.Visibility = RestartButton.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRestart(object sender, RoutedEventArgs e) => SettingsUi.Restart();
}
