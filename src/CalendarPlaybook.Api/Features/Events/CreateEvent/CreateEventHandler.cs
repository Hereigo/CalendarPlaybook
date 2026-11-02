using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Recurrence;

namespace CalendarPlaybook.Api.Features.Events.CreateEvent;

public static class CreateEventHandler
{
    public static async Task<CreateEventResponse> HandleAsync(
        CalendarDbContext db,
        string ownerId,
        CreateEventRequest request,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request, ownerId, utcNow);

        db.Events.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Ids and kinds only: titles and descriptions never go to logs (spec section 11).
        logger.LogInformation("Created event {EventId} of kind {Kind}", entity.Id, entity.Kind);

        return ToResponse(entity);
    }

    /// <summary>
    /// Maps a validated request to a new entity. Single timed events are stored in UTC; all-day dates and
    /// recurring start dates and local times are stored as given (spec sections 6 and 10).
    /// </summary>
    public static Event ToEntity(CreateEventRequest request, string ownerId, DateTime utcNow)
    {
        // Truncated to milliseconds to match datetime2(3), so the response equals what is stored.
        var now = new DateTime(utcNow.Ticks - utcNow.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

        var entity = new Event
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = request.Title!,
            Description = request.Description,
            Kind = request.Kind!.Value,
            TimeZone = HelsinkiTime.ZoneId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        switch (entity.Kind)
        {
            case EventKind.Single:
                entity.StartUtc = HelsinkiTime.ToUtc(request.StartDate!.Value, request.StartTime!.Value);
                entity.EndUtc = HelsinkiTime.ToUtc(request.EndDate!.Value, request.EndTime!.Value);
                break;

            case EventKind.AllDay:
                entity.StartDate = request.StartDate;
                entity.EndDate = request.EndDate;
                break;

            case EventKind.Recurring:
                var recurrence = request.Recurrence!;
                entity.StartDate = request.StartDate;
                entity.StartLocalTime = request.StartTime;
                entity.EndLocalTime = request.EndTime;
                entity.RuleType = recurrence.RuleType;
                entity.Interval = recurrence.Interval;
                entity.DayOfMonth = recurrence.DayOfMonth;
                entity.Month = recurrence.Month;
                entity.DayOfWeek = recurrence.DayOfWeek;
                entity.UntilDate = recurrence.UntilDate;
                break;
        }

        return entity;
    }

    public static CreateEventResponse ToResponse(Event entity)
    {
        DateOnly? startDate = entity.StartDate;
        TimeOnly? startTime = entity.StartLocalTime;
        DateOnly? endDate = entity.EndDate;
        TimeOnly? endTime = entity.EndLocalTime;

        if (entity is { StartUtc: { } startUtc, EndUtc: { } endUtc })
        {
            (startDate, startTime) = HelsinkiTime.ToLocal(startUtc);
            (endDate, endTime) = HelsinkiTime.ToLocal(endUtc);
        }

        var recurrence = entity is { RuleType: { } ruleType, UntilDate: { } untilDate }
            ? new CreateEventRecurrenceResponse(ruleType, entity.Interval, entity.DayOfMonth, entity.Month, entity.DayOfWeek, untilDate)
            : null;

        return new CreateEventResponse(
            entity.Id,
            entity.Title,
            entity.Description,
            entity.Kind,
            startDate,
            startTime,
            endDate,
            endTime,
            entity.StartUtc,
            entity.EndUtc,
            entity.TimeZone,
            recurrence,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }
}
