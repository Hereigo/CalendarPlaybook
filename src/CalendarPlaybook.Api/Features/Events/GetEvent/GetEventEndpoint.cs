using System.Security.Claims;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarPlaybook.Api.Features.Events.GetEvent;

public static class GetEventEndpoint
{
    /// <summary>Maps <c>GET /{id}</c> on the events group: returns the event definition, or <c>404</c> for an unknown or foreign id.</summary>
    public static RouteGroupBuilder MapGetEvent(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync);
        return group;
    }

    private static async Task<Results<Ok<GetEventResponse>, NotFound>> HandleAsync(
        Guid id,
        ClaimsPrincipal user,
        CalendarDbContext db,
        CancellationToken cancellationToken)
    {
        var response = await GetEventHandler.HandleAsync(db, user.GetOwnerId(), id, cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }
}
