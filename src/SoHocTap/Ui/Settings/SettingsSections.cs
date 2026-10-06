using SoHocTap.Shell;

namespace SoHocTap.Ui.SettingsCards;

/// <summary>
/// Các thẻ của trang Cài đặt, theo thứ tự hiện. Mỗi thẻ một dòng (test đọc hai danh sách này).
/// <see cref="Common"/> là thẻ hay dùng, hiện ngay; <see cref="Advanced"/> nằm trong mục Nâng cao, gập sẵn, chỉ dựng khi mở ra.
/// </summary>
internal static class SettingsSections
{
    public static IReadOnlyList<Func<AppHost, ISettingsSection>> Common { get; } =
    [
        h => new AccountSection(h),
        _ => new RemindersSection(),
        _ => new FontSection(),
        _ => new LanguageSection(),
        _ => new StartupSection(),
        h => new UpdateSection(h),
    ];

    public static IReadOnlyList<Func<AppHost, ISettingsSection>> Advanced { get; } =
    [
        h => new SyncSection(h),
        _ => new OpenDocsSection(),
        h => new LibrarySection(h),
        _ => new OrganizeSection(),
        _ => new DiagnosticsSection(),
        _ => new KeysSection(),
    ];
}
