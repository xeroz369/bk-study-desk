using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using SoHocTap.Sources.Lms;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed class SectionRow(LmsSource.SectionInfo info, Func<IReadOnlySet<FileKind>> kinds) : INotifyPropertyChanged
{
    private bool _selected = info.Items.Any(f => !f.Have);

    public LmsSource.SectionInfo Info { get; } = info;
    public string Name => Info.Name;
    public IReadOnlyList<LmsSource.FileStat> Files => [.. Info.Of(kinds())];

    public string Meta
    {
        get
        {
            var files = Files;
            if (files.Count == 0) return L.T("download.noneOfKind");
            var have = files.Count(f => f.Have);
            return L.F("download.meta", files.Count, Format.Size(files.Sum(f => f.Bytes)),
                have == files.Count ? L.T("download.haveAll") : have > 0 ? L.F("download.haveSome", have) : L.T("download.notYet"));
        }
    }

    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }

    public void KindsChanged() => PropertyChanged?.Invoke(this, new(nameof(Meta)));

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string Summary(IReadOnlyList<SectionRow> rows)
    {
        if (rows.Count == 0) return "";
        var picked = rows.Where(r => r.Selected).ToList();
        var files = picked.SelectMany(r => r.Files).ToList();
        return L.F("download.summary", rows.Count, picked.Count, files.Count, Format.Size(files.Sum(f => f.Bytes)));
    }
}

public enum LoadState { Unread, Reading, Ready, Failed }

public sealed class CourseRow(LmsCourse course, Func<IReadOnlySet<FileKind>> kinds) : INotifyPropertyChanged
{
    private bool _wanted;
    private string _error = "";

    public LmsCourse Course { get; } = course;
    public string Name => Course.Part is null ? Course.Subject : Course.Subject + ", " + Course.Part;
    public LoadState State { get; private set; }
    public IReadOnlyList<SectionRow> Sections { get; private set; } = [];

    public bool Expanded { get; set; }

    private IEnumerable<SectionRow> Choosable => Sections.Where(s => s.Files.Count > 0);

    public bool Empty => State == LoadState.Ready && !Choosable.Any();

    public bool? Checked
    {
        get
        {
            if (State != LoadState.Ready) return _wanted;
            var rows = Choosable.ToList();
            var on = rows.Count(s => s.Selected);
            return on == 0 ? false : on == rows.Count ? true : null;
        }
        set
        {
            _wanted = value == true;
            if (State == LoadState.Ready) foreach (var s in Choosable) s.Selected = _wanted;
            Refresh();
        }
    }

    public IReadOnlyList<LmsSource.FileStat> Pending => [.. Sections.Where(s => s.Selected).SelectMany(s => s.Files).Where(f => !f.Have)];

    public IReadOnlyList<LmsSource.FileStat> Fresh => [.. Choosable.SelectMany(s => s.Files).Where(f => !f.Have)];

    public string Meta => State switch
    {
        LoadState.Unread => L.T("download.notRead"),
        LoadState.Reading => L.T("download.readingShort"),
        LoadState.Failed => L.F("download.readError", _error),
        _ when !Choosable.Any() => L.T("download.courseEmpty"),
        _ => Fresh is { Count: > 0 } fresh
            ? L.F("download.courseMeta", fresh.Count, Format.Size(fresh.Sum(f => f.Bytes)))
            : L.T("download.haveAll"),
    };

    public void Begin() { State = LoadState.Reading; Refresh(); }

    public Func<string, bool>? SectionFilter { get; set; }

    public void Loaded(IEnumerable<LmsSource.SectionInfo> sections)
    {
        Sections = [.. sections.Select(s => new SectionRow(s, kinds))];
        foreach (var s in Sections)
            if (!_wanted || SectionFilter?.Invoke(s.Name) == false) s.Selected = false;
        State = LoadState.Ready;
        Refresh();
    }

    public void Failed(string message) { _error = message; State = LoadState.Failed; Refresh(); }

    public void Refresh()
    {
        foreach (var s in Sections) s.KindsChanged();
        PropertyChanged?.Invoke(this, new(null));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TermRow(string term, bool current, IReadOnlyList<CourseRow> courses) : INotifyPropertyChanged
{
    public string Term { get; } = term;
    public bool Current { get; } = current;
    public IReadOnlyList<CourseRow> Courses { get; } = courses;
    public bool Expanded { get; set; }
    public string Header => L.F(Current ? "download.termCurrent" : "download.term", Term);

    public bool? Checked
    {
        get
        {
            var marks = Courses.Where(c => !c.Empty).Select(c => c.Checked).ToList();
            return marks.Count > 0 && marks.All(m => m == true) ? true : marks.All(m => m == false) ? false : null;
        }
        set { foreach (var c in Courses) c.Checked = value == true; Refresh(); }
    }

    public string Meta
    {
        get
        {
            var fresh = Courses.SelectMany(c => c.Fresh).ToList();
            var unread = Courses.Count(c => c.State != LoadState.Ready);
            var text = L.F("download.termMeta", Courses.Count, fresh.Count, Format.Size(fresh.Sum(f => f.Bytes)));
            return unread > 0 ? text + L.F("download.termUnread", unread) : text;
        }
    }

    public void Refresh() => PropertyChanged?.Invoke(this, new(null));

    public event PropertyChangedEventHandler? PropertyChanged;
}

public static partial class DownloadPlan
{
    private static readonly StringComparer ViOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), true);

    public enum Verdict { Nothing, Ok, Large, NoSpace }

    public static bool IsExamSection(string name) => ExamRx().IsMatch(Files.NameMatch.Fold(name));

    [GeneratedRegex(@"\b(de thi|de kiem tra|de cu|exams?)\b")]
    private static partial Regex ExamRx();

    public static IReadOnlyList<TermRow> Build(IEnumerable<LmsCourse> courses, string currentTerm, Func<IReadOnlySet<FileKind>> kinds) =>
        [.. courses.Where(c => c.Subject.Length > 0).GroupBy(c => c.Term)
            .OrderByDescending(g => g.Key == currentTerm).ThenByDescending(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TermRow(g.Key, g.Key == currentTerm,
                [.. g.OrderBy(c => c.Subject, ViOrder).ThenBy(c => c.Part).Select(c => new CourseRow(c, kinds))]))];

    public static Verdict Judge(int files, long bytes, long? free, long warnBytes, int warnFiles) =>
        files == 0 ? Verdict.Nothing
        : free is { } f && bytes > f ? Verdict.NoSpace
        : bytes >= warnBytes || files >= warnFiles ? Verdict.Large
        : Verdict.Ok;
}
