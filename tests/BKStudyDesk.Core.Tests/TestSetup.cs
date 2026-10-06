using System.Runtime.CompilerServices;

namespace BKStudyDesk.Core.Tests;

/// <summary>Như lúc app khởi động: nạp chữ giao diện (lang\ chép cạnh assembly test) trước mọi test.</summary>
internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Init() => SoHocTap.Ui.L.Load();
}
