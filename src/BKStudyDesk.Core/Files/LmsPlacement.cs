namespace SoHocTap.Files;

public enum PlacementKind { Duplicate, ReplaceOwn, New }

public sealed record Placement(PlacementKind Kind, string Target);

public static class LmsPlacement
{
    public static bool IsAppOwned(string? path, string subjectsRoot, string lmsSubfolder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(lmsSubfolder)) return false;
        string rel;
        try { rel = Path.GetRelativePath(Path.GetFullPath(subjectsRoot), Path.GetFullPath(path)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        if (rel == "." || rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return false;
        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 3 && parts[1..^1].Any(p => NameMatch.Same(p, lmsSubfolder));
    }

    public static Placement Decide(string? knownPath, string? sameContent, string dest, string subjectsRoot, string lmsSubfolder, Func<string, bool> fileExists)
    {
        if (sameContent is not null && fileExists(sameContent)) return new(PlacementKind.Duplicate, sameContent);
        if (knownPath is not null && fileExists(knownPath) && IsAppOwned(knownPath, subjectsRoot, lmsSubfolder)) return new(PlacementKind.ReplaceOwn, knownPath);
        return new(PlacementKind.New, dest);
    }

    public static string? Apply(string tmp, Placement p, Func<string, string> archiveFor)
    {
        switch (p.Kind)
        {
            case PlacementKind.Duplicate:
                File.Delete(tmp);
                return null;
            case PlacementKind.ReplaceOwn:
                FileOps.MoveFile(p.Target, FileOps.UniquePath(archiveFor(p.Target)));
                FileOps.MoveFile(tmp, p.Target);
                return p.Target;
            default:
                var dest = FileOps.UniquePath(p.Target);
                FileOps.MoveFile(tmp, dest);
                return dest;
        }
    }

    public static (string? Path, string? DedupOf) IndexFields(Placement p, string? knownPath, string? final, string subjectsRoot, string lmsSubfolder)
    {
        if (p.Kind != PlacementKind.Duplicate) return (final, null);
        var own = knownPath is not null && string.Equals(Path.GetFullPath(knownPath), Path.GetFullPath(p.Target), StringComparison.OrdinalIgnoreCase)
                  && IsAppOwned(p.Target, subjectsRoot, lmsSubfolder);
        return own ? (p.Target, null) : (null, p.Target);
    }
}
