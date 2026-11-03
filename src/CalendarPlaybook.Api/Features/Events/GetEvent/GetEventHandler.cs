using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Recurrence;
using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Features.Events.GetEvent;

public static class GetEventHandler
{
    /// <summary>Returns the owner's event, or <c>null</c> when the id is unknown or belongs to someone else (both map to 404).</summary>
    public static async Task<GetEventResponse?> HandleAsync(
        CalendarDbContext db,
        string ownerId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events
            .AsNoTracking()
            .Where(e => e.OwnerId == ownerId && e.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToResponse(entity);
    }

    public static GetEventResponse ToResponse(Event entity)
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
            ? new GetEventRecurrenceResponse(ruleType, entity.Interval, entity.DayOfMonth, entity.Month, entity.DayOfWeek, untilDate)
            : null;

        return new GetEventResponse(
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
