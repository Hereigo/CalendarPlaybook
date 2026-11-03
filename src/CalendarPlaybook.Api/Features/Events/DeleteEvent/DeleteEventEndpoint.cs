using System.Security.Claims;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarPlaybook.Api.Features.Events.DeleteEvent;

public static class DeleteEventEndpoint
{
    /// <summary>Maps <c>DELETE /{id}</c> on the events group: deletes the event and its whole series and returns <c>204</c>, or <c>404</c> for an unknown or foreign id.</summary>
    public static RouteGroupBuilder MapDeleteEvent(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", HandleAsync);
        return group;
    }

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        CalendarDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var deleted = await DeleteEventHandler.HandleAsync(
            db,
            user.GetOwnerId(),
            id,
            loggerFactory.CreateLogger(typeof(DeleteEventHandler)),
            cancellationToken);

        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
