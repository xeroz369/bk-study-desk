using System.Runtime.CompilerServices;

namespace BKStudyDesk.Core.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init() => SoHocTap.Ui.L.Load();
}
