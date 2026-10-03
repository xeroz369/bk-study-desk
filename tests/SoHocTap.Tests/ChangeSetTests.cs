using SoHocTap.Data;

namespace SoHocTap.Tests;

public class ChangeSetTests
{
    private static LmsEvent Ev(string id, long time, bool done = false) => new(id, 1, "Môn", "Bài " + id, "assign", time, "Hạn nộp", null, Done: done);

    private static LmsData Lms(List<LmsEvent>? events = null, List<LmsGradeBook>? grades = null) =>
        new(1, null, "HK261", [], events ?? [], [], null, null, grades);

    private static MybkClass Class(string code, int day, string start, string room, params int[] weeks) =>
        new(code, "Môn " + code, "L01", day, [.. weeks], start, "10:00", room, 1, 2, null);

    private static MybkExam Exam(string code, string type, string date, string time, string room) => new(code, "Môn " + code, type, date, time, 90, room, "CS2");

    private static MybkGrade Grade(string code, double? score, string? letter = null) => new("20261", code, "Môn " + code, 3, score, letter, null, 1, null, null, null, null);

    private static MybkData Mybk(List<MybkClass>? schedule = null, List<MybkExam>? exams = null, List<MybkGrade>? grades = null) =>
        new(1, new MybkStudent("", "", null), new MybkTerm("20261", ""), schedule ?? [], exams ?? [], [], grades ?? [], null, null, null, null, null, null, null);

    [Fact]
    public void FirstSync_ReportsNothing()
    {
        Assert.True(ChangeSet.Compare(null, Lms([Ev("a", 1)])).IsEmpty);
        Assert.True(ChangeSet.Compare((MybkData?)null, Mybk([Class("X", 2, "07:00", "H1")])).IsEmpty);
    }

    [Fact]
    public void Events_NewAndRemoved_ById_DoneNotNew()
    {
        var c = ChangeSet.Compare(Lms([Ev("a", 1), Ev("b", 2)]), Lms([Ev("b", 2), Ev("c", 3), Ev("d", 4, done: true)]));
        Assert.Equal(["c"], c.NewEvents.Select(e => e.Id));
        Assert.Equal(["a"], c.RemovedEvents.Select(e => e.Id));
        Assert.False(c.IsEmpty);
    }

    [Fact]
    public void Events_Unchanged_IsEmpty() => Assert.True(ChangeSet.Compare(Lms([Ev("a", 1)]), Lms([Ev("a", 1)])).IsEmpty);

    [Fact]
    public void LmsGrades_NewAndChanged()
    {
        LmsGradeBook Book(params LmsGradeItem[] items) => new(5, "Môn", null, [.. items]);
        LmsGradeItem Item(string name, double? g) => new(name, "quiz", g, null, 10, null, null);
        var before = Lms(grades: [Book(Item("Quiz 1", 9), Item("Quiz 2", null), Item("Quiz 3", 7))]);
        var after = Lms(grades: [Book(Item("Quiz 1", 9), Item("Quiz 2", 10), Item("Quiz 3", 8), Item("Quiz 4", 6), Item("Quiz 5", null))]);
        var c = ChangeSet.Compare(before, after);
        Assert.Equal(["Quiz 2", "Quiz 3", "Quiz 4"], c.LmsGrades.Select(g => g.Item.Name));
        Assert.Null(c.LmsGrades[0].Before);
        Assert.Equal(7, c.LmsGrades[1].Before);
    }

    [Fact]
    public void Schedule_RoomTimeDayWeeks()
    {
        var before = Mybk([Class("A", 2, "07:00", "H1-101", 40, 41), Class("B", 3, "09:00", "H2", 40), Class("C", 4, "13:00", "H3", 40)]);
        var after = Mybk([Class("A", 2, "07:00", "H1-202", 40, 41), Class("B", 5, "10:00", "H2", 40, 42), Class("C", 4, "13:00", "H3", 40)]);
        var c = ChangeSet.Compare(before, after);
        Assert.Equal(2, c.Schedule.Count);
        Assert.Equal(ClassFields.Room, c.Schedule.Single(s => s.Code == "A").Fields);
        Assert.Equal(ClassFields.Time | ClassFields.Day | ClassFields.Weeks, c.Schedule.Single(s => s.Code == "B").Fields);
    }

