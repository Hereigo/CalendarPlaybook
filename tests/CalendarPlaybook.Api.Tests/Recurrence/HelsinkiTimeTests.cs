using CalendarPlaybook.Api.Recurrence;

namespace CalendarPlaybook.Api.Tests.Recurrence;

// In 2027 Helsinki springs forward on 28 March (03:00 → 04:00) and falls back on 31 October (04:00 → 03:00).
public class HelsinkiTimeTests
{
    [Theory]
    [InlineData(2027, 3, 27, 8)]   // Winter time, UTC+2.
    [InlineData(2027, 3, 29, 7)]   // Summer time, UTC+3.
    [InlineData(2027, 10, 30, 7)]  // Summer time, UTC+3.
    [InlineData(2027, 11, 1, 8)]   // Winter time, UTC+2.
    public void TenOClockLocal_AroundDstChanges_ConvertsWithTheOffsetOfThatDay(int year, int month, int day, int expectedUtcHour)
    {
        var utc = HelsinkiTime.ToUtc(new DateOnly(year, month, day), new TimeOnly(10, 0));

        Assert.Equal(new DateTime(year, month, day, expectedUtcHour, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void TimeInSpringGap_MovesToNextValidLocalTime()
    {
        var utc = HelsinkiTime.ToUtc(new DateOnly(2027, 3, 28), new TimeOnly(3, 30));

        Assert.Equal(new DateTime(2027, 3, 28, 1, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal((new DateOnly(2027, 3, 28), new TimeOnly(4, 30)), HelsinkiTime.ToLocal(utc));
    }

    [Fact]
    public void TimeInAutumnRepeatedHour_UsesEarlierInstant()
    {
        var utc = HelsinkiTime.ToUtc(new DateOnly(2027, 10, 31), new TimeOnly(3, 30));

        Assert.Equal(new DateTime(2027, 10, 31, 0, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToLocal_TreatsUnspecifiedKindAsUtc()
    {
        // EF Core reads datetime2 back with an unspecified kind.
        var local = HelsinkiTime.ToLocal(new DateTime(2027, 6, 1, 7, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal((new DateOnly(2027, 6, 1), new TimeOnly(10, 0)), local);
    }

    [Fact]
    public void ToLocal_RoundTripsToUtc()
    {
        var date = new DateOnly(2027, 1, 15);
        var time = new TimeOnly(23, 45);

        Assert.Equal((date, time), HelsinkiTime.ToLocal(HelsinkiTime.ToUtc(date, time)));
    }
}
