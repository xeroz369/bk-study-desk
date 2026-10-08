using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SoHocTap.Presentation.Practice.Pages;
using SoHocTap.Ui;

namespace SoHocTap.Presentation.Practice;

/// <summary>Chỗ đang thao tác: cả môn, một chương (Unit là chỉ số) hoặc một bài.</summary>
public sealed record Scope(Course Course, int? Unit = null, string? LessonId = null);

/// <summary>Kết quả đọc file nhập: gói đã đặt vào chỗ, báo cáo kiểm tra, hoặc lỗi; kèm ghi chú cho phần xem trước.</summary>
public sealed record Prepared(StudyPack? Pack, PackReport? Report, string? Error, List<string> Notes);

/// <summary>Quyền của một bài: tạo câu, nhập, xuất.</summary>
public sealed record LessonRights(bool Create, bool Import, bool Export);

/// <summary>
/// Tạo, nhập, xuất gói luyện tập theo đúng chỗ (môn, chương, bài); port <c>transfer.ts</c>.
/// Gói tự định vị bằng tên: môn theo course.code, chương theo title của unit, bài theo title của lesson (so sánh đã chuẩn hóa).
/// Nhập ở đâu thì đổi tên chương, bài trong gói cho khớp chỗ đó; xuất thì giữ nguyên tên để máy khác nhập vào đúng chỗ.
/// </summary>
public sealed partial class Transfer(StudyRegistry study, IReadOnlyList<InstalledPack> packs, IReadOnlyList<QuizInfo> quizzes, string appName,
    TimeProvider? clock = null)
{
    /// <summary>Tiền tố id gói do app tự tạo trên máy này (câu tự soạn, quiz LMS đã lưu): không nhận từ file bên ngoài.</summary>
    private static readonly string[] ReservedIds = ["tu-soan-", "quiz-lms-"];

    private const string License = "CC-BY-SA-4.0";
    private const string School = "HCMUT";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private static string Norm(string s) => Text.NormText(s);

    public static string Slug(string s, string fallback = "muc") => StudyMarkdown.Slug(s, fallback);

    private static string CodeOf(Course c) => c.Code ?? c.Id.ToUpperInvariant();

    // ------------------------------------------------------------------ chỗ

    public static Unit? UnitOf(Scope s) => s.Unit is { } u ? s.Course.Units[u] : null;

    public static LessonEntry? LessonOf(Scope s) => UnitOf(s)?.Lessons.FirstOrDefault(e => e.Id == s.LessonId);

    public static string ScopeLabel(Scope s) => LessonOf(s)?.Title ?? UnitOf(s)?.Title ?? s.Course.Name;

    // ------------------------------------------------------------------ quyền

    /// <summary>Quiz LMS chưa đóng: không xuất. Câu "Ghi lại" từ trí nhớ (gói quiz-lms-ghi-) cũng khóa theo quiz gốc.</summary>
    public bool QuizLocked(string lessonId)
    {
        var recall = LmsQuiz.IsRecallLesson(lessonId) ? study.Entries().FirstOrDefault(e => e.Id == lessonId)?.Title : null;
        return quizzes.Any(i => !i.Shareable && (i.LessonId == lessonId || recall == RecallTitle(i)));
    }

    // Tên bài ghi lại là dữ liệu trong gói (khớp theo tên), không dịch theo ngôn ngữ giao diện.
    public static string RecallTitle(QuizInfo i) => RecallTitle(i.Quiz.Quiz);

    /// <summary>Tên bài ghi lại câu còn nhớ của một quiz (khớp theo tên với quiz gốc).</summary>
    public static string RecallTitle(string quiz) => "Ghi lại: " + quiz;

    /// <summary>Quiz LMS đã lưu không nhận câu mới (trừ câu ghi lại từ trí nhớ), chỉ chia sẻ khi đã đóng.</summary>
    public LessonRights LessonCan(string lessonId)
    {
        var lms = LmsQuiz.IsLmsQuizLesson(lessonId);
        return new LessonRights(!lms || LmsQuiz.IsRecallLesson(lessonId), !lms, !QuizLocked(lessonId));
    }

    // ------------------------------------------------------------------ xuất

    /// <summary>Dòng "Lần làm trên LMS: bạn chọn..." là đáp án riêng của người gửi: không đưa vào file chia sẻ.</summary>
    [GeneratedRegex(@"<p>\s*<small>\s*Lần làm trên LMS:[\s\S]*?</p>")]
    private static partial Regex OwnAttempt();

    /// <summary>Câu trong sổ thành câu trong gói, giữ đúng loại.</summary>
    public static PackQuestion ToPackQuestion(Question q, int i)
    {
        var p = new PackQuestion { Id = $"q{i + 1}", Tag = q.Tag, Group = q.Group, Prompt = q.Prompt, Solution = OwnAttempt().Replace(q.Solution, "") };
        switch (q.Type ?? "single")
        {
            case "multi":
                p.Type = "multi";
                p.Options = [.. q.Options];
                p.Answers = [.. q.Answers ?? []];
                break;
            case "numeric":
                p.Type = "numeric";
                p.Answer = JsonValue.Create(q.Value ?? 0);
                p.Tolerance = q.Tolerance is { } t && t != 0 ? t : null;
                p.Unit = string.IsNullOrEmpty(q.Unit) ? null : q.Unit;
                break;
            case "short":
                p.Type = "short";
                p.Accept = [.. q.Accept ?? []];
                break;
            default:
                p.Options = [.. q.Options];
                p.Answer = JsonValue.Create(q.Answer);
                break;
        }
        return p;
    }

    /// <summary>Một câu dạng Study Markdown (báo lỗi câu, prompt cho AI).</summary>
    public static string QuestionMarkdown(Question q)
    {
        var p = new StudyPack
        {
            Id = "x1", Title = "x", Version = "1.0.0", Course = new PackCourse("X", "x"), Authors = [new PackAuthor("x")],
            Units = [new PackUnit { Title = "x", Lessons = [new PackLesson { Id = "x1", Title = "x", Questions = [ToPackQuestion(q, 0)] }] }],
        };
        var md = StudyMarkdown.Write(p).Md;
        var at = md.IndexOf("### ", StringComparison.Ordinal);
        return Text.TrimJs(at >= 0 ? md[at..] : md);
    }

    /// <summary>Dựng gói từ phần sổ trong scope. Tên chương, bài giữ nguyên để người nhận nhập vào đúng chỗ.</summary>
    public StudyPack BuildExport(Scope s, string author)
    {
        var c = s.Course;
        var code = CodeOf(c);
        var units = c.Units
            .Select((u, ui) => (u, ui))
            .Where(x => s.Unit is null || x.ui == s.Unit)
            .Select(x =>
            {
                var used = new HashSet<string>();
                var lessons = new List<PackLesson>();
                foreach (var e in x.u.Lessons.Where(e => (s.LessonId is null || e.Id == s.LessonId) && !QuizLocked(e.Id)))
                {
                    if (!study.Lessons.TryGetValue(e.Id, out var l)) continue;
                    var id = Slug(e.Title, "bai");
                    while (!used.Add(id)) id += "-2";
                    var sources = (l.Sources ?? e.Sources ?? []).Select(r => new PackSource(r.File, r.Pages is { Length: > 0 } ? r.Pages : null)).ToList();
                    lessons.Add(new PackLesson
                    {
                        Id = id,
                        Title = e.Title,
                        Sources = sources.Count > 0 ? sources : null,
                        Sections = l.Sections is { Count: > 0 } secs ? [.. secs.Select(t => new PackSection(string.IsNullOrEmpty(t.Title) ? null : t.Title, Text.TrimJs(t.Html)))] : null,
                        Questions = [.. (l.Questions ?? []).Select(ToPackQuestion)],
                    });
                }
                return new PackUnit { Title = x.u.Title, Lessons = lessons };
            })
            .Where(u => u.Lessons.Count > 0)
            .ToList();
        var exams = s.Unit is null
            ? study.Exams.Values
                .Where(x => x.CourseId == c.Id && !x.Id.StartsWith("ngau-nhien-", StringComparison.Ordinal))   // đề ngẫu nhiên chỉ có trong bộ nhớ
                .Select(x => new PackExam
                {
                    Id = Slug(x.Id, "de"),
                    Title = x.Title,
                    Minutes = x.Minutes,
                    Scoring = x.Scoring ?? c.Scoring,
                    Questions = [.. study.ExamQuestions(x).Where(q => !QuizLocked(q.LessonId ?? "")).Select(ToPackQuestion)],
                })
                .Where(x => x.Questions!.Count > 0)
                .ToList()
            : [];
        var label = ScopeLabel(s);
        var id = Slug($"hcmut-{code}-{(s.Unit is null ? "tron-mon" : label)}");
        return new StudyPack
        {
            Id = id.Length > 64 ? id[..64] : id,
            Title = s.Unit is null ? c.Name : $"{c.Name}: {label}",
            Version = "1.0.0",
            Language = "vi",
            Course = new PackCourse(code, c.Name, School),
            Authors = [new PackAuthor(author.Length > 0 ? author : L.T("transfer.noName"))],
            License = License,
            CreatedWith = L.F("transfer.createdExport", appName),
            Verified = new PackVerified(L.T("transfer.verifiedExport")),
            Units = units,
            Exams = exams.Count > 0 ? exams : null,
        };
    }

    /// <summary>Gói thành file Study Markdown: .md khi không có ảnh, .zip (goi.md và img/) khi có.</summary>
    public static (string FileName, byte[] Data) ExportMarkdown(StudyPack p)
    {
        var (md, images) = StudyMarkdown.Write(p);
        if (images.Count == 0) return ($"{p.Id}.md", Encoding.UTF8.GetBytes(md));
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("goi.md", CompressionLevel.Optimal).Open(), new UTF8Encoding(false))) w.Write(md);
            foreach (var (path, uri) in images)
            {
                using var s = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
                var bytes = Convert.FromBase64String(uri[(uri.IndexOf(',', StringComparison.Ordinal) + 1)..]);
                s.Write(bytes);
            }
        }
        return ($"{p.Id}.zip", ms.ToArray());
    }

    // ------------------------------------------------------------------ nhập

    /// <summary>Giới hạn file nhập: zip nhỏ không được bung ra hàng GB (zip bomb).</summary>
    public const long MaxFile = 25 * 1024 * 1024;
    private const long MaxUnzipped = 60 * 1024 * 1024;
    private const int MaxEntries = 500;

    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
    };

    [GeneratedRegex(@"\.(md|json|xml|txt|gift)$", RegexOptions.IgnoreCase)] private static partial Regex TextEntry();
    [GeneratedRegex(@"^[^/]+/(img/)")] private static partial Regex ImgFolder();

    /// <summary>
    /// Đọc file người dùng chọn: .zip (goi.md và img/) hoặc file chữ bất kỳ (.md, .json, .xml, .txt). Null khi quá giới hạn
    /// (file quá lớn, zip bung ra quá lớn hay quá nhiều file); zip hỏng ném InvalidDataException.
    /// </summary>
    public static (string Text, Dictionary<string, string> Images)? ReadFile(string name, byte[] data)
    {
        if (data.LongLength > MaxFile) return null;
        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return (Encoding.UTF8.GetString(data), []);
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        if (zip.Entries.Count > MaxEntries) return null;
        var images = new Dictionary<string, string>();
        var text = "";
        long total = 0;
        foreach (var e in zip.Entries)
        {
            total += e.Length;
            if (total > MaxUnzipped) return null;
            if (e.FullName.EndsWith('/')) continue;
            using var s = e.Open();
            using var ms = new MemoryStream();
            // Đọc có chặn: Length trong zip có thể khai sai.
            var buf = new byte[81920];
            int n;
            while ((n = s.Read(buf)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > e.Length || ms.Length > MaxUnzipped) return null;
            }
            if (TextEntry().IsMatch(e.FullName) && text.Length == 0) text = Encoding.UTF8.GetString(ms.ToArray());
            else if (Mime.TryGetValue(Path.GetExtension(e.FullName), out var mime))
                images[ImgFolder().Replace(e.FullName, "$1")] = $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
        }
        return (text, images);
    }

    [GeneratedRegex(@"^```[\w-]*[ \t]*\n")] private static partial Regex FenceOpen();
    [GeneratedRegex(@"\n```\s*$")] private static partial Regex FenceClose();
    [GeneratedRegex(@"^#{1,2} ", RegexOptions.Multiline)] private static partial Regex TopHeading();
    [GeneratedRegex(@"^---\n[\s\S]*?\n---\n")] private static partial Regex FrontMatter();
    [GeneratedRegex(@"^```(?:json)?\s*|\s*```$")] private static partial Regex JsonFence();

    /// <summary>
    /// Đọc gói và đặt vào scope: nhập ở bài thì mọi câu vào bài đó, ở chương thì mọi bài vào chương đó, ở môn thì giữ chương, bài
    /// của gói (trùng tên thì ghép, khác thì thêm mới). Gói môn khác thì đổi mã cho khớp môn đang nhập.
    /// </summary>
    public Prepared PrepareImport(string raw, Scope? s, string author = "", IReadOnlyDictionary<string, string>? images = null)
    {
        var r = PrepareRaw(raw, s, author, images ?? new Dictionary<string, string>());
        // Gói nhận từ người khác mang id dành riêng sẽ ghi đè dữ liệu của mình: đổi sang id riêng để nhập thành gói mới.
        if (r.Pack is { } p && ReservedIds.Any(x => p.Id.StartsWith(x, StringComparison.Ordinal)))
        {
            var stale = UpdateNotes(p).ToHashSet();
            var id = "nhan-" + p.Id;
            p.Id = id.Length > 64 ? id[..64] : id;
            var notes = r.Notes.Where(n => !stale.Contains(n)).Concat(UpdateNotes(p)).ToList();
            return r with { Notes = notes, Report = Validate(p, raw.Length) };
        }
        return r;
    }

    private static PackReport Validate(StudyPack p, int size) => PackValidator.Validate(JsonNode.Parse(p.ToJson()), size);

    private static StudyPack Clone(StudyPack p) => StudyPack.Parse(p.ToJson())!;

    private Prepared PrepareRaw(string raw, Scope? s, string author, IReadOnlyDictionary<string, string> images)
    {
        var notes = new List<string>();
        raw = FenceClose().Replace(FenceOpen().Replace(Text.TrimJs(raw.Replace("\r", "")), ""), "");   // AI hay bọc câu trả lời trong khối mã
        StudyPack data;
        var fmt = QuizFormats.Detect(raw);
        if (fmt == QuizFormat.Markdown)
        {
            // Chỉ có câu (###) dán khi đang đứng ở chương hay bài: thêm "# chương / ## bài" của chỗ đó.
            var shift = 0;
            var unit = s is null ? null : UnitOf(s);
            if (s != null && unit != null && !TopHeading().IsMatch(raw))
            {
                var head = $"# {unit.Title}\n\n## {LessonOf(s)?.Title ?? L.T("transfer.extraLesson")}\n\n";
                var fm = FrontMatter().Match(raw) is { Success: true } m ? m.Value : "";
                raw = fm + head + raw[fm.Length..];
                shift = head.Split('\n').Length - 1;
            }
            var md = StudyMarkdown.Parse(raw, images);
            if (md.Errors.Count > 0)
            {
                var list = md.Errors.Take(8).Select(e => (e.Line > 0 ? L.F("transfer.line", Math.Max(1, e.Line - shift)) : "") + e.Message).ToList();
                if (unit is null && !TopHeading().IsMatch(raw)) list.Add(L.T("transfer.tipPickPlace"));
                return new Prepared(null, null, L.F("transfer.mdErrors", md.Errors.Count), list);
            }
            notes.AddRange(md.Warnings.Take(6).Select(w => w.Line > 0 ? L.F("transfer.warnLine", w.Line, w.Message) : L.F("transfer.warn", w.Message)));
            var p = md.Pack;
            if (p.Course.Code.Length == 0 && s != null) p.Course = new PackCourse(CodeOf(s.Course), s.Course.Name, School);
            if (p.Authors.Count == 0) p.Authors = [new PackAuthor(author.Length > 0 ? author : L.T("transfer.noName"))];
            if (p.Course.Code.Length == 0) return new Prepared(null, null, L.T("transfer.noCourse"), notes);
            data = p;
        }
        else if (fmt is { } f && f != QuizFormat.StudyPack)
        {
            // Aiken, GIFT, Moodle XML: đổi sang gói rồi xử lý như gói thường (đặt vào đúng chỗ bên dưới).
            if (s is null) return new Prepared(null, null, L.T("transfer.pickCourseFirst"), notes);
            var label = f switch { QuizFormat.Aiken => "Aiken", QuizFormat.Gift => "GIFT", _ => "Moodle XML" };
            var conv = f switch { QuizFormat.Aiken => QuizFormats.ParseAiken(raw), QuizFormat.Gift => QuizFormats.ParseGift(raw), _ => QuizFormats.ParseMoodleXml(raw) };
            notes.Add(L.F("transfer.readFormat", label, conv.Lessons.Sum(l => l.Questions?.Count ?? 0)));
            notes.AddRange(conv.Notes);
            var code = CodeOf(s.Course);
            var stamp = Base36(_clock.GetUtcNow().ToUnixTimeMilliseconds());
            data = new StudyPack
            {
                Id = Slug($"nhap-{code}-{label}-{stamp}"),
                Title = L.F("transfer.importedTitle", s.Course.Name, label),
                Version = "1.0.0",
                Language = "vi",
                Course = new PackCourse(code, s.Course.Name, School),
                Authors = [new PackAuthor(author.Length > 0 ? author : L.T("transfer.fromFile"))],
                License = License,
                CreatedWith = L.F("transfer.createdImport", label),
                Verified = new PackVerified(L.F("transfer.verifiedImport", label)),
                Units = [new PackUnit { Title = UnitOf(s)?.Title ?? L.F("transfer.importedUnit", label), Lessons = [.. conv.Lessons.Where(l => l.Questions is { Count: > 0 })] }],
            };
            if (data.Units[0].Lessons.Count == 0) return new Prepared(null, null, L.F("transfer.nothingRead", label), notes);
        }
        else
        {
            JsonNode? node;
            try { node = JsonNode.Parse(JsonFence().Replace(Text.TrimJs(raw), "")); }
            catch (JsonException e) { return new Prepared(null, null, L.F("transfer.unknownFormat", e.Message), notes); }
            var report = PackValidator.Validate(node, raw.Length);
            var pack = report.Ok ? StudyPack.Parse(node!.ToJsonString()) : null;
            if (!report.Ok || pack is null || s is null) return new Prepared(pack, report, null, notes);
            data = pack;
        }
        if (data.Format.Length == 0) data.Format = StudyPack.FormatId;
        var first = Validate(data, raw.Length);
        if (!first.Ok || s is null) return new Prepared(first.Ok ? data : null, first, null, notes);
        return Place(Clone(data), s, raw.Length, notes);
    }

    private Prepared Place(StudyPack p, Scope s, int size, List<string> notes)
    {
        var code = CodeOf(s.Course);
        if (Norm(p.Course.Code) != Norm(code))
        {
            notes.Add(L.F("transfer.otherCourse", p.Course.Code, p.Course.Name, s.Course.Name));
            p.Course = p.Course with { Code = code, Name = s.Course.Name };
        }
        var unit = UnitOf(s);
        var lesson = LessonOf(s);
        if (unit != null && lesson != null)
        {
            // Một bài: gộp kiến thức và câu của mọi bài trong gói, đánh lại id câu.
            var all = p.Units.SelectMany(u => u.Lessons).ToList();
            var questions = all.SelectMany(l => l.Questions ?? []).Select((q, i) => { q.Id = $"q{i + 1}"; return q; }).ToList();
            var sections = all.SelectMany(l => l.Sections ?? []).ToList();
            p.Units = [new PackUnit { Title = unit.Title, Lessons = [new PackLesson { Id = Slug(lesson.Title, "bai"), Title = lesson.Title, Sections = sections.Count > 0 ? sections : null, Questions = questions }] }];
            if (p.Exams is { Count: > 0 }) notes.Add(L.T("transfer.examsSkipped"));
            p.Exams = null;
            notes.Add(L.F("transfer.intoLesson", questions.Count, lesson.Title));
        }
        else if (unit != null)
        {
            var used = new HashSet<string>();
            var flat = p.Units.SelectMany(u => u.Lessons).ToList();
            var renamed = flat.Select(l =>
            {
                var id = l.Id;
                while (!used.Add(id)) id += "-2";
                return id;
            }).ToList();
            // Đề trỏ tới "<id bài>.<id câu>"; id bài có thể lặp giữa các chương nên chỉ giữ chỗ khớp đầu tiên.
            var oldIds = new Dictionary<string, string>();
            for (var i = 0; i < flat.Count; i++) oldIds.TryAdd(flat[i].Id, renamed[i]);
            for (var i = 0; i < flat.Count; i++) flat[i].Id = renamed[i];
            p.Units = [new PackUnit { Title = unit.Title, Lessons = flat }];
            foreach (var x in p.Exams ?? [])
                x.QuestionRefs = x.QuestionRefs?.Select(r =>
                {
                    var dot = r.IndexOf('.', StringComparison.Ordinal);
                    var (l, q) = dot < 0 ? (r, "") : (r[..dot], r[(dot + 1)..].Split('.')[0]);
                    return $"{oldIds.GetValueOrDefault(l, l)}.{q}";
                }).ToList();
            notes.Add(L.F("transfer.intoUnit", flat.Count, unit.Title));
        }
        else
        {
            var hit = p.Units.Count(u => s.Course.Units.Any(x => Norm(x.Title) == Norm(u.Title)));
            notes.Add(L.F("transfer.intoCourse", p.Units.Count, hit, p.Units.Count - hit));
        }
        notes.AddRange(UpdateNotes(p));
        return new Prepared(p, Validate(p, size), null, notes);
    }

    private static string Base36(long n)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        do { sb.Insert(0, digits[(int)(n % 36)]); n /= 36; } while (n > 0);
        return sb.ToString();
    }

    // ------------------------------------------------------------------ cập nhật gói, câu trùng

    /// <summary>Fingerprint của câu trong gói đúng như sổ tính sau khi nạp (cùng cách đổi và cùng bộ lọc).</summary>
    private string PackFp(PackQuestion q) => Fingerprint.Of(study.PackQuestion(q, "", ""));

    /// <summary>fingerprint thành id câu sổ sẽ đặt (AddPack: "gói.bài.câu").</summary>
    public Dictionary<string, string> NewIds(StudyPack p)
    {
        var output = new Dictionary<string, string>();
        foreach (var u in p.Units)
            foreach (var l in u.Lessons)
                for (var i = 0; i < (l.Questions?.Count ?? 0); i++) output[PackFp(l.Questions![i])] = $"{p.Id}.{l.Id}.{l.Questions[i].Id ?? "q" + (i + 1)}";
        foreach (var x in p.Exams ?? [])
            for (var i = 0; i < (x.Questions?.Count ?? 0); i++) output[PackFp(x.Questions![i])] = $"{p.Id}.{x.Id}.{x.Questions[i].Id ?? "q" + (i + 1)}";
        return output;
    }

    private IEnumerable<Question> AllQuestions() => study.LessonQuestions().Concat(study.Exams.Values.SelectMany(x => x.Questions ?? []));

    /// <summary>Ghi chú xem trước: cập nhật gói đã cài, câu đã có trong sổ.</summary>
    private List<string> UpdateNotes(StudyPack p)
    {
        var notes = new List<string>();
        if (packs.FirstOrDefault(x => x.Pack?.Id == p.Id)?.Pack is { } old)
            notes.Add(old.Version == p.Version ? L.F("transfer.reinstall", p.Version) : L.F("transfer.update", old.Version, p.Version));
        var have = AllQuestions().Where(q => q.PackId != p.Id).Select(q => q.Fp).ToHashSet();
        var dup = NewIds(p).Keys.Count(have.Contains);
        if (dup > 0) notes.Add(L.F("transfer.duplicates", dup));
        return notes;
    }

    /// <summary>
    /// Trước khi cài bản mới của một gói: id câu có thể đổi (đánh lại số, dời chỗ), nên chuyển kết quả theo fingerprint.
    /// Moves: id cũ thành id mới; Gone: id cũ bản mới không còn (bỏ kết quả). Đưa vào <see cref="PracticeProgress.MigrateIds"/>.
    /// </summary>
    public (Dictionary<string, string> Moves, List<string> Gone) Migration(StudyPack p)
    {
        var ids = NewIds(p);
        var old = AllQuestions().Where(q => q.PackId == p.Id).ToList();
        var moves = new Dictionary<string, string>();
        foreach (var q in old)
            if (q.Fp is { } fp && ids.TryGetValue(fp, out var to)) moves[q.Id] = to;
        var still = ids.Values.ToHashSet();
        return (moves, [.. old.Select(q => q.Id).Where(id => !still.Contains(id))]);
    }

    // ------------------------------------------------------------------ tự soạn

    /// <summary>Gói chứa câu, bài tự soạn của môn: tu-soan-mã môn (recall: quiz-lms-ghi-mã môn), lưu trong content/packs như gói khác.</summary>
    public StudyPack AuthoredPack(Course c, string author, bool recall = false)
    {
        var code = CodeOf(c);
        var id = Slug($"{(recall ? LmsQuiz.RecallPrefix : "tu-soan-")}{code}");
        var p = packs.FirstOrDefault(x => x.Pack?.Id == id)?.Pack is { } have
            ? Clone(have)
            : new StudyPack
            {
                Id = id,
                Title = L.F("transfer.authoredTitle", c.Name),
                Version = "1.0.0",
                Language = "vi",
                Course = new PackCourse(code, c.Name, School),
                Authors = [new PackAuthor(author.Length > 0 ? author : L.T("transfer.noName"))],
                License = License,
                CreatedWith = L.F("transfer.createdAuthored", appName),
                Verified = new PackVerified(L.T("transfer.verifiedAuthored")),
            };
        if (author.Length > 0 && !p.Authors.Any(a => a.Name == author)) p.Authors.Add(new PackAuthor(author));
        return p;
    }

    /// <summary>Bài (theo tên) trong chương (theo tên) của gói tự soạn; chưa có thì tạo.</summary>
    public static PackLesson LessonIn(StudyPack p, string unitTitle, string lessonTitle)
    {
        var u = p.Units.FirstOrDefault(x => Norm(x.Title) == Norm(unitTitle));
        if (u is null) p.Units.Add(u = new PackUnit { Title = unitTitle });
        var l = u.Lessons.FirstOrDefault(x => Norm(x.Title) == Norm(lessonTitle));
        if (l is null)
        {
            var ids = p.Units.SelectMany(x => x.Lessons.Select(y => y.Id)).ToHashSet();
            var id = Slug(lessonTitle, "bai");
            while (ids.Contains(id)) id += "-2";
            u.Lessons.Add(l = new PackLesson { Id = id, Title = lessonTitle, Questions = [] });
        }
        return l;
    }

    public static void BumpVersion(StudyPack p)
    {
        var v = p.Version.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).Concat([0, 0, 0]).Take(3).ToArray();
        p.Version = $"{v[0]}.{v[1]}.{v[2] + 1}";
    }
}
