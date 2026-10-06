using System.Windows;
using System.Windows.Controls;
using SoHocTap.Shell;
using SoHocTap.Ui.SettingsCards;

namespace SoHocTap.Ui.Pages;

/// <summary>
/// Trang Cài đặt: thẻ hay dùng (<see cref="SettingsSections.Common"/>) hiện ngay, thẻ nâng cao (<see cref="SettingsSections.Advanced"/>)
/// chỉ dựng lần đầu mở mục Nâng cao. Mỗi thẻ cách nhau 12.
/// </summary>
public partial class SettingsPage : UserControl, IPage
{
    private readonly AppHost _host;
    private readonly List<ISettingsSection> _sections = [];

    internal SettingsPage(AppHost host)
    {
        InitializeComponent();
        _host = host;
        Add(SettingsSections.Common, Cards);
        // Đổi trang (Ctrl+số thay Page.Content) không luôn kèm LostFocus: lưu chữ đang gõ dở khi trang rời cây giao diện.
        Unloaded += (_, _) => Flush();
    }

    private void Add(IEnumerable<Func<AppHost, ISettingsSection>> list, Panel into)
    {
        foreach (var create in list)
        {
            var section = create(_host);
            _sections.Add(section);
            var card = (FrameworkElement)section;
            card.Margin = new Thickness(0, 0, 0, 12);
            into.Children.Add(card);
        }
    }

    private void OnAdvanced(object sender, RoutedEventArgs e)
    {
        if (AdvancedCards.Children.Count == 0) Add(SettingsSections.Advanced, AdvancedCards);
    }

    public string Title => L.T("nav.settings");
    public string Subtitle => L.T("settings.subtitle");

    /// <summary>Dữ liệu vừa đổi (sync xong, đăng nhập, Kho quiz đổi công tắc): mọi thẻ đã dựng đọc lại; ô đang gõ thì giữ nguyên.</summary>
    public void Refresh()
    {
        foreach (var s in _sections) s.Load();
    }

    /// <summary>Cửa sổ xuống khay hoặc app thoát: lưu chữ còn gõ dở trong các ô.</summary>
    public void Sleep() => Flush();

    private void Flush()
    {
        foreach (var s in _sections) s.Flush();
    }
}
