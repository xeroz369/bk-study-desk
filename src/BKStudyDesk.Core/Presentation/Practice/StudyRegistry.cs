using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

/// <summary>
/// Sổ nội dung (port <c>registry.svelte.ts</c>, phần không đụng file, mạng): môn, chương, bài, đề, gói luyện tập.
/// Nạp file (bài <c>.md</c>, gói đã cài, quiz LMS đã lưu) là việc của đợt 3; ở đây chỉ ghép và tra cứu.
/// </summary>
/// <param name="sanitize">Lọc HTML của gói trước khi hiển thị (gói có thể đến từ người khác). Mặc định giữ nguyên; app truyền bộ lọc thật.</param>
/// <param name="clock">Đồng hồ (id đề ngẫu nhiên); test truyền đồng hồ cố định.</param>
public sealed class StudyRegistry(Func<string, string>? sanitize = null, TimeProvider? clock = null)
{
    private readonly Func<string, string> _sanitize = sanitize ?? (s => s);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    // Chữ của câu Đúng, Sai: là dữ liệu (nằm trong fingerprint), không đổi theo ngôn ngữ giao diện.
    private static readonly string[] TrueFalse = ["Đúng", "Sai"];

    public Manifest Manifest { get; set; } = new();
    public Dictionary<string, Lesson> Lessons { get; } = [];
    public Dictionary<string, Page> Pages { get; } = [];
    public Dictionary<string, Exam> Exams { get; } = [];

    /// <summary>Môn đang soạn mà chưa có bài: chỉ trang Soạn dùng, không hiện môn rỗng ở Luyện tập.</summary>
    public List<Course> Drafts { get; } = [];

    public void AddPage(Page p) => Pages[p.Id] = p;

    public void AddLesson(Lesson lesson)
    {
        var qs = lesson.Questions ?? [];
        for (var i = 0; i < qs.Count; i++)
        {
            var q = qs[i];
            if (string.IsNullOrEmpty(q.Id)) q.Id = $"{lesson.Id}.q{i + 1}";
            q.LessonId = lesson.Id;
            if (string.IsNullOrEmpty(q.Fp)) q.Fp = Fingerprint.Of(q);
        }
        Lessons[lesson.Id] = lesson;
    }

    /// <summary>Đề thi: QuestionIds tham chiếu câu trong bài học, Questions là câu riêng của đề.</summary>
    public void AddExam(Exam exam)
    {
        var qs = exam.Questions ?? [];
        for (var i = 0; i < qs.Count; i++)
        {
            var q = qs[i];
            if (string.IsNullOrEmpty(q.Id)) q.Id = $"{exam.Id}.q{i + 1}";
            q.ExamId = exam.Id;
            if (string.IsNullOrEmpty(q.Fp)) q.Fp = Fingerprint.Of(q);
        }
        Exams[exam.Id] = exam;
    }

