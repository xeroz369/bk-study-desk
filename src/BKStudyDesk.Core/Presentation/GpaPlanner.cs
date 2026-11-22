using System.Globalization;
using SoHocTap.Ui;

namespace SoHocTap.Presentation;

public sealed record GpaCourse(string Code, string Name, double? Credits);

public static class GpaPlanner
{
    public static IReadOnlyList<GpaCourse> Pending(MybkData? m)
    {
        if (m is null) return [];
        var scored = m.Grades.Where(g => g.Score is not null && g.Term == m.Term.Code).Select(g => g.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var credits = (m.Curriculum?.Courses ?? []).Where(c => c.Credits is > 0).GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Credits, StringComparer.OrdinalIgnoreCase);
        foreach (var g in m.Grades.Where(g => g.Credits is > 0)) credits.TryAdd(g.Code, g.Credits);
        return [.. m.Schedule.Where(c => !string.IsNullOrEmpty(c.Code) && !scored.Contains(c.Code))
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .Select(c => new GpaCourse(c.Code, c.Name, credits.GetValueOrDefault(c.Code)))];
    }

    public static (double Gpa, double Credits)? Base(MybkData? m)
    {
        foreach (var t in (m?.GradeTerms ?? []).Where(t => t.Code != "BL"))
            if (double.TryParse(t.GpaAll, NumberStyles.Float, CultureInfo.InvariantCulture, out var gpa) && gpa > 0
                && double.TryParse(t.CreditsAll.Text(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cr) && cr > 0)
                return (gpa, cr);
        return null;
    }

    public static (double? Term, double? All) Compute((double Gpa, double Credits)? start, IEnumerable<(double? Credits, double? Score)> planned)
    {
        var used = planned.Where(p => p.Credits is > 0 && p.Score is >= 0 and <= 10).Select(p => (C: p.Credits!.Value, S: p.Score!.Value)).ToList();
        var credits = used.Sum(p => p.C);
        double? term = credits > 0 ? used.Sum(p => p.C * p.S) / credits : null;
        double? all = start is { } b ? (b.Gpa * b.Credits + used.Sum(p => p.C * p.S)) / (b.Credits + credits) : term;
        return (term is { } x ? Math.Round(x, 2) : null, all is { } y ? Math.Round(y, 2) : null);
    }
}
