using System.Windows;
using SoHocTap.Shell;

namespace SoHocTap.Ui;

public partial class LoginWindow : Window
{
    private bool _done;

    internal LoginWindow(Action<string, string> progress)
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await Web.EnsureCoreWebView2Async(await WebHost.EnvironmentAsync());
            // Riêng window này bật password manager của WebView2 (hỏi lưu, autofill); lưu hay không là do người dùng chọn.
            Web.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
            Web.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
            var flow = new LoginFlow(Web.CoreWebView2, progress);
            flow.Finished += async () => { _done = true; await Task.Delay(800); Close(); };
            flow.Start();
        };
        Closed += (_, _) => { if (!_done) progress("cancel", ""); };
    }
}
