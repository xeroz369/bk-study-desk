using SoHocTap.Data;

namespace SoHocTap.Tests;

public class RegistrationsTests
{
    private const long Now = 1_800_000_000, D = 86400;
    private static MybkRegistration R(string code, long start, long end) => new(code, "Đợt " + code, start, end);

    [Fact]
    public void Upcoming_OpenFirstThenSoonest_DropsClosed()
    {
        var list = Registrations.Upcoming([R("later", Now + 20 * D, Now + 25 * D), R("closed", Now - 5 * D, Now - D),
            R("soon", Now + 3 * D, Now + 6 * D), R("open", Now - D, Now + 2 * D)], Now);
        Assert.Equal(["open", "soon", "later"], list.Select(r => r.Code));
    }

    [Fact]
    public void IsOpen_StartInclusiveEndExclusive()
    {
        Assert.True(Registrations.IsOpen(R("a", Now, Now + D), Now));
        Assert.False(Registrations.IsOpen(R("a", Now - D, Now), Now));
    }
}
