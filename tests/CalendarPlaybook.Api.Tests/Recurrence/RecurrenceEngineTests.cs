using CalendarPlaybook.Api.Recurrence;

namespace CalendarPlaybook.Api.Tests.Recurrence;

public class RecurrenceEngineTests
{
    [Fact]
    public void EveryDays_FromMarch1_Every3Days_UntilMarch31_MatchesAcceptanceCriterion4()
    {
        var occurrences = Expand(
            new RecurrenceRule.EveryDays(3),
            start: new DateOnly(2027, 3, 1),
            until: new DateOnly(2027, 3, 31));

        Assert.Equal(
            [1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31],
            occurrences.Select(d => d.Day));
    }

    [Fact]
    public void EveryDays_CrossesYearBoundary()
    {
        var occurrences = Expand(
            new RecurrenceRule.EveryDays(10),
            start: new DateOnly(2027, 12, 25),
            until: new DateOnly(2028, 1, 10));

        Assert.Equal(
            [new DateOnly(2027, 12, 25), new DateOnly(2028, 1, 4)],
            occurrences);
    }

    [Fact]
    public void EveryDays_RangeStartingLater_SkipsEarlierSteps()
    {
        var occurrences = Expand(
            new RecurrenceRule.EveryDays(7),
            start: new DateOnly(2027, 1, 1),
            until: new DateOnly(2027, 12, 31),
            from: new DateOnly(2027, 2, 1),
            to: new DateOnly(2027, 2, 28));

        Assert.Equal(
            [new DateOnly(2027, 2, 5), new DateOnly(2027, 2, 12), new DateOnly(2027, 2, 19), new DateOnly(2027, 2, 26)],
            occurrences);
    }

    [Fact]
    public void MonthDay31_ShiftsToLastDayInShorterMonths_MatchesAcceptanceCriterion5()
    {
        var occurrences = Expand(
            new RecurrenceRule.MonthDay(31),
            start: new DateOnly(2027, 1, 1),
            until: new DateOnly(2027, 6, 30));

        Assert.Equal(
            [
                new DateOnly(2027, 1, 31),
                new DateOnly(2027, 2, 28),
                new DateOnly(2027, 3, 31),
                new DateOnly(2027, 4, 30),
                new DateOnly(2027, 5, 31),
                new DateOnly(2027, 6, 30),
            ],
            occurrences);
    }

    [Fact]
    public void MonthDay31_InLeapFebruary_ShiftsToFeb29()
    {
        var occurrences = Expand(
            new RecurrenceRule.MonthDay(31),
            start: new DateOnly(2028, 2, 1),
            until: new DateOnly(2028, 2, 29));

        Assert.Equal([new DateOnly(2028, 2, 29)], occurrences);
    }

    [Fact]
    public void MonthDay_BeforeStartDay_IsNotGeneratedForStartMonth()
    {
        var occurrences = Expand(
            new RecurrenceRule.MonthDay(5),
            start: new DateOnly(2027, 3, 10),
            until: new DateOnly(2027, 5, 31));

        Assert.Equal(
            [new DateOnly(2027, 4, 5), new DateOnly(2027, 5, 5)],
            occurrences);
    }

    [Fact]
    public void WeekDay_FirstOccurrenceIsOnOrAfterStart()
    {
        // 2027-03-01 is a Monday. Start on Wednesday 2027-03-03, rule is Monday.
        var occurrences = Expand(
            new RecurrenceRule.WeekDay(DayOfWeek.Monday),
            start: new DateOnly(2027, 3, 3),
            until: new DateOnly(2027, 3, 22));

        Assert.Equal(
            [new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 15), new DateOnly(2027, 3, 22)],
            occurrences);
    }

    [Fact]
    public void WeekDay_StartOnRuleDay_IncludesStart()
    {
        var occurrences = Expand(
            new RecurrenceRule.WeekDay(DayOfWeek.Monday),
            start: new DateOnly(2027, 3, 1),
            until: new DateOnly(2027, 3, 15));

        Assert.Equal([new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 15)], occurrences);
    }

    [Fact]
    public void YearDay_Feb29_ShiftsToFeb28InNonLeapYears_MatchesAcceptanceCriterion6()
    {
        var occurrences = Expand(
            new RecurrenceRule.YearDay(month: 2, dayOfMonth: 29),
            start: new DateOnly(2027, 1, 1),
            until: new DateOnly(2029, 12, 31));

        Assert.Equal(
            [new DateOnly(2027, 2, 28), new DateOnly(2028, 2, 29), new DateOnly(2029, 2, 28)],
            occurrences);
    }

    [Fact]
    public void YearDay_SameMonthAndDayEveryYear()
    {
        var occurrences = Expand(
            new RecurrenceRule.YearDay(month: 3, dayOfMonth: 14),
            start: new DateOnly(2027, 1, 1),
            until: new DateOnly(2028, 12, 31));

        Assert.Equal([new DateOnly(2027, 3, 14), new DateOnly(2028, 3, 14)], occurrences);
    }

    [Fact]
    public void UntilDate_IsInclusive()
    {
        var occurrences = Expand(
            new RecurrenceRule.EveryDays(1),
            start: new DateOnly(2027, 5, 1),
            until: new DateOnly(2027, 5, 3));

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(new DateOnly(2027, 5, 3), occurrences[^1]);
    }

    [Fact]
    public void UntilBeforeStart_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            RecurrenceEngine.Occurrences(
                new RecurrenceRule.EveryDays(1),
                start: new DateOnly(2027, 5, 10),
                until: new DateOnly(2027, 5, 9),
                from: new DateOnly(2027, 5, 1),
                to: new DateOnly(2027, 5, 31)).ToList());
    }

    [Fact]
    public void EveryDays_ZeroOrNegativeInterval_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecurrenceRule.EveryDays(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecurrenceRule.EveryDays(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void MonthDay_DayOutsideOneToThirtyOne_IsRejected(int day)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecurrenceRule.MonthDay(day));
    }

    private static List<DateOnly> Expand(RecurrenceRule rule, DateOnly start, DateOnly until, DateOnly? from = null, DateOnly? to = null)
    {
        // Default window covers the whole series so the tests read as "the full series".
        return RecurrenceEngine
            .Occurrences(rule, start, until, from ?? start, to ?? until)
            .ToList();
    }
}
