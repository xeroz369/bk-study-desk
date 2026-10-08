using System.IO.Compression;
using SoHocTap.Presentation.Practice;
using SoHocTap.Presentation.Practice.Pages;

namespace BKStudyDesk.Core.Tests.Practice;

/// <summary>
/// Port của transfer.ts. Không so bằng cách chạy bản TS (transfer.ts đọc sổ qua registry.svelte.ts dùng rune của Svelte, không chạy
/// được bằng Node thuần): test viết theo hành vi của bản TS, từng nhánh một.
/// </summary>
public class TransferTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 3, 0, 0, TimeSpan.Zero);

    private static StudyRegistry Make()
    {
        var study = new StudyRegistry(s => Html.Sanitize(s));
        study.Manifest.Courses.Add(new Course
        {
            Id = "ppt", Name = "Phương pháp tính", Code = "MT1009", Scoring = new Scoring(20, 0.5, -0.1),
            Units =
            [
                new Unit { Title = "Chương 1", Lessons = [new LessonEntry("ppt-01", "Sai số"), new LessonEntry("ppt-02", "Sai số")] },
                new Unit { Title = "Chương 2", Lessons = [new LessonEntry("ppt-03", "Phương trình phi tuyến"), new LessonEntry("ppt-04", "Chưa soạn")] },
            ],
        });
        study.AddLesson(new Lesson
        {
            Id = "ppt-01", Title = "Sai số", Sources = [new SourceRef("slide.pdf", "3-5")], Sections = [new Section("Ý chính", " <p>a</p> ")],
            Questions =
            [
                new Question { Prompt = "1+1?", Options = ["1", "2"], Answer = 1, Solution = "Vì 2.<p><small>Lần làm trên LMS: bạn chọn A</small></p>" },
                new Question { Type = "numeric", Prompt = "pi?", Answer = -1, Value = 3.14, Tolerance = 0.01, Unit = "rad", Solution = "3,14." },
            ],
        });
        study.AddLesson(new Lesson { Id = "ppt-02", Title = "Sai số", Questions = [new Question { Type = "short", Prompt = "Tên?", Answer = -1, Accept = ["x"], Solution = "x." }] });
        study.AddLesson(new Lesson { Id = "ppt-03", Title = "Phương trình phi tuyến", Questions = [new Question { Type = "multi", Prompt = "Chọn", Options = ["a", "b", "c"], Answer = -1, Answers = [0, 2], Solution = "a, c." }] });
        study.AddExam(new Exam { Id = "ppt-de", CourseId = "ppt", Title = "Đề thử", Minutes = 30, QuestionIds = ["ppt-01.q1"] });
        study.AddExam(new Exam { Id = "ngau-nhien-1", CourseId = "ppt", Title = "Đề ngẫu nhiên", Minutes = 30, QuestionIds = ["ppt-01.q1"] });
        return study;
    }

    private static Transfer T(StudyRegistry study, IReadOnlyList<InstalledPack>? packs = null, IReadOnlyList<QuizInfo>? quizzes = null) =>
        new(study, packs ?? [], quizzes ?? [], "BK Study Desk", new FixedClock(Now));

    private static Scope At(StudyRegistry study, int? unit = null, string? lesson = null) => new(study.Course("ppt")!, unit, lesson);

    [Fact]
    public void Scope_names_lesson_unit_or_course()
    {
        var study = Make();
        Assert.Equal("Phương trình phi tuyến", Transfer.ScopeLabel(At(study, 1, "ppt-03")));
        Assert.Equal("Chương 2", Transfer.ScopeLabel(At(study, 1)));
        Assert.Equal("Phương pháp tính", Transfer.ScopeLabel(At(study)));
        Assert.Null(Transfer.LessonOf(At(study, 0, "ppt-03")));   // bài không thuộc chương này
    }

    [Fact]
    public void Slug_strips_marks_and_keeps_fallback()
    {
        Assert.Equal("phuong-trinh-phi-tuyen", Transfer.Slug("Phương trình phi tuyến"));
        Assert.Equal("dao-ham", Transfer.Slug("Đạo hàm!"));
        Assert.Equal("bai", Transfer.Slug("?", "bai"));
    }

    [Fact]
    public void Export_course_keeps_names_skips_random_exam_and_own_attempt()
    {
        var study = Make();
        var p = T(study).BuildExport(At(study), "An");
        Assert.Equal("hcmut-mt1009-tron-mon", p.Id);
        Assert.Equal("Phương pháp tính", p.Title);
        Assert.Equal(["Chương 1", "Chương 2"], p.Units.Select(u => u.Title));
        Assert.Equal(["sai-so", "sai-so-2"], p.Units[0].Lessons.Select(l => l.Id));
        var q1 = p.Units[0].Lessons[0].Questions![0];
        Assert.Equal("Vì 2.", q1.Solution);
        Assert.Equal(1, q1.AnswerNumber);
        var num = p.Units[0].Lessons[0].Questions![1];
        Assert.Equal(("numeric", 3.14, 0.01, "rad"), (num.Type, num.AnswerNumber, num.Tolerance, num.Unit));
        Assert.Equal("<p>a</p>", p.Units[0].Lessons[0].Sections![0].Body);
        Assert.Equal(new PackSource("slide.pdf", "3-5"), p.Units[0].Lessons[0].Sources![0]);
        var exam = Assert.Single(p.Exams!);
        Assert.Equal(("ppt-de", 0.5), (exam.Id, exam.Scoring!.Right));
        Assert.Equal(["An"], p.Authors.Select(a => a.Name));
        Assert.Equal("", string.Join(" | ", PackValidator.Validate(System.Text.Json.Nodes.JsonNode.Parse(p.ToJson())).Errors.Select(e => e.Path + " " + e.Message)));
    }

    [Fact]
    public void Export_one_unit_has_no_exams()
    {
        var study = Make();
        var p = T(study).BuildExport(At(study, 1), "");
        Assert.Equal("hcmut-mt1009-chuong-2", p.Id);
        Assert.Equal("Phương pháp tính: Chương 2", p.Title);
        Assert.Null(p.Exams);
        Assert.Equal([0, 2], p.Units[0].Lessons[0].Questions![0].Answers!);
    }

    [Fact]
    public void Locked_lms_quiz_is_not_exported()
    {
        var study = Make();
        var info = new QuizInfo(new SavedQuiz("1-1", "Quiz 1", "PPT", "MT1009", null, 1, null, null, null, null, null, null, []), "ppt-03", 1, 0, false);
        var t = T(study, quizzes: [info]);
        Assert.True(t.QuizLocked("ppt-03"));
        Assert.False(t.LessonCan("ppt-03").Export);
        Assert.Single(t.BuildExport(At(study), "").Units);
    }

    [Fact]
    public void Lms_lesson_takes_no_new_questions_except_recall()
    {
        var study = Make();
        var t = T(study);
        Assert.Equal(new LessonRights(false, false, true), t.LessonCan("quiz-lms-mt1009.q1"));
        Assert.Equal(new LessonRights(true, false, true), t.LessonCan(LmsQuiz.RecallPrefix + "mt1009.b1"));
        Assert.Equal(new LessonRights(true, true, true), t.LessonCan("ppt-01"));
    }

    [Fact]
    public void Question_markdown_round_trips()
    {
        var study = Make();
        var md = Transfer.QuestionMarkdown(study.Question("ppt-01.q1")!);
        Assert.StartsWith("### ", md, StringComparison.Ordinal);
        var back = StudyMarkdown.Parse("---\nid: x\nmon: X x\n---\n\n# C\n\n## B\n\n" + md);
        Assert.Empty(back.Errors);
        var q = back.Pack.Units[0].Lessons[0].Questions![0];
        Assert.Equal((1d, "1+1?"), (q.AnswerNumber!.Value, q.Prompt.Replace("<p>", "").Replace("</p>", "")));
    }

    private const string Json = """
        {"format":"studypack/1","id":"goi-b","title":"Gói B","version":"1.0.0","course":{"code":"MT1005","name":"GT2"},
         "authors":[{"name":"Bình"}],
         "units":[{"title":"U1","lessons":[
           {"id":"l1","title":"L1","questions":[{"prompt":"a?","options":["x","y"],"answer":0,"solution":"s"}]}]},{"title":"U2","lessons":[
           {"id":"l2","title":"L2","questions":[{"prompt":"b?","options":["x","y"],"answer":1,"solution":"s"}]}]}],
         "exams":[{"id":"e1","title":"Đề","minutes":10,"questionRefs":["l1.q1"]}]}
        """;

    [Fact]
    public void Import_into_lesson_merges_and_renumbers()
    {
        var study = Make();
        var t = T(study);
        var r = t.PrepareImport(Json, At(study, 0, "ppt-01"));
        Assert.Null(r.Error);
        Assert.True(r.Report!.Ok);
        var p = r.Pack!;
        Assert.Equal(("MT1009", "Phương pháp tính"), (p.Course.Code, p.Course.Name));
        var l = Assert.Single(Assert.Single(p.Units).Lessons);
        Assert.Equal(("Chương 1", "sai-so", "Sai số"), (p.Units[0].Title, l.Id, l.Title));
        Assert.Equal(["q1", "q2"], l.Questions!.Select(q => q.Id));
        Assert.Null(p.Exams);
        Assert.Contains(r.Notes, n => n.Contains("MT1005"));
        Assert.Contains(r.Notes, n => n.Contains("2 câu"));
    }

    [Fact]
    public void Import_into_unit_renames_repeated_lesson_ids_and_exam_refs()
    {
        var study = Make();
        var t = T(study);
        var p = t.PrepareImport(Json, At(study, 1))!.Pack!;
        Assert.Equal("Chương 2", Assert.Single(p.Units).Title);
        Assert.Equal(["l1", "l2"], p.Units[0].Lessons.Select(l => l.Id));
        Assert.Equal(["l1.q1"], p.Exams![0].QuestionRefs);
    }

    [Fact]
    public void Import_without_scope_keeps_pack()
    {
        var r = T(Make()).PrepareImport("```json\n" + Json + "\n```", null);
        Assert.Equal("", r.Error + string.Join(" | ", r.Report?.Errors.Select(e => e.Path + " " + e.Message) ?? []));
        Assert.Equal("goi-b", r.Pack!.Id);
        Assert.Equal("MT1005", r.Pack.Course.Code);
    }

    [Fact]
    public void Reserved_id_is_renamed()
    {
        var r = T(Make()).PrepareImport(Json.Replace("\"goi-b\"", "\"tu-soan-mt1005\""), null);
        Assert.Equal("nhan-tu-soan-mt1005", r.Pack!.Id);
    }

    [Fact]
    public void Bare_markdown_questions_go_into_the_picked_lesson()
    {
        var study = Make();
        var t = T(study);
        var r = t.PrepareImport("### Câu 1\nHỏi?\n- [ ] a\n- [x] b\n> vì b\n", At(study, 1, "ppt-03"));
        Assert.Null(r.Error);
        var l = r.Pack!.Units[0].Lessons[0];
        Assert.Equal("Phương trình phi tuyến", l.Title);
        Assert.Equal(1, l.Questions![0].AnswerNumber);
    }

    [Fact]
    public void Markdown_errors_shift_line_numbers_back()
    {
        var study = Make();
        var t = T(study);
        var r = t.PrepareImport("### Câu 1\nHỏi?\n- [ ] a\n- [ ] b\n", At(study, 1, "ppt-03"));
        Assert.NotNull(r.Error);
        Assert.All(r.Notes, n => Assert.DoesNotContain("dòng 0", n));
    }

    [Fact]
    public void Gift_needs_a_course_and_becomes_a_pack()
    {
        var study = Make();
        var t = T(study);
        const string gift = "::Q1:: 2+2 = {=4 ~3 ~5}\n";
        Assert.NotNull(t.PrepareImport(gift, null).Error);
        var r = t.PrepareImport(gift, At(study));
        Assert.Null(r.Error);
        Assert.StartsWith("nhap-mt1009-gift-", r.Pack!.Id, StringComparison.Ordinal);
        Assert.Contains(r.Notes, n => n.Contains("GIFT"));
    }

    [Fact]
    public void Unknown_text_is_an_error()
    {
        Assert.NotNull(T(Make()).PrepareImport("xin chào", null).Error);
    }

    [Fact]
    public void Authored_pack_reuses_installed_and_adds_author()
    {
        var study = Make();
        var c = study.Course("ppt")!;
        var fresh = T(study).AuthoredPack(c, "An");
        Assert.Equal(("tu-soan-mt1009", "Phương pháp tính: câu tự tạo"), (fresh.Id, fresh.Title));
        var l = Transfer.LessonIn(fresh, "Chương 1", "Sai số");
        l.Questions!.Add(new PackQuestion { Prompt = "x", Options = ["a", "b"], Answer = 0 });
        Assert.Same(l, Transfer.LessonIn(fresh, "chương 1", "SAI SỐ"));
        Assert.Equal("sai-so-2", Transfer.LessonIn(fresh, "Chương 2", "Sai số").Id);
        var again = T(study, [new InstalledPack("tu-soan-mt1009.studypack.json", fresh, null, 1)]).AuthoredPack(c, "Bình");
        Assert.Equal(["An", "Bình"], again.Authors.Select(a => a.Name));
        Assert.NotSame(fresh, again);
        Assert.Equal("tu-soan-mt1009", T(study).AuthoredPack(c, "", recall: false).Id);
        Assert.Equal("quiz-lms-ghi-mt1009", T(study).AuthoredPack(c, "", recall: true).Id);
    }

    [Fact]
    public void Bump_version_raises_patch()
    {
        var p = new StudyPack { Version = "1.2.9" };
        Transfer.BumpVersion(p);
        Assert.Equal("1.2.10", p.Version);
    }

    [Fact]
    public void Migration_moves_results_by_fingerprint()
    {
        var study = Make();
        var pack = StudyPack.Parse(Json)!;
        study.AddPack(pack);
        var t = T(study);
        var next = StudyPack.Parse(Json)!;
        // Bản mới: câu a dời sang bài l3 (id đổi, fingerprint giữ), câu b sửa đề (fingerprint đổi, id giữ).
        next.Units[0].Lessons[0].Id = "l3";
        next.Units[1].Lessons[0].Questions![0].Prompt = "b sửa?";
        next.Exams = null;
        var (moves, gone) = t.Migration(next);
        Assert.Equal("goi-b.l3.q1", moves["goi-b.l1.q1"]);
        Assert.Equal(["goi-b.l1.q1"], gone);
        Assert.False(moves.ContainsKey("goi-b.l2.q1"));
    }

    [Fact]
    public void Update_notes_report_reinstall_and_duplicates()
    {
        var study = Make();
        var pack = StudyPack.Parse(Json)!;
        var t = T(study, [new InstalledPack("goi-b.studypack.json", pack, null, 2)]);
        var r = t.PrepareImport(Json.Replace("\"a?\"", "\"1+1?\"").Replace("[\"x\",\"y\"],\"answer\":0", "[\"1\",\"2\"],\"answer\":1"), null);
        Assert.NotNull(r.Pack);
        var placed = t.PrepareImport(Json, At(study));
        Assert.Contains(placed.Notes, n => n.Contains("v1.0.0"));
    }

    [Fact]
    public void Export_markdown_zips_when_there_are_images()
    {
        var p = new StudyPack
        {
            Id = "goi-anh", Title = "Ảnh", Version = "1.0.0", Course = new PackCourse("X1", "X"), Authors = [new PackAuthor("a")],
            Units = [new PackUnit { Title = "U", Lessons = [new PackLesson { Id = "l", Title = "L", Questions = [new PackQuestion { Prompt = "<img src=\"data:image/png;base64,AQID\">", Options = ["a", "b"], Answer = 0 }] }] }],
        };
        var (name, data) = Transfer.ExportMarkdown(p);
        Assert.Equal("goi-anh.zip", name);
        using var zip = new ZipArchive(new MemoryStream(data));
        Assert.Contains(zip.Entries, e => e.FullName == "goi.md");
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("img/", StringComparison.Ordinal));
        p.Units[0].Lessons[0].Questions![0].Prompt = "chữ";
        Assert.Equal("goi-anh.md", Transfer.ExportMarkdown(p).FileName);
    }
}

