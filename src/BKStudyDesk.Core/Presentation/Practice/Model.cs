namespace SoHocTap.Presentation.Practice;

public sealed record SourceRef(string File, string? Pages = null);

public sealed record ShuffleSetting(bool? Questions = null, bool? Options = null);

public sealed class Question
{
    public string Id { get; set; } = "";
    public string? Type { get; set; }
    public string? Tag { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = [];
    public int Answer { get; set; }
    public int[]? Answers { get; set; }
    public double? Value { get; set; }
    public double? Tolerance { get; set; }
    public string? Unit { get; set; }
    public string[]? Accept { get; set; }
    public string Solution { get; set; } = "";
    public string? LessonId { get; set; }
    public string? ExamId { get; set; }
    public bool? KeepOrder { get; set; }
    public string? Group { get; set; }
    public string? PackId { get; set; }
    public string? Fp { get; set; }
}

public sealed record Section(string? Title, string Html);

public sealed class Lesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<SourceRef>? Sources { get; set; }
    public List<Section>? Sections { get; set; }
    public List<Question>? Questions { get; set; }
    public ShuffleSetting? Shuffle { get; set; }
}

public sealed record Scoring(double Count, double Right, double Wrong);

public sealed class Exam
{
    public string Id { get; set; } = "";
    public string CourseId { get; set; } = "";
    public string Title { get; set; } = "";
    public double Minutes { get; set; }
    public List<SourceRef>? Sources { get; set; }
    public List<string>? QuestionIds { get; set; }
    public List<Question>? Questions { get; set; }
    public Scoring? Scoring { get; set; }
    public ShuffleSetting? Shuffle { get; set; }
}

public sealed record Page(string Id, string Html);

public sealed record LessonEntry(string Id, string Title, string? File = null, List<SourceRef>? Sources = null);

public sealed record PackInfo(string Id, string Title, string Authors);

public sealed class Unit
{
    public string Title { get; set; } = "";
    public List<LessonEntry> Lessons { get; set; } = [];
    public PackInfo? Pack { get; set; }
}

public sealed record Blueprint(double Minutes, int[]? Units = null, int? Count = null, Scoring? Scoring = null);

public sealed record ExamDate(string Date, string Time);

public sealed class Course
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public ExamDate? Exam { get; set; }
    public Scoring? Scoring { get; set; }
    public Blueprint? Blueprint { get; set; }
    public List<Unit> Units { get; set; } = [];
}

public sealed record PageEntry(string Id, string Title, string File);

public sealed class Manifest
{
    public string Term { get; set; } = "";
    public List<string>? Priority { get; set; }
    public List<PageEntry>? Pages { get; set; }
    public List<string>? ExamFiles { get; set; }
    public List<Course> Courses { get; set; } = [];
}

public sealed record Entry(string Id, string Title, string? File, List<SourceRef>? Sources, string CourseId, string CourseName, string UnitTitle);
