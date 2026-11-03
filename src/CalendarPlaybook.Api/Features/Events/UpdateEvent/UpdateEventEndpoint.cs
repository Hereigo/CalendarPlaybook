using System.Security.Claims;
using CalendarPlaybook.Api.Features.Events.GetEvent;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarPlaybook.Api.Features.Events.UpdateEvent;

public static class UpdateEventEndpoint
{
    /// <summary>Maps <c>PUT /{id}</c> on the events group: replaces the definition (whole series) and returns <c>200</c>, or <c>404</c> for an unknown or foreign id.</summary>
    public static RouteGroupBuilder MapUpdateEvent(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", HandleAsync);
        return group;
    }

    private static async Task<Results<Ok<GetEventResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid id,
        UpdateEventRequest request,
        ClaimsPrincipal user,
        CalendarDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var errors = UpdateEventValidator.Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var response = await UpdateEventHandler.HandleAsync(
            db,
            user.GetOwnerId(),
            id,
            request,
            DateTime.UtcNow,
            loggerFactory.CreateLogger(typeof(UpdateEventHandler)),
            cancellationToken);

        return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
    }
}