    [Fact]
    public void Schedule_TwoSessionsSameCourse_OnlyChangedOneReported()
    {
        // Lý thuyết thứ 2 và bài tập thứ 5 cùng mã, cùng nhóm: đổi phòng buổi thứ 5 không được kéo theo buổi thứ 2.
        var before = Mybk([Class("A", 2, "07:00", "H1", 40), Class("A", 5, "09:00", "H2", 40)]);
        var after = Mybk([Class("A", 2, "07:00", "H1", 40), Class("A", 5, "09:00", "H9", 40)]);
        var change = Assert.Single(ChangeSet.Compare(before, after).Schedule);
        Assert.Equal(ClassFields.Room, change.Fields);
        Assert.Equal(5, change.After!.Day);
    }

    [Fact]
    public void Schedule_AddedAndRemoved()
    {
        var c = ChangeSet.Compare(Mybk([Class("A", 2, "07:00", "H1", 40)]), Mybk([Class("B", 3, "07:00", "H1", 40)]));
        Assert.Equal(2, c.Schedule.Count);
        Assert.Contains(c.Schedule, s => s.Code == "B" && s.Before is null);
        Assert.Contains(c.Schedule, s => s.Code == "A" && s.After is null);
    }

    [Fact]
    public void Exams_DateTimeRoom_NewAndRemoved()
    {
        var before = Mybk(exams: [Exam("A", "GK", "2026-10-15", "09g00", "H6-101"), Exam("B", "GK", "2026-10-16", "13g00", "H6-201"), Exam("C", "GK", "2026-10-17", "07g00", "H1")]);
        var after = Mybk(exams: [Exam("A", "GK", "2026-10-15", "09g00", "H6-102 "), Exam("B", "GK", "2026-10-18", "07g00", "H6-201"), Exam("A", "CK", "2026-12-20", "07g00", "H1")]);
        var c = ChangeSet.Compare(before, after);
        Assert.Equal(ExamFields.Room, c.Exams.Single(e => e.Code == "A" && e.Type == "GK").Fields);
        Assert.Equal(ExamFields.Date | ExamFields.Time, c.Exams.Single(e => e.Code == "B").Fields);
        Assert.Null(c.Exams.Single(e => e.Code == "A" && e.Type == "CK").Before);
        Assert.Null(c.Exams.Single(e => e.Code == "C").After);
    }

    [Fact]
    public void Exams_WhitespaceOnly_NotAChange()
    {
        var c = ChangeSet.Compare(Mybk(exams: [Exam("A", "GK", "2026-10-15", "09g00", "H6")]), Mybk(exams: [Exam("A", "GK", "2026-10-15", " 09g00", "H6 ")]));
        Assert.Empty(c.Exams);
    }

    [Fact]
    public void MybkGrades_NewAndChanged()
    {
        var before = Mybk(grades: [Grade("A", null), Grade("B", 7, "B"), Grade("C", 8, "B+")]);
        var after = Mybk(grades: [Grade("A", 9, "A"), Grade("B", 7, "B"), Grade("C", 8.5, "A"), Grade("D", null, "MT"), Grade("E", null)]);
        var c = ChangeSet.Compare(before, after);
        Assert.Equal(["A", "C", "D"], c.MybkGrades.Select(g => g.Grade.Code));
        Assert.Null(c.MybkGrades[0].Before);
        Assert.Equal(8, c.MybkGrades[1].Before!.Score);
        Assert.Contains("điểm MyBK 3", c.Counts, StringComparison.Ordinal);
    }
}