public class ReportTextTests
{
    [Fact]
    public void Report_has_place_flag_note_and_question()
    {
        var study = new StudyRegistry();
        study.Manifest.Courses.Add(new Course { Id = "ppt", Name = "PPT", Units = [new Unit { Title = "C1", Lessons = [new LessonEntry("b1", "Bài 1")] }] });
        study.AddLesson(new Lesson { Id = "b1", Title = "Bài 1", Questions = [new Question { Prompt = "1+1?", Options = ["1", "2"], Answer = 1, Solution = "2." }] });
        var p = new PracticeProgress(study, new MemoryStorage());
        var q = study.Question("b1.q1")!;
        p.SetNote(q.Fp, text: "đáp án --> sai", flag: true);
        var text = PracticeActions.ReportText(study, p, [q]);
        var lines = text.Split('\n');
        Assert.Equal("<!-- PPT, Bài 1 -->", lines[0]);
        Assert.Equal("<!-- nghi đáp án hoặc lời giải sai -->", lines[1]);
        Assert.Equal("<!-- ghi chú: đáp án - -> sai -->", lines[2]);
        Assert.StartsWith("### ", lines[3], StringComparison.Ordinal);
    }
}

public class ReadFileTests
{
    private static byte[] Zip(params (string Name, byte[] Data)[] files)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, data) in files)
            {
                using var s = z.CreateEntry(name).Open();
                s.Write(data);
            }
        return ms.ToArray();
    }

    [Fact]
    public void Text_file_is_read_as_utf8()
    {
        var r = Transfer.ReadFile("a.md", System.Text.Encoding.UTF8.GetBytes("### Câu"))!.Value;
        Assert.Equal(("### Câu", 0), (r.Text, r.Images.Count));
    }

    [Fact]
    public void Zip_gives_markdown_and_images_under_img()
    {
        var r = Transfer.ReadFile("goi.zip", Zip(("goi/goi.md", "# x"u8.ToArray()), ("goi/img/a.png", [1, 2, 3])))!.Value;
        Assert.Equal("# x", r.Text);
        Assert.Equal("data:image/png;base64,AQID", r.Images["img/a.png"]);
    }

    [Fact]
    public void Zip_bomb_is_refused()
    {
        Assert.Null(Transfer.ReadFile("bom.zip", Zip(("a.md", new byte[61 * 1024 * 1024]))));
        Assert.Null(Transfer.ReadFile("big.md", new byte[Transfer.MaxFile + 1]));
    }
}
