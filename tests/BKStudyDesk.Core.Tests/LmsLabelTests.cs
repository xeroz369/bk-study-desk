using SoHocTap.Data;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Nhãn mốc LMS: nhãn hạn nộp do app ghi vào dữ liệu thì dịch theo ngôn ngữ; nhãn của LMS giữ nguyên.</summary>
public class LmsLabelTests
{
    private static LmsEvent E(string kind, string label) => new("e1", 1, "Giải tích 2", "Bài 1", kind, 0, label, null);

    [Fact]
    public void DueLabel_ComesFromLang()
    {
        var shown = AppState.LmsLabel(E("assign", LmsSource.DueLabel));
        Assert.Equal(L.T("timeline.dueLabel"), shown);
        Assert.NotEqual("timeline.dueLabel", shown);   // key có trong lang, không hiện tên key
    }

    [Fact]
    public void LmsProvidedLabels_Unchanged()
    {
        Assert.Equal("Quiz 3 closes", AppState.LmsLabel(E("quiz", "Quiz 3 closes")));
        Assert.Equal("Bài tập lớn", AppState.LmsLabel(E("assign", "Bài tập lớn")));
    }
}
