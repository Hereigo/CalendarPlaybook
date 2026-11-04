using CalendarPlaybook.Api.Features.Events.UpdateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Tests.Features.Events.UpdateEvent;

public class UpdateEventApplyTests
{
    private static readonly DateTime Created = new(2027, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new DateTime(2027, 2, 1, 8, 0, 0, 123, DateTimeKind.Utc).AddTicks(4567);

    private static Event RecurringEvent() => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = "owner-sub",
        Title = "Weekly",
        Description = "Old",
        Kind = EventKind.Recurring,
        TimeZone = "Europe/Helsinki",
        StartDate = new DateOnly(2027, 1, 4),
        StartLocalTime = new TimeOnly(9, 0),
        EndLocalTime = new TimeOnly(9, 30),
        RuleType = RuleType.WeekDay,
        DayOfWeek = DayOfWeek.Monday,
        UntilDate = new DateOnly(2027, 12, 31),
        CreatedAtUtc = Created,
        UpdatedAtUtc = Created,
    };

    [Fact]
    public void KindChange_ClearsColumnsOfThePreviousKind()
    {
        var entity = RecurringEvent();
        var request = new UpdateEventRequest(
            "Trip", null, EventKind.AllDay,
            new DateOnly(2027, 3, 28), null, new DateOnly(2027, 3, 30), null,
            null);

        UpdateEventHandler.Apply(entity, request, Now);

        Assert.Equal(EventKind.AllDay, entity.Kind);
        Assert.Equal("Trip", entity.Title);
        Assert.Null(entity.Description);
        Assert.Equal(new DateOnly(2027, 3, 30), entity.EndDate);
        Assert.Null(entity.StartLocalTime);
        Assert.Null(entity.EndLocalTime);
        Assert.Null(entity.RuleType);
        Assert.Null(entity.DayOfWeek);
        Assert.Null(entity.UntilDate);
    }

    [Fact]
    public void KeepsIdOwnerAndCreatedAt_AndTouchesUpdatedAtTruncatedToMilliseconds()
    {
        var entity = RecurringEvent();
        var id = entity.Id;
        var request = new UpdateEventRequest(
            "Daily", null, EventKind.Recurring,
            new DateOnly(2027, 1, 4), new TimeOnly(9, 0), null, new TimeOnly(9, 30),
            new UpdateEventRecurrence(RuleType.EveryDays, 2, null, null, null, new DateOnly(2027, 6, 30)));

        UpdateEventHandler.Apply(entity, request, Now);

        Assert.Equal(id, entity.Id);
        Assert.Equal("owner-sub", entity.OwnerId);
        Assert.Equal(Created, entity.CreatedAtUtc);
        Assert.Equal(new DateTime(2027, 2, 1, 8, 0, 0, 123, DateTimeKind.Utc), entity.UpdatedAtUtc);
        Assert.Equal(RuleType.EveryDays, entity.RuleType);
        Assert.Equal(2, entity.Interval);
        Assert.Null(entity.DayOfWeek);
    }

    [Fact]
    public void Single_StoresUtcFromHelsinkiLocalTime()
    {
        var entity = RecurringEvent();
        var request = new UpdateEventRequest(
            "Dentist", null, EventKind.Single,
            new DateOnly(2027, 6, 1), new TimeOnly(10, 0), new DateOnly(2027, 6, 1), new TimeOnly(11, 30),
            null);

        UpdateEventHandler.Apply(entity, request, Now);

        Assert.Equal(new DateTime(2027, 6, 1, 7, 0, 0, DateTimeKind.Utc), entity.StartUtc);
        Assert.Equal(new DateTime(2027, 6, 1, 8, 30, 0, DateTimeKind.Utc), entity.EndUtc);
        Assert.Null(entity.StartDate);
        Assert.Null(entity.UntilDate);
    }
}
