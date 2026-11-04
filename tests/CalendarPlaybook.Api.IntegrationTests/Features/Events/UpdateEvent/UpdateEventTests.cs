using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.IntegrationTests.Infrastructure;

namespace CalendarPlaybook.Api.IntegrationTests.Features.Events.UpdateEvent;

/// <summary><c>PUT /api/events/{id}</c> through the real pipeline: JwtBearer, validation, owner filter, EF Core, SQL Server.</summary>
public class UpdateEventTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static readonly object WeeklySunday = new
    {
        title = "Weekly",
        kind = "Recurring",
        startDate = "2027-01-04",
        startTime = "09:00",
        endTime = "09:30",
        recurrence = new { ruleType = "WeekDay", dayOfWeek = "Sunday", untilDate = "2027-12-31" },
    };

    private static readonly object AllDayEvent = new { title = "Private", kind = "AllDay", startDate = "2027-03-28", endDate = "2027-03-28" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/events", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Recurring_ReplacesTheRuleOfTheWholeSeries_AndGetReturnsIt()
    {
        var sub = NewSub();
        var client = CreateClientFor(sub);
        var id = await CreateAsync(client, WeeklySunday);
        var before = Assert.Single(await EventsOwnedBy(sub));

        var response = await client.PutAsJsonAsync($"/api/events/{id}", new
        {
            title = "Weekly (moved)",
            description = "Now Fridays",
            kind = "Recurring",
            startDate = "2027-01-04",
            startTime = "14:00",
            endTime = "15:00",
            recurrence = new { ruleType = "WeekDay", dayOfWeek = "Friday", untilDate = "2028-06-30" },
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Weekly (moved)", body.GetProperty("title").GetString());
        Assert.Equal("Friday", body.GetProperty("recurrence").GetProperty("dayOfWeek").GetString());

        var got = await (await client.GetAsync($"/api/events/{id}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("14:00:00", got.GetProperty("startTime").GetString());
        Assert.Equal("2028-06-30", got.GetProperty("recurrence").GetProperty("untilDate").GetString());

        var row = Assert.Single(await EventsOwnedBy(sub));
        Assert.Equal(DayOfWeek.Friday, row.DayOfWeek);
        Assert.Equal(before.CreatedAtUtc, row.CreatedAtUtc);
        Assert.True(row.UpdatedAtUtc >= before.UpdatedAtUtc);
    }

    [Fact]
    public async Task KindChange_FromRecurringToSingle_StoresUtcAndClearsTheRule()
    {
        var sub = NewSub();
        var client = CreateClientFor(sub);
        var id = await CreateAsync(client, WeeklySunday);

        var response = await client.PutAsJsonAsync($"/api/events/{id}", new
        {
            title = "Dentist",
            kind = "Single",
            startDate = "2027-06-01",
            startTime = "10:00",
            endDate = "2027-06-01",
            endTime = "11:30",
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(await EventsOwnedBy(sub));
        Assert.Equal(EventKind.Single, row.Kind);
        Assert.Equal(new DateTime(2027, 6, 1, 7, 0, 0), row.StartUtc);
        Assert.Equal(new DateTime(2027, 6, 1, 8, 30, 0), row.EndUtc);
        Assert.Null(row.StartDate);
        Assert.Null(row.StartLocalTime);
        Assert.Null(row.RuleType);
        Assert.Null(row.DayOfWeek);
        Assert.Null(row.UntilDate);
    }

    public static TheoryData<string, object> InvalidRequests => new()
    {
        { "title", new { title = "", kind = "AllDay", startDate = "2027-03-28", endDate = "2027-03-28" } },
        { "endTime", new { title = "Late", kind = "Single", startDate = "2027-06-01", startTime = "11:00", endDate = "2027-06-01", endTime = "10:00" } },
        { "recurrence.untilDate", new { title = "Forever", kind = "Recurring", startDate = "2027-01-04", recurrence = new { ruleType = "EveryDays", interval = 1 } } },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequest_Returns400ProblemDetails_AndLeavesTheRowUnchanged(string field, object request)
    {
        var sub = NewSub();
        var client = CreateClientFor(sub);
        var id = await CreateAsync(client, WeeklySunday);

        var response = await client.PutAsJsonAsync($"/api/events/{id}", request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for '{field}': {problem}");
        Assert.Equal("Weekly", Assert.Single(await EventsOwnedBy(sub)).Title);
    }

    [Fact]
    public async Task NumericEnumValue_Returns400()
    {
        var client = CreateClientFor(NewSub());
        var id = await CreateAsync(client, AllDayEvent);

        var response = await client.PutAsJsonAsync($"/api/events/{id}", new { title = "Trip", kind = 1, startDate = "2027-03-28", endDate = "2027-03-28" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForeignId_Returns404_SameAsUnknownId_AndTheOwnersRowIsUntouched()
    {
        var owner = NewSub();
        var id = await CreateAsync(CreateClientFor(owner), AllDayEvent);
        var other = CreateClientFor(NewSub());
        var update = new { title = "Hijacked", kind = "AllDay", startDate = "2027-04-01", endDate = "2027-04-01" };

        var foreign = await other.PutAsJsonAsync($"/api/events/{id}", update, Ct);
        var unknown = await other.PutAsJsonAsync($"/api/events/{Guid.NewGuid()}", update, Ct);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var row = Assert.Single(await EventsOwnedBy(owner));
        Assert.Equal("Private", row.Title);
        Assert.Equal(new DateOnly(2027, 3, 28), row.StartDate);
    }

    [Fact]
    public async Task OwnerIdInBody_IsIgnored()
    {
        var owner = NewSub();
        var client = CreateClientFor(owner);
        var id = await CreateAsync(client, AllDayEvent);

        var response = await client.PutAsJsonAsync($"/api/events/{id}", new
        {
            title = "Mine",
            kind = "AllDay",
            startDate = "2027-04-01",
            endDate = "2027-04-01",
            ownerId = NewSub(),
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(owner, (await FindEvent(id))!.OwnerId);
    }

    public static TheoryData<string> RejectedTokens => new() { "none", "foreign key", "wrong audience", "wrong issuer", "expired", "no sub" };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task MissingOrInvalidToken_Returns401_AndLeavesTheRowUnchanged(string token)
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

        var response = await client.PutAsJsonAsync($"/api/events/{id}", new { title = "Changed", kind = "AllDay", startDate = "2027-04-01", endDate = "2027-04-01" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Private", Assert.Single(await EventsOwnedBy(sub)).Title);
    }
}
