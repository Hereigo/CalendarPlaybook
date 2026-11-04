using CalendarPlaybook.Api.Features.Events.ListOccurrences;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Tests.Features.Events.ListOccurrences;

public class ListOccurrencesExpandTests
{
    private static Event NewEvent(EventKind kind, string title = "Title") => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = "auth0|owner",
        Title = title,
        Kind = kind,
        TimeZone = "Europe/Helsinki",
    };

    private static Event Single(string title, DateTime startUtc, DateTime endUtc)
    {
        var entity = NewEvent(EventKind.Single, title);
        entity.StartUtc = startUtc;
        entity.EndUtc = endUtc;
        return entity;
    }

    private static Event AllDay(string title, DateOnly start, DateOnly end)
    {
        var entity = NewEvent(EventKind.AllDay, title);
        entity.StartDate = start;
        entity.EndDate = end;
        return entity;
    }

    private static Event Weekly(string title, DayOfWeek day, DateOnly start, DateOnly until, TimeOnly? startTime = null, TimeOnly? endTime = null)
    {
        var entity = NewEvent(EventKind.Recurring, title);
        entity.StartDate = start;
        entity.StartLocalTime = startTime;
        entity.EndLocalTime = endTime;
        entity.RuleType = RuleType.WeekDay;
        entity.DayOfWeek = day;
        entity.UntilDate = until;
        return entity;
    }

    private static readonly DateOnly June1 = new(2027, 6, 1);
    private static readonly DateOnly June30 = new(2027, 6, 30);

    [Fact]
    public void Single_InRange_ReturnsUtcInstantsMarkedUtc_AndLocalDates()
    {
        // Unspecified kind, as read from SQL Server. 21:30 UTC on June 1 is 00:30 local on June 2.
        var entity = Single("Late", new DateTime(2027, 6, 1, 20, 0, 0), new DateTime(2027, 6, 1, 21, 30, 0));

        var item = Assert.Single(ListOccurrencesHandler.Expand([entity], June1, June30));

        Assert.Equal(entity.Id, item.EventId);
        Assert.Equal(EventKind.Single, item.Kind);
        Assert.Equal(DateTimeKind.Utc, item.OccurrenceStartUtc!.Value.Kind);
        Assert.Equal(new DateTime(2027, 6, 1, 20, 0, 0, DateTimeKind.Utc), item.OccurrenceStartUtc);
        Assert.Equal(new DateOnly(2027, 6, 1), item.StartDate);
        Assert.Equal(new DateOnly(2027, 6, 2), item.EndDate);
    }

    [Fact]
    public void Single_UsesTheHelsinkiDayBoundary_NotTheUtcOne()
    {
        // 22:30-23:30 UTC on May 31 is 01:30-02:30 local on June 1: inside a June range.
        var inside = Single("Inside", new DateTime(2027, 5, 31, 22, 30, 0), new DateTime(2027, 5, 31, 23, 30, 0));
        // 20:00-20:59 UTC on June 30 is 23:00-23:59 local on June 30: still inside.
        var lastEvening = Single("Last evening", new DateTime(2027, 6, 30, 20, 0, 0), new DateTime(2027, 6, 30, 20, 59, 0));
        // 21:00 UTC on June 30 is 00:00 local on July 1: outside.
        var outside = Single("Outside", new DateTime(2027, 6, 30, 21, 0, 0), new DateTime(2027, 6, 30, 22, 0, 0));

        var titles = ListOccurrencesHandler.Expand([inside, lastEvening, outside], June1, June30).Select(i => i.Title);

        Assert.Equal(["Inside", "Last evening"], titles);
    }

    [Fact]
    public void Single_EndingExactlyAtMidnightOfFrom_IsNotInRange()
    {
        // Ends at 00:00 local on June 1, so it does not occupy June 1.
        var entity = Single("Ends at midnight", new DateTime(2027, 5, 31, 20, 0, 0), new DateTime(2027, 5, 31, 21, 0, 0));

        Assert.Empty(ListOccurrencesHandler.Expand([entity], June1, June30));
    }

    [Fact]
    public void AllDay_OverlappingTheRangeFromEitherSide_IsReturnedWithItsFullSpan()
    {
        var starts = AllDay("Starts before", new DateOnly(2027, 5, 28), new DateOnly(2027, 6, 2));
        var ends = AllDay("Ends after", new DateOnly(2027, 6, 29), new DateOnly(2027, 7, 3));
        var before = AllDay("Before", new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31));
        var after = AllDay("After", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 1));

        var items = ListOccurrencesHandler.Expand([after, ends, before, starts], June1, June30);

        Assert.Equal(["Starts before", "Ends after"], items.Select(i => i.Title));
        Assert.Equal(new DateOnly(2027, 5, 28), items[0].StartDate);
        Assert.Equal(new DateOnly(2027, 7, 3), items[1].EndDate);
        Assert.All(items, i =>
        {
            Assert.Null(i.OccurrenceStartUtc);
            Assert.Null(i.OccurrenceEndUtc);
        });
    }

    [Fact]
    public void RecurringWeekly_ExpandsEveryOccurrenceInRange_UntilDateInclusive()
    {
        // Mondays in June 2027: 7, 14, 21, 28. The series ends on Monday June 21 (inclusive).
        var entity = Weekly("Standup", DayOfWeek.Monday, new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 21), new TimeOnly(10, 0), new TimeOnly(10, 15));

        var items = ListOccurrencesHandler.Expand([entity], June1, June30);

        Assert.Equal(
            [new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 14), new DateOnly(2027, 6, 21)],
            items.Select(i => i.StartDate));
        Assert.All(items, i => Assert.Equal(i.StartDate, i.EndDate));
    }

    [Fact]
    public void RecurringTimed_KeepsWallClockTimeAcrossDstChange()
    {
        // 10:00 local every Saturday around the spring change on 2027-03-28: UTC+2 before it, UTC+3 after.
        var entity = Weekly("Saturday", DayOfWeek.Saturday, new DateOnly(2027, 3, 1), new DateOnly(2027, 4, 30), new TimeOnly(10, 0), new TimeOnly(11, 0));

        var items = ListOccurrencesHandler.Expand([entity], new DateOnly(2027, 3, 20), new DateOnly(2027, 4, 3));

        Assert.Equal(
            [new DateTime(2027, 3, 20, 8, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 27, 8, 0, 0, DateTimeKind.Utc), new DateTime(2027, 4, 3, 7, 0, 0, DateTimeKind.Utc)],
            items.Select(i => i.OccurrenceStartUtc!.Value));
        Assert.Equal(new DateTime(2027, 4, 3, 8, 0, 0, DateTimeKind.Utc), items[^1].OccurrenceEndUtc);
    }

    [Fact]
    public void RecurringTimed_StartInDstGap_MovesToTheNextValidTime()
    {
        // 03:30 does not exist on 2027-03-28 (Sunday); it becomes 04:30 local = 01:30 UTC.
        var entity = Weekly("Early", DayOfWeek.Sunday, new DateOnly(2027, 3, 21), new DateOnly(2027, 3, 28), new TimeOnly(3, 30), new TimeOnly(5, 0));

        var items = ListOccurrencesHandler.Expand([entity], new DateOnly(2027, 3, 28), new DateOnly(2027, 3, 28));

        Assert.Equal(new DateTime(2027, 3, 28, 1, 30, 0, DateTimeKind.Utc), Assert.Single(items).OccurrenceStartUtc);
    }

    [Fact]
    public void RecurringAllDay_HasDatesOnly()
    {
        var entity = Weekly("Trash day", DayOfWeek.Friday, new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 30));

        var items = ListOccurrencesHandler.Expand([entity], June1, June30);

        Assert.Equal(4, items.Count);
        Assert.All(items, i =>
        {
            Assert.Equal(EventKind.Recurring, i.Kind);
            Assert.Null(i.OccurrenceStartUtc);
            Assert.Null(i.OccurrenceEndUtc);
        });
    }

    [Fact]
    public void RecurringMonthDay31_ShiftsToTheLastDayOfShortMonths()
    {
        var entity = NewEvent(EventKind.Recurring, "Rent");
        entity.StartDate = new DateOnly(2027, 1, 1);
        entity.RuleType = RuleType.MonthDay;
        entity.DayOfMonth = 31;
        entity.UntilDate = new DateOnly(2027, 12, 31);

        var items = ListOccurrencesHandler.Expand([entity], new DateOnly(2027, 2, 1), new DateOnly(2027, 4, 30));

        Assert.Equal([new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31), new DateOnly(2027, 4, 30)], items.Select(i => i.StartDate));
    }

    [Fact]
    public void RecurringEveryDays_AndYearDay_UseTheirParameters()
    {
        var every = NewEvent(EventKind.Recurring, "Every 3 days");
        every.StartDate = new DateOnly(2027, 6, 1);
        every.RuleType = RuleType.EveryDays;
        every.Interval = 3;
        every.UntilDate = new DateOnly(2027, 6, 30);

        var yearly = NewEvent(EventKind.Recurring, "Leap day");
        yearly.StartDate = new DateOnly(2024, 1, 1);
        yearly.RuleType = RuleType.YearDay;
        yearly.Month = 2;
        yearly.DayOfMonth = 29;
        yearly.UntilDate = new DateOnly(2030, 12, 31);

        var items = ListOccurrencesHandler.Expand([every, yearly], June1, new DateOnly(2027, 6, 10));
        Assert.Equal([1, 4, 7, 10], items.Select(i => i.StartDate.Day));

        var feb = ListOccurrencesHandler.Expand([yearly], new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28));
        Assert.Equal(new DateOnly(2027, 2, 28), Assert.Single(feb).StartDate);
    }

    [Fact]
    public void SeriesOutsideTheRange_IsNotReturned()
    {
        var notYetStarted = Weekly("Later", DayOfWeek.Monday, new DateOnly(2027, 7, 5), new DateOnly(2027, 12, 31));
        var alreadyEnded = Weekly("Earlier", DayOfWeek.Monday, new DateOnly(2027, 1, 4), new DateOnly(2027, 5, 31));

        Assert.Empty(ListOccurrencesHandler.Expand([notYetStarted, alreadyEnded], June1, June30));
    }

    [Fact]
    public void Result_IsOrderedByDate_AllDayFirst_ThenStartTime_ThenTitle()
    {
        var day = new DateOnly(2027, 6, 10);
        var lateTimed = Single("Z late", new DateTime(2027, 6, 10, 12, 0, 0), new DateTime(2027, 6, 10, 13, 0, 0));
        var earlyTimed = Single("Y early", new DateTime(2027, 6, 10, 6, 0, 0), new DateTime(2027, 6, 10, 7, 0, 0));
        var allDayB = AllDay("B all-day", day, day);
        var allDayA = AllDay("A all-day", day, day);
        var nextDay = AllDay("Next day", day.AddDays(1), day.AddDays(1));

        var titles = ListOccurrencesHandler.Expand([nextDay, lateTimed, allDayB, earlyTimed, allDayA], June1, June30).Select(i => i.Title);

        Assert.Equal(["A all-day", "B all-day", "Y early", "Z late", "Next day"], titles);
    }
}
