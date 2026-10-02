using SoHocTap.Core;

namespace SoHocTap.Tests;

public class DataImportTests
{
    [Fact]
    public void Plan_CopiesDataFilesAndSecrets()
    {
        var plan = DataImport.Plan(["config.json", "ket-qua.json", @"secrets\lms.bin", @"packs\a.studypack.json", "window.json"]);
        Assert.Equal(["config.json", "ket-qua.json", @"secrets\lms.bin", @"packs\a.studypack.json", "window.json"], plan);
    }

    [Fact]
    public void Plan_SkipsBrowserProfileLogsAndTemp()
    {
        var plan = DataImport.Plan([@"webview\EBWebView\Default\Cookies", "app.log", @"tmp\unz-1\x.pdf", "update-state.json", "mybk-requests.log", "lms.json"]);
        Assert.Equal(["lms.json"], plan);
    }

    [Fact]
    public void Plan_EmptyInput_Empty() => Assert.Empty(DataImport.Plan([]));
}