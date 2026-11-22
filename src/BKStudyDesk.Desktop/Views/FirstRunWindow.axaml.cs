using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SoHocTap.Core;
using SoHocTap.Presentation;
using SoHocTap.Ui;
using SoHocTap.Updates;

namespace BKStudyDesk.Desktop.Views;

public partial class FirstRunWindow : Window
{
    private bool _saved;

    public FirstRunWindow() => InitializeComponent();

    internal static FirstRunWindow? Next()
    {
        if (FirstRun.NeedsFolder) return new FirstRunWindow().AskFolder();
        if (UpdateService.Supported && UpdateService.Mode == UpdateMode.Ask) return new FirstRunWindow().AskUpdateMode();
        return null;
    }

    internal static (FirstRunWindow Folder, FirstRunWindow Update) Both() => (new FirstRunWindow().AskFolder(), new FirstRunWindow().AskUpdateMode());

    private FirstRunWindow AskFolder()
    {
        Title = L.T("setup.title");
        FolderPanel.IsVisible = true;
        Folder.Text = Paths.SuggestedRoot;
        Closing += (_, _) => { if (!_saved) FirstRun.SaveFolder(Paths.SuggestedRoot); };
        return this;
    }

    private FirstRunWindow AskUpdateMode()
    {
        Title = L.T("update.title");
        UpdatePanel.IsVisible = true;
        ModeAuto.IsVisible = UpdateService.CanSelfUpdate;
        return this;
    }

    private void OnFolderChanged(object? sender, TextChangedEventArgs e) => Note.Text = FirstRun.FreeSpace(Folder.Text ?? "");

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L.T("settings.rootPick") });
        if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) Folder.Text = FirstRun.Picked(path);
    }

    private void OnSaveFolder(object? sender, RoutedEventArgs e)
    {
        if (FirstRun.SaveFolder((Folder.Text ?? "").Trim()) is { } error) { Note.Text = error; return; }
        _saved = true;
        Close();
    }

    private void OnSaveMode(object? sender, RoutedEventArgs e)
    {
        UpdateService.SetMode(ModeAuto.IsChecked == true ? UpdateMode.Auto : ModeOff.IsChecked == true ? UpdateMode.Off : UpdateMode.Notify);
        Close();
    }
}
