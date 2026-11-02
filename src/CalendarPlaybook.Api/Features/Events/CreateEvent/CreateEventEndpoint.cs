using System.Security.Claims;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CalendarPlaybook.Api.Features.Events.CreateEvent;

public static class CreateEventEndpoint
{
    /// <summary>Maps <c>POST /</c> on the events group: creates an event and returns <c>201</c> with its definition.</summary>
    public static RouteGroupBuilder MapCreateEvent(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync);
        return group;
    }

    private static async Task<Results<Created<CreateEventResponse>, ValidationProblem>> HandleAsync(
        CreateEventRequest request,
        ClaimsPrincipal user,
        CalendarDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var errors = CreateEventValidator.Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var response = await CreateEventHandler.HandleAsync(
            db,
            user.GetOwnerId(),
            request,
            DateTime.UtcNow,
            loggerFactory.CreateLogger(typeof(CreateEventHandler)),
            cancellationToken);

        return TypedResults.Created($"/api/events/{response.Id}", response);
    }
}
