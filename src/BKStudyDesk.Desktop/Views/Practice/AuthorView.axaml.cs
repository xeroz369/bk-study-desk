using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BKStudyDesk.Desktop.Practice;
using SoHocTap.Core;
using SoHocTap.Presentation.Practice;
using SoHocTap.Ui;

namespace BKStudyDesk.Desktop.Views.Practice;

/// <summary>
/// Soạn: chọn chỗ (môn, chương, bài) rồi Tạo câu, Nhập, Xuất, Prompt cho AI (port Soan.svelte, QuestionEditor.svelte).
/// Câu, bài tự soạn lưu vào gói tu-soan-mã môn qua <see cref="PracticeService.InstallAsync"/>; sau khi lưu, host nạp lại sổ và mở lại
/// màn này đúng chỗ (sự kiện <see cref="Saved"/>).
/// </summary>
public partial class AuthorView : UserControl
{
    private const string Letters = "ABCDEFGH";

    public AuthorView() => InitializeComponent();

    public event Action? BackRequested;

    /// <summary>Đã lưu gói: host nạp lại sổ rồi mở lại Soạn ở chỗ (mã môn, tên chương, tên bài), tab, kèm câu báo.</summary>
    public event Action<AuthorPlace>? Saved;

    private readonly PracticeService _service = null!;
    private readonly List<Course> _courses = [];
    private readonly List<RadioButton> _types = [];
    private readonly List<RadioButton> _modes = [];
    private Scope? _scope;
    private bool _loading;
    private Dictionary<string, string> _rawImages = [];
    private Prepared? _prepared;
    private StudyPack? _exported;
    private PackReport? _exportReport;
    private Button? _armed;
    private readonly DispatcherTimer _disarm = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _importDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };

    internal AuthorView(PracticeService service, AuthorPlace? place = null) : this()
    {
        _service = service;
        AuthorBox.Text = PracticeService.Author;
        foreach (var t in AuthorForm.Types)
        {
            var r = new RadioButton { GroupName = "type", Content = L.T("practice.author.type." + t), Tag = t, IsChecked = t == "single", Margin = new Avalonia.Thickness(0, 0, 16, 0) };
            r.IsCheckedChanged += (_, _) => { if (r.IsChecked == true) TypeChanged(); };
            _types.Add(r);
            Types.Children.Add(r);
        }
        for (var i = 0; i < 4; i++) AddOption(right: i == 0);
        foreach (var (m, key) in new[] { (PromptMode.Soan, "soan"), (PromptMode.TuongTu, "tuongTu"), (PromptMode.Soat, "soat"), (PromptMode.GiaiThich, "giaiThich") })
        {
            var r = new RadioButton { GroupName = "mode", Content = L.T("practice.author.prompt." + key), Tag = m, IsChecked = m == PromptMode.Soan, Margin = new Avalonia.Thickness(0, 0, 16, 0) };
            r.IsCheckedChanged += (_, _) => { if (r.IsChecked == true) UpdatePrompt(); };
            _modes.Add(r);
            Modes.Children.Add(r);
        }
        _disarm.Tick += (_, _) => Disarm();
        _importDelay.Tick += (_, _) => { _importDelay.Stop(); UpdateImport(); };
        CreateColumns.SizeChanged += (_, _) => Layout(CreateColumns.Bounds.Width);
        if (place?.Flash is { Length: > 0 } flash)
        {
            FlashText.Text = flash;
            FlashText.IsVisible = true;
        }
        LoadCourses(place);
        Tabs.SelectedIndex = place?.Tab ?? 0;
        TypeChanged();
        _ = LoadSubjectsAsync();
    }

    private string Author => AuthorBox.Text?.Trim() ?? "";

    private Transfer Tx() => _service.Transfer();

    // ------------------------------------------------------------------ chỗ

    private void LoadCourses(AuthorPlace? place)
    {
        _loading = true;
        _courses.Clear();
        _courses.AddRange(_service.Study.Manifest.Courses);
        if (place?.CourseId is { } cid && _service.Study.Course(cid) is { } draft && !_courses.Contains(draft)) _courses.Add(draft);
        CourseBox.ItemsSource = _courses.Select(CourseLabel).ToList();
        NoCourseText.IsVisible = _courses.Count == 0;
        var course = _courses.FirstOrDefault(c => c.Id == place?.CourseId) ?? _courses.FirstOrDefault();
        CourseBox.SelectedIndex = course is null ? -1 : _courses.IndexOf(course);
        _loading = false;
        if (course is null) { SetScope(null); return; }
        var ui = place?.UnitTitle is { } ut ? course.Units.FindIndex(u => Text.NormText(u.Title) == Text.NormText(ut)) : -1;
        var lesson = ui >= 0 && place?.LessonTitle is { } lt ? course.Units[ui].Lessons.FirstOrDefault(e => Text.NormText(e.Title) == Text.NormText(lt)) : null;
        SetScope(new Scope(course, ui >= 0 ? ui : null, lesson?.Id));
        // Chương, bài chưa có (ví dụ Ghi lại câu còn nhớ của quiz LMS): điền sẵn tên vào thẻ Bài mới.
        if (lesson is null && place?.LessonTitle is { } newLesson) NewLessonBox.Text = newLesson;
        if (ui < 0 && place?.UnitTitle is { } newUnit) NewUnitBox.Text = newUnit;
    }

    private string CourseLabel(Course c) =>
        c.Units.Count == 0 && _service.Study.Drafts.Contains(c) ? L.F("practice.author.courseNew", c.Name, c.Code ?? "") : c.Name;

    // Môn đang học kỳ này (LMS, MyBK): thêm vào cuối danh sách nếu sổ chưa có, chọn thì thành môn nháp (EnsureCourse).
    private async Task LoadSubjectsAsync()
    {
        var subjects = await _service.SubjectsAsync();
        var added = false;
        foreach (var (code, name) in subjects)
            if (!_courses.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                _courses.Add(_service.Study.EnsureCourse(code, name));
                added = true;
            }
        if (!added) return;
        _loading = true;
        var sel = CourseBox.SelectedIndex;
        CourseBox.ItemsSource = _courses.Select(CourseLabel).ToList();
        CourseBox.SelectedIndex = sel >= 0 ? sel : 0;
        NoCourseText.IsVisible = _courses.Count == 0;
        _loading = false;
        if (sel < 0) SetScope(new Scope(_courses[0]));
    }

    private void SetScope(Scope? s)
    {
        _scope = s;
        _loading = true;
        var c = s?.Course;
        UnitBox.ItemsSource = c is null ? null : (List<string>)[L.T("practice.author.wholeCourse"), .. c.Units.Select(u => u.Title)];
        UnitBox.SelectedIndex = c is null ? -1 : (s!.Unit ?? -1) + 1;
        var unit = s is null ? null : Transfer.UnitOf(s);
        LessonBox.ItemsSource = unit is null ? null : (List<string>)[L.T("practice.author.wholeUnit"), .. unit.Lessons.Select(e => e.Title)];
        LessonBox.SelectedIndex = unit is null ? -1 : unit.Lessons.FindIndex(e => e.Id == s!.LessonId) + 1;
        LessonBox.IsEnabled = unit != null;
        _loading = false;
        Refresh();
    }

    private void OnCourse(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading && CourseBox.SelectedIndex is >= 0 and var i && i < _courses.Count) SetScope(new Scope(_courses[i]));
    }

    private void OnUnit(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _scope is null) return;
        SetScope(new Scope(_scope.Course, UnitBox.SelectedIndex > 0 ? UnitBox.SelectedIndex - 1 : null));
    }

    private void OnLesson(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _scope is null || Transfer.UnitOf(_scope) is not { } unit) return;
        SetScope(_scope with { LessonId = LessonBox.SelectedIndex > 0 ? unit.Lessons[LessonBox.SelectedIndex - 1].Id : null });
    }

    private void OnAuthorLost(object? sender, RoutedEventArgs e)
    {
        if (Author != PracticeService.Author) PracticeService.Author = Author;
        Refresh();
    }

    private string Label => _scope is null ? "" : Transfer.ScopeLabel(_scope);

    private void Refresh()
    {
        var unit = _scope is null ? null : Transfer.UnitOf(_scope);
        var lesson = _scope is null ? null : Transfer.LessonOf(_scope);
        var rights = lesson is null ? new LessonRights(true, true, true) : Tx().LessonCan(lesson.Id);
        // Tạo câu: cần chọn bài; chưa chọn thì thẻ Bài mới.
        QuestionCard.IsVisible = lesson != null && rights.Create;
        QuestionHead.Text = lesson is null ? "" : L.F("practice.run.where", unit!.Title, lesson.Title);
        LessonCard.IsVisible = _scope != null && lesson is null;
        NewUnitPanel.IsVisible = unit is null;
        MineRows.Children.Clear();
        var mine = Mine();
        MineCard.IsVisible = mine.Count > 0;
        foreach (var q in mine) MineRows.Children.Add(MineRow(q));
        ImportHead.Text = L.F("practice.author.import.card", Label);
        ImportButton.IsEnabled = rights.Import && _prepared?.Report?.Ok == true;
        PromptHead.Text = Label;
        UpdateExport();
        UpdateImport();
        UpdatePrompt();
        Render();
    }

    private bool Recall => _scope is { } s && (Transfer.UnitOf(s)?.Title ?? NewUnitBox.Text?.Trim()) == LmsQuiz.UnitTitle;

    private string AuthoredId() => _scope is null ? "" : Tx().AuthoredPack(_scope.Course, "", Recall).Id;

    private List<Question> Mine()
    {
        if (_scope is null || Transfer.LessonOf(_scope) is not { } lesson) return [];
        var id = AuthoredId();
        return [.. (_service.Study.Lessons.GetValueOrDefault(lesson.Id)?.Questions ?? []).Where(q => q.PackId == id)];
    }

    private Border MineRow(Question q)
    {
        var del = new Button { Content = L.T("practice.author.remove"), Tag = q.Id, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        del.Click += OnRemove;
        DockPanel.SetDock(del, Dock.Right);
        var text = Text.PlainText(q.Prompt);
        return new Border
        {
            Classes = { "row" },
            Child = new DockPanel
            {
                Children =
                {
                    del,
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Classes = { "name" }, Text = text.Length > 120 ? text[..120] : text },
                            new TextBlock { Classes = { "sub" }, Text = q.Tag ?? "" },
                        },
                    },
                },
            },
        };
    }

    // ------------------------------------------------------------------ tạo câu

    private string Type => _types.FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? "single";

    private void TypeChanged()
    {
        var type = Type;
        OptionsPanel.IsVisible = type is "single" or "multi";
        TruthBox.IsVisible = type == "truefalse";
        NumericPanel.IsVisible = type == "numeric";
        ShortPanel.IsVisible = type == "short";
        // Một đáp án dùng RadioButton, nhiều đáp án dùng CheckBox; giữ chữ đã gõ.
        var rows = OptionRows.Children.Cast<Grid>().Select(r => (Text: ((TextBox)r.Children[1]).Text ?? "", Right: ((ToggleButton)r.Children[0]).IsChecked == true)).ToList();
        OptionRows.Children.Clear();
        var first = true;
        foreach (var (text, right) in rows)
        {
            AddOption(text, type == "single" ? right && first : right);
            if (right) first = false;
        }
        Render();
    }

    private void AddOption(string text = "", bool right = false)
    {
        if (OptionRows.Children.Count >= Letters.Length) return;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,8,*") };
        ToggleButton mark = Type == "multi" ? new CheckBox() : new RadioButton { GroupName = "right" };
        mark.IsChecked = right;
        mark.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        mark.IsCheckedChanged += (_, _) => Render();
        mark.Content = Letters[OptionRows.Children.Count].ToString();
        row.Children.Add(mark);
        var box = new TextBox { Text = text };
        box.TextChanged += (_, _) => Render();
        Grid.SetColumn(box, 2);
        row.Children.Add(box);
        OptionRows.Children.Add(row);
        AddOptionButton.IsEnabled = OptionRows.Children.Count < Letters.Length;
    }

    private void OnAddOption(object? sender, RoutedEventArgs e) => AddOption();

    private AuthorForm Form() => new()
    {
        Type = Type,
        Prompt = PromptBox.Text ?? "",
        Solution = SolutionBox.Text ?? "",
        Tag = TagBox.Text ?? "",
        KeepOrder = KeepOrderBox.IsChecked == true,
        Options = [.. OptionRows.Children.Cast<Grid>().Select(r => new AuthorOption(((TextBox)r.Children[1]).Text ?? "", ((ToggleButton)r.Children[0]).IsChecked == true))],
        Truth = TruthBox.IsChecked == true,
        Value = ValueBox.Text ?? "",
        Tolerance = ToleranceBox.Text ?? "",
        Unit = UnitBoxText.Text ?? "",
        Accept = AcceptBox.Text ?? "",
    };

    private void OnFormText(object? sender, TextChangedEventArgs e) => Render();
    private void OnFormCheck(object? sender, RoutedEventArgs e) => Render();

    // Xem trước: vẽ đúng như màn làm câu (Markdown và TeX qua bộ lọc HTML).
    private void Render()
    {
        if (Preview is null) return;
        Preview.Children.Clear();
        var f = Form();
        if (f.Prompt.Trim().Length > 0) Preview.Children.Add(ContentRenderer.Render(AuthorForm.Render(f.Prompt)));
        switch (f.Type)
        {
            case "single" or "multi":
                var k = 0;
                foreach (var o in f.Options.Where(o => o.Text.Trim().Length > 0))
                {
                    ToggleButton b = f.Type == "multi" ? new CheckBox() : new RadioButton { GroupName = "preview" };
                    var row = new DockPanel();
                    var letter = new TextBlock { Classes = { "marker" }, Text = Letters[Math.Min(k++, Letters.Length - 1)] + "." };
                    DockPanel.SetDock(letter, Dock.Left);
                    row.Children.Add(letter);
                    row.Children.Add(ContentRenderer.Render(AuthorForm.Render(o.Text)));
                    b.Content = row;
                    b.IsChecked = o.Right;
                    b.IsHitTestVisible = false;
                    Preview.Children.Add(b);
                }
                break;
            case "truefalse":
                Preview.Children.Add(new TextBlock { Text = L.F("practice.run.answerIs", L.T(f.Truth ? "practice.author.isTrue" : "practice.author.isFalse")) });
                break;
            case "numeric" or "short":
                Preview.Children.Add(new TextBlock { Text = L.F("practice.run.answerIs", f.Type == "numeric" ? f.Value : f.Accept) });
                break;
        }
        if (f.Solution.Trim().Length > 0)
            Preview.Children.Add(new Border { Classes = { "callout", "note" }, Child = ContentRenderer.Render(AuthorForm.Render(f.Solution)) });
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_scope is null || Transfer.UnitOf(_scope) is not { } unit || Transfer.LessonOf(_scope) is not { } lesson)
        {
            ShowError(CreateError, L.T("practice.author.err.noLesson"));
            return;
        }
        var (q, err) = Form().Build();
        if (q is null)
        {
            ShowError(CreateError, err!);
            return;
        }
        var p = Tx().AuthoredPack(_scope.Course, Author, Recall);
        var l = Transfer.LessonIn(p, unit.Title, lesson.Title);
        l.Questions ??= [];
        var used = l.Questions.Select(x => x.Id).ToHashSet();
        var n = l.Questions.Count + 1;
        while (used.Contains($"q{n}")) n++;
        q.Id = $"q{n}";
        l.Questions.Add(q);
        await SaveAsync(p, CreateError, new AuthorPlace(_scope.Course.Id, unit.Title, lesson.Title, 0, L.F("practice.author.saved", p.Title)));
    }

    private async void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string qid || _scope is null || Transfer.UnitOf(_scope) is not { } unit || Transfer.LessonOf(_scope) is not { } lesson) return;
        if (_armed != b)
        {
            Disarm();
            _armed = b;
            b.Content = L.T("practice.author.removeConfirm");
            _disarm.Start();
            return;
        }
        Disarm();
        var p = Tx().AuthoredPack(_scope.Course, Author, Recall);
        var l = Transfer.LessonIn(p, unit.Title, lesson.Title);
        var local = qid.Split('.')[^1];
        l.Questions = [.. (l.Questions ?? []).Where(x => x.Id != local)];
        await SaveAsync(p, CreateError, new AuthorPlace(_scope.Course.Id, unit.Title, lesson.Title, 0, null));
    }

    private void Disarm()
    {
        _disarm.Stop();
        if (_armed is { } b) b.Content = b == ImportButton ? L.T("practice.author.import.go") : L.T("practice.author.remove");
        _armed = null;
    }

    private async void OnCreateLesson(object? sender, RoutedEventArgs e)
    {
        if (_scope is null) return;
        var unitTitle = Transfer.UnitOf(_scope)?.Title ?? NewUnitBox.Text?.Trim() ?? "";
        var lessonTitle = NewLessonBox.Text?.Trim() ?? "";
        if (unitTitle.Length == 0 || lessonTitle.Length == 0)
        {
            ShowError(LessonError, L.T("practice.author.err.needNames"));
            return;
        }
        var p = Tx().AuthoredPack(_scope.Course, Author, Recall);
        var l = Transfer.LessonIn(p, unitTitle, lessonTitle);
        if (BodyBox.Text?.Trim() is { Length: > 0 } body) l.Sections = [.. l.Sections ?? [], new PackSection(L.T("practice.author.knowledge"), AuthorForm.Render(body))];
        if (l.Sections is not { Count: > 0 } && l.Questions is not { Count: > 0 })
            l.Sections = [new PackSection(L.T("practice.author.notes"), $"<p>{L.T("practice.author.emptyLesson")}</p>")];
        await SaveAsync(p, LessonError, new AuthorPlace(_scope.Course.Id, unitTitle, lessonTitle, 0, L.F("practice.author.saved", p.Title)));
    }

    private async Task SaveAsync(StudyPack p, TextBlock errorText, AuthorPlace after)
    {
        var report = PackValidator.Validate(System.Text.Json.Nodes.JsonNode.Parse(p.ToJson()));
        if (!report.Ok)
        {
            ShowError(errorText, string.Join("\n", report.Errors.Take(6).Select(Issue)));
            return;
        }
        Transfer.BumpVersion(p);
        if (await _service.InstallAsync(p) is { } err)
        {
            ShowError(errorText, L.F("practice.author.err.save", err));
            return;
        }
        Saved?.Invoke(after);
    }

    private static string Issue(PackIssue i) => (i.Path.Length > 0 ? i.Path + ": " : "") + i.Message;

    private static void ShowError(TextBlock t, string text)
    {
        t.Text = text;
        t.IsVisible = text.Length > 0;
    }

    // ------------------------------------------------------------------ nhập

    private void OnRawChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_loading) _rawImages = [];
        // Đọc lại gói sau khi ngừng gõ một chút: dán tệp lớn không làm giật mỗi phím.
        _importDelay.Stop();
        _importDelay.Start();
    }

    private void UpdateImport()
    {
        var raw = RawBox.Text ?? "";
        _prepared = raw.Trim().Length > 0 ? Tx().PrepareImport(raw, _scope, Author, _rawImages) : null;
        var notes = new List<string>();
        if (_prepared?.Error is { } err)
        {
            ImportTitle.Text = err;
            notes.AddRange(_prepared.Notes);
            notes.Add(L.T("practice.author.import.aiTip"));
        }
        else if (_prepared?.Report is { } r)
        {
            ImportTitle.Text = r.Ok ? L.F("practice.author.import.ok", r.Stats.Lessons, r.Stats.Questions, r.Stats.Exams) : L.F("practice.author.import.bad", r.Errors.Count);
            notes.AddRange(_prepared.Notes);
            notes.AddRange(r.Errors.Take(8).Select(Issue));
            notes.AddRange(r.Warnings.Take(5).Select(w => L.F("transfer.warn", Issue(w))));
        }
        else ImportTitle.Text = "";
        ImportTitle.Classes.Set("danger", _prepared is { Error: not null } or { Report.Ok: false });
        ImportNotes.Text = string.Join("\n", notes);
        ImportNotes.IsVisible = notes.Count > 0;
        var rights = _scope is not null && Transfer.LessonOf(_scope) is { } lesson ? Tx().LessonCan(lesson.Id) : new LessonRights(true, true, true);
        ImportButton.IsEnabled = rights.Import && _prepared?.Report?.Ok == true && _prepared.Pack != null;
    }

    private async void OnPickFile(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(L.T("practice.author.import.fileTypes")) { Patterns = ["*.md", "*.zip", "*.json", "*.xml", "*.txt", "*.gift"] }],
        });
        if (files.Count == 0) return;
        var f = files[0];
        try
        {
            await using var s = await f.OpenReadAsync();
            using var ms = new MemoryStream();
            // Đọc tối đa MaxFile + 1 byte: file lớn hơn thì ReadFile từ chối, không đọc hết vào bộ nhớ.
            var buf = new byte[81920];
            int n;
            while ((n = await s.ReadAsync(buf)) > 0 && ms.Length <= Transfer.MaxFile) ms.Write(buf, 0, n);
            if (Transfer.ReadFile(f.Name, ms.ToArray()) is not { } r)
            {
                ShowFileError(f.Name, L.T("practice.author.import.tooBig"));
                return;
            }
            _loading = true;
            RawBox.Text = r.Text;
            _loading = false;
            _rawImages = r.Images;
            UpdateImport();
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowFileError(f.Name, x.Message);
        }
    }

    private void ShowFileError(string name, string why)
    {
        ImportTitle.Text = L.F("practice.author.import.readFail", name, why);
        ImportTitle.Classes.Set("danger", true);
        ImportNotes.IsVisible = false;
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (_prepared?.Pack is not { } p || _prepared.Report?.Ok != true) return;
        // Đã có gói cùng id: bấm lần nữa mới thay.
        if (_service.Packs.Any(x => x.Pack?.Id == p.Id) && _armed != ImportButton)
        {
            Disarm();
            _armed = ImportButton;
            ImportButton.Content = L.F("practice.author.import.replace", p.Id);
            _disarm.Start();
            return;
        }
        Disarm();
        if (await _service.InstallAsync(p) is { } err)
        {
            ImportTitle.Text = L.F("practice.author.err.save", err);
            ImportTitle.Classes.Set("danger", true);
            return;
        }
        var unit = _scope is null ? null : Transfer.UnitOf(_scope);
        var lesson = _scope is null ? null : Transfer.LessonOf(_scope);
        Saved?.Invoke(new AuthorPlace(_scope?.Course.Id, unit?.Title, lesson?.Title, 1, L.F("practice.author.import.done", p.Title)));
    }

    // ------------------------------------------------------------------ xuất

    private void UpdateExport()
    {
        _exported = _scope is null ? null : Tx().BuildExport(_scope, Author);
        _exportReport = _exported is null ? null : PackValidator.Validate(System.Text.Json.Nodes.JsonNode.Parse(_exported.ToJson()));
        ExportHead.Text = L.F("practice.author.export.card", Label);
        ExportStats.Text = _exported is null || _exportReport is null ? ""
            : L.F("practice.author.export.stats", _exportReport.Stats.Lessons, PackValidator.CountQuestions(_exported), _exportReport.Stats.Exams);
        ExportErrors.IsVisible = _exportReport is { Ok: false };
        ExportErrors.Text = _exportReport is { Ok: false } r ? L.F("practice.author.export.errors", string.Join("; ", r.Errors.Take(6).Select(Issue))) : "";
        var off = _exportReport is not { Ok: true } || _exportReport.Stats.Lessons == 0;
        foreach (var b in new[] { ExportMdButton, ExportCopyButton, ExportXmlButton, ExportGiftButton, ExportJsonButton }) b.IsEnabled = !off;
    }

    private async Task SaveFileAsync(string name, byte[] data, string typeName, string pattern)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = name,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [pattern] }],
        });
        if (file is null) return;
        try
        {
            await using var s = await file.OpenWriteAsync();
            await s.WriteAsync(data);
            ExportDone.Text = L.F("practice.author.export.saved", file.Name);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Soạn: lưu file xuất: {x.Message}");
            ExportDone.Text = L.F("practice.author.err.save", x.Message);
        }
    }

    private static byte[] Utf8(string s) => new UTF8Encoding(false).GetBytes(s);

    private async void OnExportMd(object? sender, RoutedEventArgs e)
    {
        if (_exported is null) return;
        var (name, data) = Transfer.ExportMarkdown(_exported);
        await SaveFileAsync(name, data, name.EndsWith(".zip", StringComparison.Ordinal) ? "Zip" : "Markdown", "*" + Path.GetExtension(name));
    }

    private async void OnExportCopy(object? sender, RoutedEventArgs e)
    {
        if (_exported is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) return;
        await clip.SetTextAsync(StudyMarkdown.Write(_exported).Md);
        ExportDone.Text = L.T("practice.author.export.copied");
    }

    private async void OnExportXml(object? sender, RoutedEventArgs e)
    {
        if (_exported != null) await SaveFileAsync($"{_exported.Id}.xml", Utf8(QuizFormats.ToMoodleXml(_exported)), "Moodle XML", "*.xml");
    }

    private async void OnExportGift(object? sender, RoutedEventArgs e)
    {
        if (_exported != null) await SaveFileAsync($"{_exported.Id}.gift.txt", Utf8(QuizFormats.ToGift(_exported)), "GIFT", "*.txt");
    }

    private async void OnExportJson(object? sender, RoutedEventArgs e)
    {
        if (_exported != null) await SaveFileAsync($"{_exported.Id}.studypack.json", Utf8(_exported.ToJson() + "\n"), "JSON", "*.json");
    }

    // ------------------------------------------------------------------ prompt cho AI

    private PromptMode Mode => _modes.FirstOrDefault(r => r.IsChecked == true)?.Tag is PromptMode m ? m : PromptMode.Soan;

    private List<Question> _scopeQuestions = [];

    private void UpdatePrompt()
    {
        if (PromptText is null) return;
        var mode = Mode;
        var needsQ = mode is PromptMode.TuongTu or PromptMode.GiaiThich;
        PickPanel.IsVisible = needsQ;
        CountPanel.IsVisible = mode is PromptMode.Soan or PromptMode.TuongTu;
        StepsText.Text = L.T(mode == PromptMode.GiaiThich ? "practice.author.prompt.stepsExplain" : "practice.author.prompt.steps");
        if (_scope is null)
        {
            PromptText.Text = "";
            CopyPromptButton.IsEnabled = false;
            return;
        }
        var s = _scope;
        var unit = Transfer.UnitOf(s);
        var lesson = Transfer.LessonOf(s);
        var entries = lesson is not null ? [lesson] : unit is not null ? unit.Lessons : s.Course.Units.SelectMany(u => u.Lessons).ToList();
        var questions = entries.SelectMany(e => _service.Study.Lessons.GetValueOrDefault(e.Id)?.Questions ?? []).Take(200).ToList();
        if (!questions.Select(q => q.Id).SequenceEqual(_scopeQuestions.Select(q => q.Id)))
        {
            _scopeQuestions = questions;
            _loading = true;
            PickBox.ItemsSource = questions.Select(q => { var t = Text.PlainText(q.Prompt); return t.Length > 110 ? t[..110] : t; }).ToList();
            PickBox.SelectedIndex = -1;
            _loading = false;
        }
        var picked = PickBox.SelectedIndex >= 0 && PickBox.SelectedIndex < _scopeQuestions.Count ? _scopeQuestions[PickBox.SelectedIndex] : null;
        var code = s.Course.Code ?? s.Course.Id.ToUpperInvariant();
        PromptText.Text = Prompts.Build(mode, new PromptInput(
            $"{code} {s.Course.Name}", (int)(CountBox.Value ?? 10), ExtraBox.Text ?? "", unit?.Title, lesson?.Title,
            [.. s.Course.Units.Select(u => u.Title)],
            picked is null ? null : Transfer.QuestionMarkdown(picked),
            mode == PromptMode.Soat && _exported != null ? StudyMarkdown.Write(_exported).Md : null));
        CopyPromptButton.IsEnabled = !needsQ || picked != null;
    }

    private void OnPromptChanged(object? sender, EventArgs e)
    {
        if (!_loading) UpdatePrompt();
    }

    private void OnCount(object? sender, NumericUpDownValueChangedEventArgs e) => UpdatePrompt();

    private void OnPickChanged(object? sender, SelectionChangedEventArgs e) => OnPromptChanged(sender, e);
    private void OnExtraText(object? sender, TextChangedEventArgs e) => OnPromptChanged(sender, e);

    private async void OnCopyPrompt(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) return;
        await clip.SetTextAsync(PromptText.Text ?? "");
        var was = CopyPromptButton.Content;
        CopyPromptButton.Content = L.T("practice.author.export.copied");
        await Task.Delay(2000);
        CopyPromptButton.Content = was;
    }

    // ------------------------------------------------------------------ bố cục

    // Hai cột khi đủ chỗ, không thì xem trước xuống dưới (như trang Hôm nay).
    private void Layout(double width)
    {
        var two = width >= (double)this.FindResource("MainMinWidth")! + 24 + (double)this.FindResource("SideMinWidth")!;
        if (two == (Grid.GetColumn(PreviewCard) == 2)) return;
        CreateColumns.ColumnDefinitions[0].MinWidth = two ? (double)this.FindResource("MainMinWidth")! : 0;
        CreateColumns.ColumnDefinitions[2].MinWidth = two ? (double)this.FindResource("SideMinWidth")! : 0;
        CreateColumns.ColumnDefinitions[0].Width = two ? new GridLength(2, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
        CreateColumns.ColumnDefinitions[2].Width = two ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        CreateColumns.RowDefinitions.Clear();
        if (!two)
        {
            CreateColumns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            CreateColumns.RowDefinitions.Add(new RowDefinition(24, GridUnitType.Pixel));
            CreateColumns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        Grid.SetColumn(PreviewCard, two ? 2 : 0);
        Grid.SetRow(PreviewCard, two ? 0 : 2);
    }

    /// <summary>Điền sẵn một câu mẫu (chụp kiểm tra).</summary>
    internal void Demo(string prompt, string[] options, int right, string solution)
    {
        PromptBox.Text = prompt;
        for (var i = 0; i < OptionRows.Children.Count && i < options.Length; i++)
        {
            var row = (Grid)OptionRows.Children[i];
            ((ToggleButton)row.Children[0]).IsChecked = i == right;
            ((TextBox)row.Children[1]).Text = options[i];
        }
        SolutionBox.Text = solution;
        Render();
    }

    /// <summary>Dán sẵn nội dung vào tab Nhập (chụp kiểm tra phần báo lỗi).</summary>
    internal void DemoRaw(string raw) => RawBox.Text = raw;

    /// <summary>Bấm Lưu câu (kiểm tra luồng lưu trên thư mục demo).</summary>
    internal void DemoSave() => OnSave(this, new RoutedEventArgs());

    private void OnBack(object? sender, RoutedEventArgs e) => BackRequested?.Invoke();
}

/// <summary>Chỗ mở lại Soạn sau khi lưu: môn (id), chương và bài theo tên (id bài có thể đổi sau khi nạp lại), tab, câu báo.</summary>
public sealed record AuthorPlace(string? CourseId, string? UnitTitle, string? LessonTitle, int Tab, string? Flash);
