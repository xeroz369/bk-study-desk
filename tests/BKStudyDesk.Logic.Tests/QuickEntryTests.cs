using System.Text.Json;
using SoHocTap.Data;

namespace SoHocTap.Tests;

/// <summary>Nhập nhanh "Nội dung - Ngày - giờ - địa điểm" (issue #22).</summary>
public class QuickEntryTests
{
    private static readonly DateTime Today = new(2026, 10, 3);

    private static QuickEntryResult P(string line) => QuickEntry.Parse(line, Today);

    [Fact]
    public void IssueExample_Makeup()
    {
        var r = P("Học bù Giải tích 2 - 12/10 - 7:00-8:50 - H1-201");
        Assert.True(r.Ok);
        Assert.Equal("Học bù Giải tích 2", r.Title);
        Assert.Equal(new DateTime(2026, 10, 12), r.Date);
        Assert.Equal(7 * 60, r.Start);
        Assert.Equal(8 * 60 + 50, r.End);
        Assert.Equal("H1-201", r.Location);              // "-" không có dấu cách không phải phân cách
        Assert.Equal(CustomEvents.KindMakeup, r.Kind);
    }

    [Fact]
    public void PlainEvent_IsEventKind()
    {
        var r = P("Họp nhóm BTL - 05/10 - 19h - Thư viện");
        Assert.True(r.Ok);
        Assert.Equal(CustomEvents.KindEvent, r.Kind);
        Assert.Equal(19 * 60, r.Start);
        Assert.Null(r.End);
        Assert.Equal("Thư viện", r.Location);
    }

    [Theory]
    [InlineData("hoc bu GT2 - 12/10 - 7h")]
    [InlineData("HỌC BÙ GT2 - 12/10 - 7h")]
    [InlineData("Lớp GT2 học bù - 12/10 - 7h")]
    public void Makeup_CaseAndDiacriticsVariants(string line) => Assert.Equal(CustomEvents.KindMakeup, P(line).Kind);

    [Fact]
    public void Makeup_DecomposedUnicode()
    {
        // Gõ bằng một số bộ gõ ra Unicode tổ hợp (NFD): vẫn nhận ra "học bù".
        var nfd = "Học bù GT2 - 12/10 - 7h".Normalize(System.Text.NormalizationForm.FormD);
        Assert.Equal(CustomEvents.KindMakeup, P(nfd).Kind);
    }

    [Theory]
    [InlineData("12/10", 2026, 10, 12)]
    [InlineData("3/10", 2026, 10, 3)]          // hôm nay vẫn tính là sắp tới
    [InlineData("02/10", 2027, 10, 2)]         // đã qua trong năm nay: sang năm
    [InlineData("1/1", 2027, 1, 1)]
    [InlineData("12/10/2026", 2026, 10, 12)]
    [InlineData("12/10/2025", 2025, 10, 12)]   // có năm thì giữ nguyên, kể cả đã qua
    [InlineData("2026-10-12", 2026, 10, 12)]
    [InlineData("2026-1-5", 2026, 1, 5)]
    [InlineData("ngày 12/10", 2026, 10, 12)]
    [InlineData("29/02", 2028, 2, 29)]         // năm nhuận gần nhất
    public void Dates(string text, int y, int m, int d) => Assert.Equal(new DateTime(y, m, d), QuickEntry.ParseDate(text, Today));

    [Theory]
    [InlineData("31/04")]
    [InlineData("32/10")]
    [InlineData("12/13")]
    [InlineData("2026-02-30")]
    [InlineData("12-10")]
    [InlineData("mai")]
    [InlineData("")]
    public void BadDates(string text) => Assert.Null(QuickEntry.ParseDate(text, Today));

    [Theory]
    [InlineData("7:00", 420, null)]
    [InlineData("7h", 420, null)]
    [InlineData("7g30", 450, null)]
    [InlineData("07:00-08:50", 420, 530)]
    [InlineData("7h-8h50", 420, 530)]
    [InlineData("13:00 - 15:00", 780, 900)]
    public void Times(string text, int start, int? end)
    {
        var r = P($"Việc - 12/10 - {text}");
        Assert.True(r.Ok, string.Join(",", r.Errors.Values));
        Assert.Equal(start, r.Start);
        Assert.Equal(end, r.End);
        Assert.Equal("", r.Location);
    }

    [Fact]
    public void SpacedRange_ThenLocation()
    {
        var r = P("Việc - 12/10 - 7:00 - 8:50 - H6-101");
        Assert.Equal(420, r.Start);
        Assert.Equal(530, r.End);
        Assert.Equal("H6-101", r.Location);
    }

    [Fact]
    public void TitleWithSeparator_FirstDateWins()
    {
        var r = P("Lab 2 - Kỹ thuật số - 14/10 - 13h - C6");
        Assert.True(r.Ok);
        Assert.Equal("Lab 2 - Kỹ thuật số", r.Title);
        Assert.Equal("C6", r.Location);
    }

    [Fact]
    public void EnDashSeparator()
    {
        var r = P("Việc – 12/10 – 7h – H1");
        Assert.True(r.Ok);
        Assert.Equal("H1", r.Location);
    }

