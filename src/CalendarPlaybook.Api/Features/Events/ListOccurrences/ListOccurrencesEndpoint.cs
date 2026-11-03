using System.Security.Claims;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CalendarPlaybook.Api.Features.Events.ListOccurrences;

public static class ListOccurrencesEndpoint
{
    /// <summary>
    /// Maps <c>GET /?from=YYYY-MM-DD&amp;to=YYYY-MM-DD</c> on the events group: the occurrences in the local date range,
    /// both ends inclusive, at most 370 days.
    /// </summary>
    public static RouteGroupBuilder MapListOccurrences(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync);
        return group;
    }

    private static async Task<Results<Ok<List<ListOccurrencesItem>>, ValidationProblem>> HandleAsync(
        [FromQuery(Name = "from")] string? fromDate,
        [FromQuery(Name = "to")] string? toDate,
        ClaimsPrincipal user,
        CalendarDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var (query, errors) = ListOccurrencesQuery.Parse(fromDate, toDate);
        if (query is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var items = await ListOccurrencesHandler.HandleAsync(
            db,
            user.GetOwnerId(),
            query,
            loggerFactory.CreateLogger(typeof(ListOccurrencesHandler)),
            cancellationToken);

        return TypedResults.Ok(items);
    }
}
