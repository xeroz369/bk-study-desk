namespace SoHocTap.Presentation.Practice;

// Nội dung học và đề thi (port từ khung Svelte của bản 1.x, src/ui/src/lib/study/types.ts). Chữ là HTML đơn giản có TeX \( \) \[ \].

public sealed record SourceRef(string File, string? Pages = null);

/// <summary>Bật, tắt xáo câu, xáo phương án mặc định (theo cài đặt của gói; người học vẫn đổi được).</summary>
public sealed record ShuffleSetting(bool? Questions = null, bool? Options = null);

/// <summary>Một câu hỏi. Loại câu giống quiz Moodle: single (mặc định), multi, numeric, short; Đúng, Sai đổi thành single hai phương án lúc nạp.</summary>
public sealed class Question
{
    public string Id { get; set; } = "";
    public string? Type { get; set; }
    public string? Tag { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = [];
    /// <summary>single: chỉ số đúng; loại khác là -1.</summary>
    public int Answer { get; set; }
    /// <summary>multi: các chỉ số đúng.</summary>
    public int[]? Answers { get; set; }
    /// <summary>numeric: đáp số, sai số tuyệt đối cho phép, đơn vị.</summary>
    public double? Value { get; set; }
    public double? Tolerance { get; set; }
    public string? Unit { get; set; }
    /// <summary>short: các đáp án chữ được chấp nhận.</summary>
    public string[]? Accept { get; set; }
    public string Solution { get; set; } = "";
    public string? LessonId { get; set; }
    public string? ExamId { get; set; }
    /// <summary>Không xáo phương án (có phương án kiểu "Cả A và B").</summary>
    public bool? KeepOrder { get; set; }
    /// <summary>Nhóm câu dùng chung đề, số liệu: luôn rút cùng nhau và đứng liền nhau.</summary>
    public string? Group { get; set; }
    /// <summary>Câu đến từ gói nào (hiện nguồn, xóa câu tự soạn).</summary>
    public string? PackId { get; set; }
    /// <summary>Fingerprint: cùng câu thì cùng giá trị, dù đến từ đâu (khóa của lịch ôn, ghi chú).</summary>
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
    /// <summary>Chương đến từ gói luyện tập, không phải nội dung có sẵn.</summary>
    public PackInfo? Pack { get; set; }
}

/// <summary>Kiểu đề thật của môn cho đề ngẫu nhiên: số câu mỗi chương (theo thứ tự), số phút, cách tính điểm.</summary>
public sealed record Blueprint(double Minutes, int[]? Units = null, int? Count = null, Scoring? Scoring = null);

public sealed record ExamDate(string Date, string Time);

public sealed class Course
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Mã môn (MT1009...): gói cùng mã ghép vào môn này.</summary>
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

/// <summary>Một bài trong danh sách phẳng của sổ (kèm môn, chương).</summary>
public sealed record Entry(string Id, string Title, string? File, List<SourceRef>? Sources, string CourseId, string CourseName, string UnitTitle);
