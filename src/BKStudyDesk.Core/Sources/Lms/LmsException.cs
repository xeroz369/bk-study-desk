namespace SoHocTap.Sources.Lms;

/// <summary>Moodle web service báo lỗi (JSON có "exception").</summary>
public sealed class LmsException(string message, string? code = null) : Exception(message)
{
    /// <summary>errorcode của Moodle (vd. "noreviewattempt"), null nếu lỗi không đến từ web service.</summary>
    public string? Code { get; } = code;
}