    [Fact]
    public void Empty_AllMissing()
    {
        var r = P("   ");
        Assert.False(r.Ok);
        Assert.Equal(QuickEntry.ErrTitleMissing, r.Errors[EntryField.Title]);
        Assert.Equal(QuickEntry.ErrDateMissing, r.Errors[EntryField.Date]);
        Assert.Equal(QuickEntry.ErrStartMissing, r.Errors[EntryField.Start]);
        Assert.Equal(new HashSet<EntryField> { EntryField.Title, EntryField.Date, EntryField.Start }, r.Missing);
    }

    [Fact]
    public void TitleOnly_MissingNotBad()
    {
        var r = P("Họp nhóm");
        Assert.Equal("Họp nhóm", r.Title);
        Assert.Equal(new HashSet<EntryField> { EntryField.Date, EntryField.Start }, r.Missing);
        Assert.DoesNotContain(EntryField.Title, r.Errors.Keys);
    }

    [Fact]
    public void TypingInProgress_TrailingSeparator_IsMissing()
    {
        var r = P("Họp - 12/10 -");
        Assert.Equal(new DateTime(2026, 10, 12), r.Date);
        Assert.Equal(QuickEntry.ErrStartMissing, r.Errors[EntryField.Start]);
        Assert.Contains(EntryField.Start, r.Missing);
    }

    [Fact]
    public void BadDate_ReportedOnDate_RestStillParsed()
    {
        var r = P("Họp - 31/04 - 7h - H1");
        Assert.Equal(QuickEntry.ErrDateBad, r.Errors[EntryField.Date]);
        Assert.DoesNotContain(EntryField.Date, r.Missing);
        Assert.Null(r.Date);
        Assert.Equal(420, r.Start);
        Assert.Equal("H1", r.Location);
    }

    [Fact]
    public void DateSkipped_TimeGiven_DateMissing()
    {
        var r = P("Họp - 19:00 - H1");
        Assert.Equal(QuickEntry.ErrDateMissing, r.Errors[EntryField.Date]);
        Assert.Equal(19 * 60, r.Start);
        Assert.Equal("H1", r.Location);
    }

    [Theory]
    [InlineData("Việc - 12/10 - 25:00", EntryField.Start, QuickEntry.ErrStartBad)]
    [InlineData("Việc - 12/10 - sáng", EntryField.Start, QuickEntry.ErrStartBad)]
    [InlineData("Việc - 12/10 - 7:00-8:99", EntryField.End, QuickEntry.ErrEndBad)]
    [InlineData("Việc - 12/10 - 9:00-8:00", EntryField.End, QuickEntry.ErrEndBeforeStart)]
    [InlineData("Việc - 12/10 - 9:00-9:00", EntryField.End, QuickEntry.ErrEndBeforeStart)]
    [InlineData("Việc - 12/10", EntryField.Start, QuickEntry.ErrStartMissing)]
    [InlineData(" - 12/10 - 7h", EntryField.Title, QuickEntry.ErrTitleMissing)]
    public void FieldErrors(string line, EntryField field, string key)
    {
        var r = P(line);
        Assert.False(r.Ok);
        Assert.Equal(key, r.Errors[field]);
    }

    [Fact]
    public void Validate_Form()
    {
        Assert.Empty(QuickEntry.Validate("Họp", Today, "7:00", ""));
        Assert.Empty(QuickEntry.Validate("Họp", Today, "7h", "8h30"));
        var e = QuickEntry.Validate(" ", null, "", "abc");
        Assert.Equal(QuickEntry.ErrTitleMissing, e[EntryField.Title]);
        Assert.Equal(QuickEntry.ErrDateMissing, e[EntryField.Date]);
        Assert.Equal(QuickEntry.ErrStartMissing, e[EntryField.Start]);
        Assert.Equal(QuickEntry.ErrEndBad, e[EntryField.End]);
        Assert.Equal(QuickEntry.ErrEndBeforeStart, QuickEntry.Validate("Họp", Today, "9:00", "8:00")[EntryField.End]);
        Assert.Equal(QuickEntry.ErrStartBad, QuickEntry.Validate("Họp", Today, "9:75", "")[EntryField.Start]);
    }

    [Fact]
    public void Format_RoundTrips()
    {
        var ev = new CustomEvent { Id = "a", Title = "Học bù GT2", Date = "2026-10-12", Start = "07:00", End = "08:50", Location = "H1-201", Kind = CustomEvents.KindMakeup };
        var line = QuickEntry.Format(ev);
        Assert.Equal("Học bù GT2 - 12/10/2026 - 07:00-08:50 - H1-201", line);
        var r = P(line);
        Assert.True(r.Ok);
        Assert.Equal((ev.Title, ev.Day, ev.StartMin, 530, ev.Location, ev.Kind), (r.Title, r.Date, r.Start, r.End ?? 0, r.Location, r.Kind));
    }

    [Fact]
    public void Clock_Pads() => Assert.Equal(("07:05", "13:00"), (QuickEntry.Clock(425), QuickEntry.Clock(780)));

    [Fact]
    public void ErrorKeys_HaveVietnameseAndEnglishText()
    {
        foreach (var code in new[] { "vi", "en" })
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", code + ".json")))!;
            foreach (var k in QuickEntry.ErrorKeys) Assert.True(dict.TryGetValue(k, out var v) && v.Length > 0, $"{code}.json thiếu {k}");
        }
    }
}
