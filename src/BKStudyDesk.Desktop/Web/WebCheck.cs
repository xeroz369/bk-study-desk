using SoHocTap.Core;

namespace BKStudyDesk.Desktop.Web;

internal static class WebCheck
{
    public static async Task<bool> RunAsync()
    {
        var hidden = new HiddenWeb();
        var page = hidden.Open(allow: null);
        var loaded = new TaskCompletionSource();
        page.Loaded += (_, _) => loaded.TrySetResult();
        page.View.NavigateToString("<html><body><input id='hid_Token' value='abc'></body></html>", new Uri("https://example.test/"));
        await Task.WhenAny(loaded.Task, Task.Delay(20000));
        var token = await page.EvalAsync("document.getElementById('hid_Token').value");
        var late = await page.EvalAwaitAsync("new Promise(r => setTimeout(() => r('late'), 200))", TimeSpan.FromSeconds(5));
        var cookies = page.View.TryGetCookieManager() is not null;
        var ok = token == "abc" && late == "late" && cookies;
        Log.Info($"Webcheck: {page.View.AdapterInfo}; script {token}; promise {late}; cookie {(cookies ? "có" : "không")}; {(ok ? "đạt" : "KHÔNG ĐẠT")}");
        hidden.Close();
        return ok;
    }
}
