using System.Text;

namespace SoHocTap.Core;

public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static bool HasContent(string path, string text)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return false;
            var bytes = Utf8.GetBytes(text);
            if (info.Length != bytes.Length) return false;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var old = new byte[bytes.Length];
            fs.ReadExactly(old);
            return old.AsSpan().SequenceEqual(bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public static bool WriteIfChanged(string path, string text, Action<Action>? retry = null)
    {
        if (HasContent(path, text)) return false;
        retry ??= a => a();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            retry(() => File.WriteAllText(tmp, text, Utf8));
            retry(() => File.Move(tmp, path, overwrite: true));
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return true;
    }
}
