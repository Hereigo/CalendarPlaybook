using CalendarPlaybook.Api.Features.Events.GetEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Tests.Features.Events.GetEvent;

public class GetEventMappingTests
{
    private static readonly DateTime Now = new(2027, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Event NewEvent(EventKind kind) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = "auth0|owner",
        Title = "Title",
        Kind = kind,
        TimeZone = "Europe/Helsinki",
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now,
    };

    [Fact]
    public void Single_ShowsLocalDatesAndTimesFromUtc()
    {
        var entity = NewEvent(EventKind.Single);
        entity.StartUtc = new DateTime(2027, 6, 1, 7, 0, 0, DateTimeKind.Utc);
        entity.EndUtc = new DateTime(2027, 6, 1, 8, 30, 0, DateTimeKind.Utc);

        var response = GetEventHandler.ToResponse(entity);

        Assert.Equal(new DateOnly(2027, 6, 1), response.StartDate);
        Assert.Equal(new TimeOnly(10, 0), response.StartTime);
        Assert.Equal(new DateOnly(2027, 6, 1), response.EndDate);
        Assert.Equal(new TimeOnly(11, 30), response.EndTime);
        Assert.Equal(entity.StartUtc, response.StartUtc);
        Assert.Null(response.Recurrence);
    }

    [Fact]
    public void AllDay_ReturnsLocalDatesWithoutTimes()
    {
        var entity = NewEvent(EventKind.AllDay);
        entity.StartDate = new DateOnly(2027, 3, 28);
        entity.EndDate = new DateOnly(2027, 3, 30);

        var response = GetEventHandler.ToResponse(entity);

        Assert.Equal(new DateOnly(2027, 3, 28), response.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 30), response.EndDate);
        Assert.Null(response.StartTime);
        Assert.Null(response.StartUtc);
        Assert.Null(response.Recurrence);
    }

    [Fact]
    public void Recurring_ReturnsLocalTimesAndRule()
    {
        var entity = NewEvent(EventKind.Recurring);
        entity.StartDate = new DateOnly(2027, 3, 1);
        entity.StartLocalTime = new TimeOnly(10, 0);
        entity.EndLocalTime = new TimeOnly(10, 15);
        entity.RuleType = RuleType.WeekDay;
        entity.DayOfWeek = DayOfWeek.Sunday;
        entity.UntilDate = new DateOnly(2027, 12, 31);

        var response = GetEventHandler.ToResponse(entity);

        Assert.Equal(new TimeOnly(10, 0), response.StartTime);
        Assert.Equal(new TimeOnly(10, 15), response.EndTime);
        Assert.Equal(
            new GetEventRecurrenceResponse(RuleType.WeekDay, null, null, null, DayOfWeek.Sunday, new DateOnly(2027, 12, 31)),
            response.Recurrence);
        Assert.Null(response.StartUtc);
    }
}
