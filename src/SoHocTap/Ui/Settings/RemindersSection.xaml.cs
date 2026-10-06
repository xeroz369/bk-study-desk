using System.Windows.Controls;
using SoHocTap.Core;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>Nhắc hạn: mọi ô cho phép 0 (tắt lần nhắc hay mốc màu đó; DeadlineNotifier và Due.Of bỏ các mốc bằng 0).</summary>
public partial class RemindersSection : UserControl, ISettingsSection
{
    private readonly NumberSetting[] _numbers;
    private readonly TextSetting _digest;

    public RemindersSection()
    {
        InitializeComponent();
        _numbers =
        [
            new(NotifyHours, NotifyHoursError, 0, () => Settings.Notify.FirstHours, v => Settings.Notify.FirstHours = v),
            new(LastHours, LastHoursError, 0, () => Settings.Notify.LastHours, v => Settings.Notify.LastHours = v),
            new(UrgentHours, UrgentHoursError, 0, () => Settings.Notify.UrgentHours, v => Settings.Notify.UrgentHours = v, ShowHint),
            new(SoonHours, SoonHoursError, 0, () => Settings.Notify.SoonHours, v => Settings.Notify.SoonHours = v, ShowHint),
        ];
        _digest = new(DigestTimes, DigestTimesError, () => Settings.Notify.DigestTimes,
            s => NotifyDigest.Parse(s) is { } slots ? NotifyDigest.Format(slots) : null, v => Settings.Notify.DigestTimes = v, L.T("settings.digestTimesError"));
        Load();
    }

    public string TitleKey => "settings.reminders";

    public void Load()
    {
        foreach (var n in _numbers) n.Load();
        _digest.Load();
        ShowHint();
    }

    public void Flush()
    {
        foreach (var n in _numbers) n.Flush();
        _digest.Flush();
    }

    private void ShowHint() =>
        SoonHint.Visibility = Due.SoonHidden(Settings.Notify.UrgentHours, Settings.Notify.SoonHours) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
}
