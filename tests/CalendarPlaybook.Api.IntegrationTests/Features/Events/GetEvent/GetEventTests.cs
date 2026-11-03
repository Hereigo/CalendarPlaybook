using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CalendarPlaybook.Api.IntegrationTests.Infrastructure;

namespace CalendarPlaybook.Api.IntegrationTests.Features.Events.GetEvent;

/// <summary><c>GET /api/events/{id}</c> through the real pipeline: JwtBearer, owner filter, EF Core, SQL Server.</summary>
public class GetEventTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/events", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Single_Returns200WithDefinition_InHelsinkiLocalTime()
    {
        var client = CreateClientFor(NewSub());
        var id = await CreateAsync(client, new
        {
            title = "Dentist",
            description = "Bring card",
            kind = "Single",
            startDate = "2027-06-01",
            startTime = "10:00",
            endDate = "2027-06-01",
            endTime = "11:30",
        });

        var response = await client.GetAsync($"/api/events/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Dentist", body.GetProperty("title").GetString());
        Assert.Equal("Bring card", body.GetProperty("description").GetString());
        Assert.Equal("Single", body.GetProperty("kind").GetString());
        Assert.Equal("2027-06-01", body.GetProperty("startDate").GetString());
        Assert.Equal("10:00:00", body.GetProperty("startTime").GetString());
        Assert.Equal("11:30:00", body.GetProperty("endTime").GetString());
        Assert.Equal("Europe/Helsinki", body.GetProperty("timeZone").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("recurrence").ValueKind);
    }

    [Fact]
    public async Task Recurring_ReturnsRule_WithStringEnumNames()
    {
        var client = CreateClientFor(NewSub());
        var id = await CreateAsync(client, new
        {
            title = "Weekly",
            kind = "Recurring",
            startDate = "2027-01-04",
            startTime = "09:00",
            endTime = "09:30",
            recurrence = new { ruleType = "WeekDay", dayOfWeek = "Sunday", untilDate = "2027-12-31" },
        });

        var response = await client.GetAsync($"/api/events/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recurrence = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("recurrence");
        Assert.Equal("WeekDay", recurrence.GetProperty("ruleType").GetString());
        Assert.Equal("Sunday", recurrence.GetProperty("dayOfWeek").GetString());
        Assert.Equal("2027-12-31", recurrence.GetProperty("untilDate").GetString());
    }

    [Fact]
    public async Task ForeignId_Returns404_SameAsUnknownId()
    {
        var owner = NewSub();
        var id = await CreateAsync(CreateClientFor(owner), new
        {
            title = "Private",
            kind = "AllDay",
            startDate = "2027-03-28",
            endDate = "2027-03-28",
        });
        var other = CreateClientFor(NewSub());

        var foreign = await other.GetAsync($"/api/events/{id}", Ct);
        var unknown = await other.GetAsync($"/api/events/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.False((await foreign.Content.ReadAsStringAsync(Ct)).Contains("Private"), "A foreign event must not leak into the 404 body.");
    }

    public static TheoryData<string> RejectedTokens => new() { "none", "foreign key", "wrong audience", "wrong issuer", "expired", "no sub" };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task MissingOrInvalidToken_Returns401(string token)
    {
        var sub = NewSub();
        var id = await CreateAsync(CreateClientFor(sub), new
        {
            title = "Private",
            kind = "AllDay",
            startDate = "2027-03-28",
            endDate = "2027-03-28",
        });
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

        var response = await client.GetAsync($"/api/events/{id}", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonGuidId_Returns404()
    {
        var response = await CreateClientFor(NewSub()).GetAsync("/api/events/not-a-guid", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
