using CalendarPlaybook.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Features.Events.DeleteEvent;

public static class DeleteEventHandler
{
    /// <summary>
    /// Deletes the owner's event. Returns <c>false</c> when the id is unknown or belongs to someone else (both map to 404).
    /// A recurring event is one row, so deleting it removes the whole series.
    /// </summary>
    public static async Task<bool> HandleAsync(
        CalendarDbContext db,
        string ownerId,
        Guid id,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events
            .Where(e => e.OwnerId == ownerId && e.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return false;
        }

        db.Events.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Ids and kinds only: titles and descriptions never go to logs (spec section 11).
        logger.LogInformation("Deleted event {EventId} of kind {Kind}", entity.Id, entity.Kind);

        return true;
    }
}
