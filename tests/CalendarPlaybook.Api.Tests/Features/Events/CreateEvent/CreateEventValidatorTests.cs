using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Tests.Features.Events.CreateEvent;

public class CreateEventValidatorTests
{
    private static readonly CreateEventRequest ValidSingle = new(
        "Dentist", null, EventKind.Single,
        new DateOnly(2027, 3, 1), new TimeOnly(10, 0), new DateOnly(2027, 3, 1), new TimeOnly(11, 0),
        null);

    private static readonly CreateEventRequest ValidAllDay = new(
        "Trip", null, EventKind.AllDay,
        new DateOnly(2027, 3, 1), null, new DateOnly(2027, 3, 3), null,
        null);

    private static readonly CreateEventRequest ValidRecurring = new(
        "Standup", null, EventKind.Recurring,
        new DateOnly(2027, 3, 1), new TimeOnly(10, 0), null, new TimeOnly(10, 15),
        new CreateEventRecurrence(RuleType.WeekDay, null, null, null, DayOfWeek.Monday, new DateOnly(2027, 12, 31)));

    private static CreateEventRequest WithRule(CreateEventRecurrence recurrence) => ValidRecurring with { Recurrence = recurrence };

    private static CreateEventRecurrence Rule(RuleType ruleType, int? interval = null, int? dayOfMonth = null, int? month = null, DayOfWeek? dayOfWeek = null) =>
        new(ruleType, interval, dayOfMonth, month, dayOfWeek, new DateOnly(2027, 12, 31));

    private static void AssertValid(CreateEventRequest request) =>
        Assert.Empty(CreateEventValidator.Validate(request));

    private static void AssertInvalid(CreateEventRequest request, string field) =>
        Assert.Contains(field, CreateEventValidator.Validate(request).Keys);

