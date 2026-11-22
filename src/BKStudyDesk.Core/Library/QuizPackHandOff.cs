using System.Text.Json.Nodes;
using SoHocTap.Api;

namespace SoHocTap.Library;

public enum HandOffKind
{
    Install,
    PracticeImport,
    Invalid,
}

public sealed record HandOff(HandOffKind Kind, JsonObject? Pack, string? Error);

public static class QuizPackHandOff
{
    public static HandOff Decide(string path, Func<string, bool> installed)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".md" or ".zip") return new(HandOffKind.PracticeImport, null, null);
        if (ext != ".json") return new(HandOffKind.Invalid, null, "không phải gói luyện tập (.json, .md, .zip)");
        string text;
        try
        {
            if (new FileInfo(path).Length > PackImport.MaxBytes) return new(HandOffKind.Invalid, null, "gói quá 20 MB");
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new(HandOffKind.Invalid, null, e.Message); }
        if (PackImport.Check(text, out var pack) is { } error) return new(HandOffKind.Invalid, null, error);
        return installed(pack!["id"]!.GetValue<string>()) ? new(HandOffKind.PracticeImport, pack, null) : new(HandOffKind.Install, pack, null);
    }
}
