namespace SoHocTap.Ui;

/// <summary>Route các trang để gọi <c>Go(...)</c>, lấy giá trị từ <see cref="PageRegistry"/>; route con ghép bằng "/".</summary>
internal static class Routes
{
    public const string Home = PageRegistry.Home;
    public const string Calendar = PageRegistry.Calendar;
    public const string CalendarExams = Calendar + "/thi";
    public const string CalendarWeek = Calendar + "/tuan";
    public const string Subjects = PageRegistry.Subjects;
    public const string Practice = PageRegistry.Practice;
    public const string Grades = PageRegistry.Grades;
    public const string Services = PageRegistry.Services;
    public const string Settings = PageRegistry.Settings;
    public const string About = PageRegistry.About;
}