    private static bool SameCode(Course c, string code) => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase);

    /// <summary>Môn theo mã; chưa có thì tạo môn nháp (id mon-mã, trùng cách AddPack đặt id).</summary>
    public Course EnsureCourse(string code, string name)
    {
        var have = Manifest.Courses.FirstOrDefault(c => SameCode(c, code)) ?? Drafts.FirstOrDefault(c => SameCode(c, code));
        if (have != null) return have;
        var draft = new Course { Id = "mon-" + code.ToLowerInvariant(), Code = code.ToUpperInvariant(), Name = name };
        Drafts.Add(draft);
        return draft;
    }

    public Course? Course(string id) => Manifest.Courses.FirstOrDefault(c => c.Id == id) ?? Drafts.FirstOrDefault(c => c.Id == id);

    /// <summary>Danh sách phẳng các bài theo thứ tự manifest.</summary>
    public List<Entry> Entries(string? courseId = null) =>
        Manifest.Courses.Where(c => courseId is null || c.Id == courseId)
            .SelectMany(c => c.Units.SelectMany(u => u.Lessons.Select(e => new Entry(e.Id, e.Title, e.File, e.Sources, c.Id, c.Name, u.Title))))
            .ToList();

    public Entry? Entry(string id) => Entries().FirstOrDefault(e => e.Id == id);

    public Question? Question(string id) =>
        Lessons.Values.SelectMany(l => l.Questions ?? []).FirstOrDefault(q => q.Id == id)
        ?? Exams.Values.SelectMany(x => x.Questions ?? []).FirstOrDefault(q => q.Id == id);

    public List<Question> LessonQuestions() => Lessons.Values.SelectMany(l => l.Questions ?? []).ToList();

    public List<Question> ExamQuestions(Exam exam) =>
        [.. (exam.QuestionIds ?? []).Select(Question).OfType<Question>(), .. exam.Questions ?? []];

    /// <summary>Tổng số câu trong các bài của môn (không tính câu riêng của đề).</summary>
    public int CourseQuestionCount(Course c) =>
        c.Units.Sum(u => u.Lessons.Sum(e => Lessons.TryGetValue(e.Id, out var l) ? l.Questions?.Count ?? 0 : 0));

    /// <summary>Câu trong bài của môn theo [chương][bài] (đầu vào của đề ngẫu nhiên và luyện trộn).</summary>
    public List<List<List<Question>>> UnitQuestions(Course c) =>
        c.Units.Select(u => u.Lessons.Select(e => Lessons.TryGetValue(e.Id, out var l) ? l.Questions ?? [] : []).ToList()).ToList();

    /// <summary>Một luật duy nhất cho thẻ tổng quan, menu môn và nút ở trang môn.</summary>
    public bool CanRandomExam(Course c) => ExamPick.EnoughForRandomExam(CourseQuestionCount(c), c.Blueprint != null);

    /// <summary>
    /// Đề ngẫu nhiên theo kiểu đề thật (course.blueprint: số câu mỗi chương, số phút, cách tính điểm). Câu rút theo
    /// <see cref="ExamPick.PickRandomExam"/>: nhóm đi cùng nhau, đề giữ thứ tự gốc. Ít câu hơn đề thật thì vẫn thang 10,
    /// giữ tỉ lệ điểm đúng, sai và co thời gian theo số câu.
    /// </summary>
    public Exam? RandomExam(string courseId, Func<Question, double>? rank = null, Func<double>? random = null)
    {
        if (Course(courseId) is not { } c) return null;
        var bp = c.Blueprint ?? new Blueprint(50);
        var baseScoring = bp.Scoring ?? c.Scoring ?? new Scoring(20, 0.5, -0.1);
        var pick = ExamPick.PickRandomExam(UnitQuestions(c), c.Blueprint, (int)baseScoring.Count, rank, random);
        if (pick.Questions.Count == 0) return null;
        var n = pick.Questions.Count;
        var right = 10.0 / n;
        var exam = new Exam
        {
            Id = $"ngau-nhien-{c.Id}-{_clock.GetUtcNow().ToUnixTimeMilliseconds()}",
            CourseId = c.Id,
            Title = L.F("practice.randomExamTitle", c.Name),
            // Math.round của JS: làm tròn nửa lên.
            Minutes = Math.Max(5, Math.Floor(bp.Minutes * n / Math.Max(pick.Total, 1) + 0.5)),
            Scoring = new Scoring(n, right, baseScoring.Wrong / baseScoring.Right * right),
            Shuffle = new ShuffleSetting(false, true),
            QuestionIds = pick.Questions.Select(q => q.Id).ToList(),
        };
        Exams[exam.Id] = exam;
        return exam;
    }

    /// <summary>
    /// Ghép một gói vào sổ: môn cùng mã thì thêm chương vào môn đó, chưa có thì tạo môn mới. Chương, bài định vị bằng tên
    /// (đã chuẩn hóa): trùng tên thì thêm kiến thức và câu vào chỗ đó, câu trùng fingerprint chỉ giữ một. Id bài, câu, đề
    /// gắn tiền tố id gói để không đụng nội dung khác và kết quả cũ.
    /// </summary>
    public void AddPack(StudyPack p)
    {
        var authors = string.Join(", ", p.Authors.Select(a => a.Name));
        var code = p.Course.Code.Trim().ToUpperInvariant();
        var course = Manifest.Courses.FirstOrDefault(c => SameCode(c, code));
        if (course is null)
        {
            course = new Course { Id = "mon-" + code.ToLowerInvariant(), Code = code, Name = p.Course.Name };
            Manifest.Courses.Add(course);
        }
        Question MapQ(PackQuestion q, string owner, int i) => PackQuestion(q, $"{owner}.{q.Id ?? "q" + (i + 1)}", p.Id);
        var shuffle = p.Settings is { } s ? new ShuffleSetting(s.ShuffleQuestions, s.ShuffleOptions) : null;
        foreach (var u in p.Units)
        {
            var unit = course.Units.FirstOrDefault(x => Text.NormText(x.Title) == Text.NormText(u.Title));
            if (unit is null)
            {
                unit = new Unit { Title = u.Title, Pack = new PackInfo(p.Id, p.Title, authors) };
                course.Units.Add(unit);
            }
            foreach (var l in u.Lessons)
            {
                var key = $"{p.Id}.{l.Id}";
                var sources = (l.Sources ?? []).Select(x => new SourceRef(x.Title, x.Pages)).ToList();
                var sections = (l.Sections ?? []).Select(x => new Section(x.Title is { } t ? _sanitize(t) : null, _sanitize(x.Body))).ToList();
                var questions = (l.Questions ?? []).Select((q, i) => MapQ(q, key, i)).ToList();
                var host = unit.Lessons.FirstOrDefault(e => Text.NormText(e.Title) == Text.NormText(l.Title));
                if (host != null)
                {
                    if (!Lessons.TryGetValue(host.Id, out var target))
                        Lessons[host.Id] = target = new Lesson { Id = host.Id, Title = host.Title, Sources = host.Sources, Sections = [], Questions = [] };
                    target.Sections = [.. target.Sections ?? [], .. sections];
                    var have = (target.Questions ?? []).Select(q => q.Fp).ToHashSet();
                    var fresh = questions.Where(q =>
                    {
                        q.Fp = Fingerprint.Of(q);
                        q.LessonId = host.Id;
                        return have.Add(q.Fp);
                    }).ToList();
                    target.Questions = [.. target.Questions ?? [], .. fresh];
                }
                else
                {
                    AddLesson(new Lesson { Id = key, Title = l.Title, Sources = sources, Sections = sections, Questions = questions, Shuffle = shuffle });
                    unit.Lessons.Add(new LessonEntry(key, l.Title, null, sources));
                }
            }
        }
        foreach (var x in p.Exams ?? [])
        {
            var id = $"{p.Id}.{x.Id}";
            AddExam(new Exam
            {
                Id = id,
                CourseId = course.Id,
                Title = x.Title,
                Minutes = x.Minutes,
                Scoring = x.Scoring,
                Shuffle = shuffle,
                QuestionIds = (x.QuestionRefs ?? []).Select(r => $"{p.Id}.{r}").ToList(),
                Questions = (x.Questions ?? []).Select((q, i) => MapQ(q, id, i)).ToList(),
            });
        }
    }

    /// <summary>
    /// Câu trong gói thành câu của sổ (đã lọc HTML). Đúng, Sai thành trắc nghiệm hai phương án để dùng chung giao diện.
    /// Dùng chung cho nạp gói và fingerprint lúc cập nhật gói, để hai bên luôn khớp.
    /// </summary>
    public Question PackQuestion(PackQuestion q, string id, string packId) => FromPack(q, id, packId, _sanitize);

    /// <summary>Như <see cref="PackQuestion"/> nhưng chọn được bộ lọc (nội dung có sẵn tính fingerprint trên HTML chưa lọc, như bản TS).</summary>
    public static Question FromPack(PackQuestion q, string id, string? packId, Func<string, string> sanitize)
    {
        var item = new Question
        {
            Id = id, Tag = q.Tag, Prompt = sanitize(q.Prompt), Solution = sanitize(q.Solution), PackId = packId, KeepOrder = q.KeepOrder, Group = q.Group,
        };
        switch (q.Type ?? "single")
        {
            case "truefalse":
                item.Options = [.. TrueFalse];
                item.Answer = q.AnswerBool == true ? 0 : 1;
                break;
            case "multi":
                item.Type = "multi";
                item.Options = (q.Options ?? []).Select(sanitize).ToList();
                item.Answer = -1;
                item.Answers = q.Answers ?? [];
                break;
            case "numeric":
                item.Type = "numeric";
                item.Answer = -1;
                item.Value = q.AnswerNumber;
                item.Tolerance = q.Tolerance;
                item.Unit = q.Unit;
                break;
            case "short":
                item.Type = "short";
                item.Answer = -1;
                item.Accept = q.Accept ?? [];
                break;
            default:
                item.Options = (q.Options ?? []).Select(sanitize).ToList();
                item.Answer = q.AnswerNumber is { } n ? (int)n : -1;
                break;
        }
        return item;
    }
}
