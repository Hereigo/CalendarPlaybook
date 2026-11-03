using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Features.Events.GetEvent;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Features.Events.UpdateEvent;

public static class UpdateEventHandler
{
    /// <summary>
    /// Replaces the owner's event definition. Returns <c>null</c> when the id is unknown or belongs to someone else (both map to 404).
    /// A recurring event is one row, so the change applies to the whole series.
    /// </summary>
    public static async Task<GetEventResponse?> HandleAsync(
        CalendarDbContext db,
        string ownerId,
        Guid id,
        UpdateEventRequest request,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events
            .Where(e => e.OwnerId == ownerId && e.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        Apply(entity, request, utcNow);
        await db.SaveChangesAsync(cancellationToken);

        // Ids and kinds only: titles and descriptions never go to logs (spec section 11).
        logger.LogInformation("Updated event {EventId} of kind {Kind}", entity.Id, entity.Kind);

        return GetEventHandler.ToResponse(entity);
    }

    /// <summary>
    /// Overwrites every definition column, so columns of the previous kind are cleared. Id, owner and creation time are kept.
    /// </summary>
    public static void Apply(Event entity, UpdateEventRequest request, DateTime utcNow)
    {
        var fresh = CreateEventHandler.ToEntity(request.ToCreateRequest(), entity.OwnerId, utcNow);

        entity.Title = fresh.Title;
        entity.Description = fresh.Description;
        entity.Kind = fresh.Kind;
        entity.TimeZone = fresh.TimeZone;
        entity.StartUtc = fresh.StartUtc;
        entity.EndUtc = fresh.EndUtc;
        entity.StartDate = fresh.StartDate;
        entity.EndDate = fresh.EndDate;
        entity.StartLocalTime = fresh.StartLocalTime;
        entity.EndLocalTime = fresh.EndLocalTime;
        entity.RuleType = fresh.RuleType;
        entity.Interval = fresh.Interval;
        entity.DayOfMonth = fresh.DayOfMonth;
        entity.Month = fresh.Month;
        entity.DayOfWeek = fresh.DayOfWeek;
        entity.UntilDate = fresh.UntilDate;
        entity.UpdatedAtUtc = fresh.UpdatedAtUtc;
    }
}
