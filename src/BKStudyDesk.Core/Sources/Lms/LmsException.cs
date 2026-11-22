namespace SoHocTap.Sources.Lms;

public sealed class LmsException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}
