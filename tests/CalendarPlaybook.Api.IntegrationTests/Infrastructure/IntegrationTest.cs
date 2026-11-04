using System.Net.Http.Headers;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarPlaybook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Base for API tests. Each test makes up its own <c>sub</c> values, so tests share the database without seeing each other's rows.
/// Every helper skips the test when Podman/Docker is not reachable.
/// </summary>
public abstract class IntegrationTest(SqlServerFixture fixture)
{
    protected static string NewSub() => Guid.NewGuid().ToString();

    /// <summary>A client that sends <paramref name="token"/> as bearer token, or no Authorization header when null.</summary>
    protected HttpClient CreateClient(string? token)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    protected HttpClient CreateClientFor(string sub) => CreateClient(TestTokens.Create(sub));

    /// <summary>Reads rows straight from SQL Server, bypassing the API.</summary>
    protected async Task<List<Event>> EventsOwnedBy(string ownerId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        return await db.Events.AsNoTracking().Where(e => e.OwnerId == ownerId).ToListAsync(TestContext.Current.CancellationToken);
    }

    protected async Task<Event?> FindEvent(Guid id)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        return await db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, TestContext.Current.CancellationToken);
    }
}