    [Fact]
    public void ValidRequests_PassForEveryKind()
    {
        AssertValid(ValidSingle);
        AssertValid(ValidAllDay);
        AssertValid(ValidRecurring);
        AssertValid(ValidRecurring with { StartTime = null, EndTime = null });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_Missing_IsRejected(string? title) =>
        AssertInvalid(ValidSingle with { Title = title }, "title");

    [Fact]
    public void Title_LengthLimit_Is200()
    {
        AssertValid(ValidSingle with { Title = new string('a', 200) });
        AssertInvalid(ValidSingle with { Title = new string('a', 201) }, "title");
    }

    [Fact]
    public void Description_LengthLimit_Is4000()
    {
        AssertValid(ValidSingle with { Description = new string('a', 4000) });
        AssertInvalid(ValidSingle with { Description = new string('a', 4001) }, "description");
    }

    [Fact]
    public void Kind_Missing_IsRejected() =>
        AssertInvalid(ValidSingle with { Kind = null }, "kind");

    [Fact]
    public void Kind_Undefined_IsRejected() =>
        AssertInvalid(ValidSingle with { Kind = (EventKind)99 }, "kind");

    [Theory]
    [InlineData(10, 0)]  // Equal to start.
    [InlineData(9, 0)]   // Before start.
    public void Single_EndNotAfterStart_IsRejected(int endHour, int endMinute) =>
        AssertInvalid(ValidSingle with { EndTime = new TimeOnly(endHour, endMinute) }, "endTime");

    [Fact]
    public void Single_EndOnLaterDay_IsValid() =>
        AssertValid(ValidSingle with { EndDate = new DateOnly(2027, 3, 2), EndTime = new TimeOnly(9, 0) });

    [Fact]
    public void Single_MissingFields_AreRejected()
    {
        var errors = CreateEventValidator.Validate(ValidSingle with { StartDate = null, StartTime = null, EndDate = null, EndTime = null });

        Assert.Equal(["endDate", "endTime", "startDate", "startTime"], errors.Keys.Order());
    }

    [Fact]
    public void Single_WithRecurrence_IsRejected() =>
        AssertInvalid(ValidSingle with { Recurrence = ValidRecurring.Recurrence }, "recurrence");

    [Fact]
    public void AllDay_SingleDay_IsValid() =>
        AssertValid(ValidAllDay with { EndDate = ValidAllDay.StartDate });

    [Fact]
    public void AllDay_EndBeforeStart_IsRejected() =>
        AssertInvalid(ValidAllDay with { EndDate = new DateOnly(2027, 2, 28) }, "endDate");

    [Fact]
    public void AllDay_WithTimes_IsRejected()
    {
        AssertInvalid(ValidAllDay with { StartTime = new TimeOnly(10, 0) }, "startTime");
        AssertInvalid(ValidAllDay with { EndTime = new TimeOnly(10, 0) }, "endTime");
    }

    [Fact]
    public void Recurring_MissingRecurrence_IsRejected() =>
        AssertInvalid(ValidRecurring with { Recurrence = null }, "recurrence");

    [Fact]
    public void Recurring_WithEndDate_IsRejected() =>
        AssertInvalid(ValidRecurring with { EndDate = new DateOnly(2027, 3, 2) }, "endDate");

    [Fact]
    public void Recurring_TimedEndTimeMissing_IsRejected() =>
        AssertInvalid(ValidRecurring with { EndTime = null }, "endTime");

    [Theory]
    [InlineData(10, 0)]  // Equal to start time.
    [InlineData(9, 0)]   // Before start time.
    public void Recurring_EndTimeNotAfterStartTime_IsRejected(int endHour, int endMinute) =>
        AssertInvalid(ValidRecurring with { EndTime = new TimeOnly(endHour, endMinute) }, "endTime");

    [Fact]
    public void Recurring_MissingUntilDate_IsRejected() =>
        AssertInvalid(WithRule(ValidRecurring.Recurrence! with { UntilDate = null }), "recurrence.untilDate");

    [Fact]
    public void Recurring_UntilDateBeforeStart_IsRejected() =>
        AssertInvalid(WithRule(ValidRecurring.Recurrence! with { UntilDate = new DateOnly(2027, 2, 28) }), "recurrence.untilDate");

    [Fact]
    public void Recurring_UntilDateEqualToStart_IsValid() =>
        AssertValid(WithRule(ValidRecurring.Recurrence! with { UntilDate = ValidRecurring.StartDate }));

    [Fact]
    public void Recurring_MissingRuleType_IsRejected() =>
        AssertInvalid(WithRule(ValidRecurring.Recurrence! with { RuleType = null, DayOfWeek = null }), "recurrence.ruleType");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EveryDays_IntervalBelowOne_IsRejected(int interval) =>
        AssertInvalid(WithRule(Rule(RuleType.EveryDays, interval: interval)), "recurrence.interval");

    [Fact]
    public void EveryDays_IntervalOne_IsValid() =>
        AssertValid(WithRule(Rule(RuleType.EveryDays, interval: 1)));

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void MonthDay_DayOutsideRange_IsRejected(int dayOfMonth) =>
        AssertInvalid(WithRule(Rule(RuleType.MonthDay, dayOfMonth: dayOfMonth)), "recurrence.dayOfMonth");

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void MonthDay_DayInRange_IsValid(int dayOfMonth) =>
        AssertValid(WithRule(Rule(RuleType.MonthDay, dayOfMonth: dayOfMonth)));

    [Theory]
    [InlineData(2, 30)]
    [InlineData(2, 31)]
    [InlineData(4, 31)]
    [InlineData(6, 31)]
    [InlineData(9, 31)]
    [InlineData(11, 31)]
    [InlineData(1, 0)]
    public void YearDay_DayThatNeverExists_IsRejected(int month, int dayOfMonth) =>
        AssertInvalid(WithRule(Rule(RuleType.YearDay, month: month, dayOfMonth: dayOfMonth)), "recurrence.dayOfMonth");

    [Fact]
    public void YearDay_Feb29_IsValid() =>
        AssertValid(WithRule(Rule(RuleType.YearDay, month: 2, dayOfMonth: 29)));

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void YearDay_MonthOutsideRange_IsRejected(int month) =>
        AssertInvalid(WithRule(Rule(RuleType.YearDay, month: month, dayOfMonth: 1)), "recurrence.month");

    [Fact]
    public void WeekDay_MissingDayOfWeek_IsRejected() =>
        AssertInvalid(WithRule(Rule(RuleType.WeekDay)), "recurrence.dayOfWeek");

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Saturday)]
    public void WeekDay_AnyDefinedDay_IsValid(DayOfWeek dayOfWeek) =>
        AssertValid(WithRule(Rule(RuleType.WeekDay, dayOfWeek: dayOfWeek)));

    [Fact]
    public void WeekDay_UndefinedDay_IsRejected() =>
        AssertInvalid(WithRule(Rule(RuleType.WeekDay, dayOfWeek: (DayOfWeek)7)), "recurrence.dayOfWeek");

    [Fact]
    public void Rule_ParametersOfAnotherRuleType_AreRejected()
    {
        AssertInvalid(WithRule(Rule(RuleType.EveryDays, interval: 2, dayOfMonth: 5)), "recurrence.dayOfMonth");
        AssertInvalid(WithRule(Rule(RuleType.MonthDay, dayOfMonth: 5, dayOfWeek: DayOfWeek.Monday)), "recurrence.dayOfWeek");
        AssertInvalid(WithRule(Rule(RuleType.WeekDay, dayOfWeek: DayOfWeek.Monday, interval: 2)), "recurrence.interval");
        AssertInvalid(WithRule(Rule(RuleType.YearDay, month: 3, dayOfMonth: 14, interval: 2)), "recurrence.interval");
    }
}
