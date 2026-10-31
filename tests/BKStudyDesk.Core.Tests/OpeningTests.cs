using SoHocTap.Data;
using SoHocTap.Ui;

namespace BKStudyDesk.Core.Tests;

/// <summary>Mốc mở của LMS (AppState.Opening): không phải hạn nộp, nên không hiện "quá hạn" khi đã qua giờ mở.</summary>
public class OpeningTests
{
    private const long Open = 1_793_120_400, Close = 1_793_725_200;

    private static LmsEvent Ev(string id, string kind, string? phase, long time, string name = "Khảo sát học phần") =>
        new(id, 1, "Giải tích 2", name, kind, time, "", null, phase);

    private static LmsData Data(List<LmsEvent> events, List<LmsQuiz>? quizzes = null) =>
        new(0, null, "261", [], events, quizzes ?? [], null, null, null);

    [Fact]
    public void Survey_open_event_is_an_opening_with_its_close_time()
    {
        var open = Ev("ev1", "event", "open", Open);
        var data = Data([open, Ev("ev2", "event", "close", Close)]);
        Assert.Equal((true, (long?)Close), AppState.Opening(open, data));
    }

    [Fact]
    public void Close_and_due_events_are_not_openings()
    {
        var data = Data([]);
        Assert.False(AppState.Opening(Ev("ev2", "event", "close", Close), data).Opens);
        Assert.False(AppState.Opening(Ev("ev3", "assign", "due", Close, "Bài 1"), data).Opens);
        Assert.False(AppState.Opening(Ev("ev4", "event", null, Close, "Buổi seminar"), data).Opens);
    }

    [Fact]
    public void Close_of_another_activity_is_not_matched()
    {
        var open = Ev("ev1", "event", "open", Open);
        var data = Data([open, Ev("ev2", "event", "close", Close, "Khảo sát khác")]);
        Assert.Equal((true, (long?)null), AppState.Opening(open, data));
    }

    [Fact]
    public void Old_quiz_data_without_phase_still_uses_quiz_open_time()
    {
        var open = Ev("ev1", "quiz", null, Open, "Quiz 1");
        var data = Data([open], [new LmsQuiz(9, 1, "Giải tích 2", "Quiz 1", Open, Close, [], "")]);
        Assert.Equal((true, (long?)Close), AppState.Opening(open, data));
    }
}
