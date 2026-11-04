using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Tests.Features.Events.CreateEvent;

public class CreateEventMappingTests
{
    private const string OwnerId = "auth0|owner";

    private static readonly DateTime Now = new DateTime(2027, 1, 10, 12, 0, 0, 123, DateTimeKind.Utc).AddTicks(4567);

    [Fact]
    public void Single_StoresUtcAndTimeZone_AndNoLocalColumns()
    {
        var request = new CreateEventRequest(
            "Dentist", "Bring card", EventKind.Single,
            new DateOnly(2027, 6, 1), new TimeOnly(10, 0), new DateOnly(2027, 6, 1), new TimeOnly(11, 30),
            null);

        var entity = CreateEventHandler.ToEntity(request, OwnerId, Now);

        Assert.Equal(OwnerId, entity.OwnerId);
        Assert.Equal("Dentist", entity.Title);
        Assert.Equal("Bring card", entity.Description);
        Assert.Equal("Europe/Helsinki", entity.TimeZone);
        Assert.Equal(new DateTime(2027, 6, 1, 7, 0, 0, DateTimeKind.Utc), entity.StartUtc);
        Assert.Equal(new DateTime(2027, 6, 1, 8, 30, 0, DateTimeKind.Utc), entity.EndUtc);
        Assert.Null(entity.StartDate);
        Assert.Null(entity.EndDate);
        Assert.Null(entity.StartLocalTime);
        Assert.Null(entity.RuleType);
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void AllDay_StoresLocalDatesOnly()
    {
        var request = new CreateEventRequest(
            "Trip", null, EventKind.AllDay,
            new DateOnly(2027, 3, 28), null, new DateOnly(2027, 3, 30), null,
            null);

        var entity = CreateEventHandler.ToEntity(request, OwnerId, Now);

        Assert.Equal(new DateOnly(2027, 3, 28), entity.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 30), entity.EndDate);
        Assert.Null(entity.StartUtc);
        Assert.Null(entity.EndUtc);
        Assert.Null(entity.UntilDate);
    }

    [Fact]
    public void RecurringTimed_StoresLocalTimesAndRule_WithoutUtcColumns()
    {
        var request = new CreateEventRequest(
            "Standup", null, EventKind.Recurring,
            new DateOnly(2027, 3, 1), new TimeOnly(10, 0), null, new TimeOnly(10, 15),
            new CreateEventRecurrence(RuleType.WeekDay, null, null, null, DayOfWeek.Monday, new DateOnly(2027, 12, 31)));

        var entity = CreateEventHandler.ToEntity(request, OwnerId, Now);

        Assert.Equal(new DateOnly(2027, 3, 1), entity.StartDate);
        Assert.Equal(new TimeOnly(10, 0), entity.StartLocalTime);
        Assert.Equal(new TimeOnly(10, 15), entity.EndLocalTime);
        Assert.Equal(RuleType.WeekDay, entity.RuleType);
        Assert.Equal(DayOfWeek.Monday, entity.DayOfWeek);
        Assert.Equal(new DateOnly(2027, 12, 31), entity.UntilDate);
        Assert.Null(entity.StartUtc);
        Assert.Null(entity.EndUtc);
        Assert.Null(entity.EndDate);
    }

    [Fact]
    public void Timestamps_AreTruncatedToMilliseconds()
    {
        var request = new CreateEventRequest(
            "Trip", null, EventKind.AllDay,
            new DateOnly(2027, 3, 28), null, new DateOnly(2027, 3, 28), null,
            null);

        var entity = CreateEventHandler.ToEntity(request, OwnerId, Now);

        Assert.Equal(new DateTime(2027, 1, 10, 12, 0, 0, 123, DateTimeKind.Utc), entity.CreatedAtUtc);
        Assert.Equal(entity.CreatedAtUtc, entity.UpdatedAtUtc);
    }

    [Fact]
    public void Response_ForSingle_ShowsLocalTimesFromUtc()
    {
        // 03:30 on the spring-forward day does not exist; the stored and returned start is 04:30.
        var request = new CreateEventRequest(
            "Early", null, EventKind.Single,
            new DateOnly(2027, 3, 28), new TimeOnly(3, 30), new DateOnly(2027, 3, 28), new TimeOnly(5, 0),
            null);

        var response = CreateEventHandler.ToResponse(CreateEventHandler.ToEntity(request, OwnerId, Now));

        Assert.Equal(new DateOnly(2027, 3, 28), response.StartDate);
        Assert.Equal(new TimeOnly(4, 30), response.StartTime);
        Assert.Equal(new TimeOnly(5, 0), response.EndTime);
        Assert.Null(response.Recurrence);
    }

    [Fact]
    public void Response_ForRecurring_IncludesRule()
    {
        var request = new CreateEventRequest(
            "Rent", null, EventKind.Recurring,
            new DateOnly(2027, 1, 1), null, null, null,
            new CreateEventRecurrence(RuleType.MonthDay, null, 31, null, null, new DateOnly(2027, 6, 30)));

        var response = CreateEventHandler.ToResponse(CreateEventHandler.ToEntity(request, OwnerId, Now));

        Assert.Equal(new CreateEventRecurrenceResponse(RuleType.MonthDay, null, 31, null, null, new DateOnly(2027, 6, 30)), response.Recurrence);
        Assert.Null(response.StartTime);
        Assert.Null(response.StartUtc);
    }
}
