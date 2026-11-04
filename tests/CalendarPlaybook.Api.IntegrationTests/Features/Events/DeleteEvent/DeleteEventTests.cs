using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CalendarPlaybook.Api.IntegrationTests.Infrastructure;

namespace CalendarPlaybook.Api.IntegrationTests.Features.Events.DeleteEvent;

/// <summary><c>DELETE /api/events/{id}</c> through the real pipeline: JwtBearer, owner filter, EF Core, SQL Server.</summary>
public class DeleteEventTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static readonly object AllDayEvent = new { title = "Private", kind = "AllDay", startDate = "2027-03-28", endDate = "2027-03-28" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/events", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Single_Returns204_AndTheEventIsGone()
    {
        var sub = NewSub();
        var client = CreateClientFor(sub);
        var id = await CreateAsync(client, AllDayEvent);

        var response = await client.DeleteAsync($"/api/events/{id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/events/{id}", Ct)).StatusCode);
        Assert.Empty(await EventsOwnedBy(sub));
    }

    [Fact]
    public async Task Recurring_DeletesTheWholeSeries_AndLeavesOtherEventsAlone()
    {
        var sub = NewSub();
        var client = CreateClientFor(sub);
        var seriesId = await CreateAsync(client, new
        {
            title = "Weekly",
            kind = "Recurring",
            startDate = "2027-01-04",
            startTime = "09:00",
            endTime = "09:30",
            recurrence = new { ruleType = "WeekDay", dayOfWeek = "Sunday", untilDate = "2027-12-31" },
        });
        var otherId = await CreateAsync(client, AllDayEvent);

        var response = await client.DeleteAsync($"/api/events/{seriesId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(otherId, Assert.Single(await EventsOwnedBy(sub)).Id);
    }

    [Fact]
    public async Task DeletingTwice_Returns404TheSecondTime()
    {
        var client = CreateClientFor(NewSub());
        var id = await CreateAsync(client, AllDayEvent);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/events/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/events/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task ForeignId_Returns404_SameAsUnknownId_AndTheOwnersRowSurvives()
    {
        var owner = NewSub();
        var id = await CreateAsync(CreateClientFor(owner), AllDayEvent);
        var other = CreateClientFor(NewSub());

        var foreign = await other.DeleteAsync($"/api/events/{id}", Ct);
        var unknown = await other.DeleteAsync($"/api/events/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(id, Assert.Single(await EventsOwnedBy(owner)).Id);
    }

    [Fact]
    public async Task NonGuidId_Returns404()
    {
        var response = await CreateClientFor(NewSub()).DeleteAsync("/api/events/not-a-guid", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string> RejectedTokens => new() { "none", "foreign key", "wrong audience", "wrong issuer", "expired", "no sub" };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task MissingOrInvalidToken_Returns401_AndTheRowSurvives(string token)
    {
        var sub = NewSub();
        var id = await CreateAsync(CreateClientFor(sub), AllDayEvent);
        var client = CreateClient(token switch
        {
            "none" => null,
            "foreign key" => TestTokens.Create(sub, key: TestTokens.ForeignKey),
            "wrong audience" => TestTokens.Create(sub, audience: "https://another-api.test"),
            "wrong issuer" => TestTokens.Create(sub, issuer: "https://another-tenant.auth0.test/"),
            "expired" => TestTokens.Create(sub, expiresUtc: DateTime.UtcNow.AddMinutes(-10)),
            "no sub" => TestTokens.Create(sub: null),
            _ => throw new ArgumentOutOfRangeException(nameof(token)),
        });

        var response = await client.DeleteAsync($"/api/events/{id}", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(id, Assert.Single(await EventsOwnedBy(sub)).Id);
    }
}
