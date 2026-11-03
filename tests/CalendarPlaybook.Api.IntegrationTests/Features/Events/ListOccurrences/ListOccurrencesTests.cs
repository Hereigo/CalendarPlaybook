using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CalendarPlaybook.Api.IntegrationTests.Infrastructure;

namespace CalendarPlaybook.Api.IntegrationTests.Features.Events.ListOccurrences;

/// <summary><c>GET /api/events?from&amp;to</c> through the real pipeline: JwtBearer, validation, owner filter, EF Core, SQL Server, recurrence engine.</summary>
public class ListOccurrencesTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/events", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListAsync(HttpClient client, string from, string to)
    {
        var response = await client.GetAsync($"/api/events?from={from}&to={to}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static object Weekly(string title, string dayOfWeek, string untilDate) => new
    {
        title,
        kind = "Recurring",
        startDate = "2027-01-04",
        startTime = "09:00",
        endTime = "09:30",
        recurrence = new { ruleType = "WeekDay", dayOfWeek, untilDate },
    };

    [Fact]
    public async Task ReturnsSingleAllDayAndRecurringOccurrences_InOrder_WithUtcAndLocalDates()
    {
        var client = CreateClientFor(NewSub());
        var singleId = await CreateAsync(client, new { title = "Dentist", kind = "Single", startDate = "2027-06-03", startTime = "10:00", endDate = "2027-06-03", endTime = "11:30" });
        var allDayId = await CreateAsync(client, new { title = "Trip", kind = "AllDay", startDate = "2027-06-02", endDate = "2027-06-04" });
        var weeklyId = await CreateAsync(client, Weekly("Standup", "Sunday", "2027-06-13"));

        var items = await ListAsync(client, "2027-06-01", "2027-06-30");

        // Sundays in June up to the until date: June 6 and 13. Then the all-day trip, then the dentist.
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(
            ["Trip", "Dentist", "Standup", "Standup"],
            items.EnumerateArray().Select(i => i.GetProperty("title").GetString()));

        var trip = items[0];
        Assert.Equal(allDayId, trip.GetProperty("eventId").GetGuid());
        Assert.Equal("AllDay", trip.GetProperty("kind").GetString());
        Assert.Equal("2027-06-02", trip.GetProperty("startDate").GetString());
        Assert.Equal("2027-06-04", trip.GetProperty("endDate").GetString());
        Assert.Equal(JsonValueKind.Null, trip.GetProperty("occurrenceStartUtc").ValueKind);

        var dentist = items[1];
        Assert.Equal(singleId, dentist.GetProperty("eventId").GetGuid());
        // 10:00 and 11:30 in Helsinki summer time (UTC+3), serialized as UTC with a Z.
        Assert.Equal("2027-06-03T07:00:00Z", dentist.GetProperty("occurrenceStartUtc").GetString());
        Assert.Equal("2027-06-03T08:30:00Z", dentist.GetProperty("occurrenceEndUtc").GetString());
        Assert.Equal("2027-06-03", dentist.GetProperty("startDate").GetString());

        Assert.Equal(weeklyId, items[2].GetProperty("eventId").GetGuid());
        Assert.Equal("2027-06-06", items[2].GetProperty("startDate").GetString());
        Assert.Equal("2027-06-06T06:00:00Z", items[2].GetProperty("occurrenceStartUtc").GetString());
        Assert.Equal("2027-06-13", items[3].GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task RangeEnds_AreInclusive_AndEventsOutsideAreLeftOut()
    {
        var client = CreateClientFor(NewSub());
        await CreateAsync(client, new { title = "First day", kind = "AllDay", startDate = "2027-06-01", endDate = "2027-06-01" });
        await CreateAsync(client, new { title = "Last day", kind = "AllDay", startDate = "2027-06-30", endDate = "2027-06-30" });
        await CreateAsync(client, new { title = "Day before", kind = "AllDay", startDate = "2027-05-31", endDate = "2027-05-31" });
        await CreateAsync(client, new { title = "Day after", kind = "AllDay", startDate = "2027-07-01", endDate = "2027-07-01" });

        var items = await ListAsync(client, "2027-06-01", "2027-06-30");

        Assert.Equal(["First day", "Last day"], items.EnumerateArray().Select(i => i.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task EditedSeries_ShowsTheNewRuleInTheListing()
    {
        var client = CreateClientFor(NewSub());
        var id = await CreateAsync(client, Weekly("Weekly", "Sunday", "2027-12-31"));
        var before = await ListAsync(client, "2027-06-01", "2027-06-30");
        Assert.Equal(4, before.GetArrayLength());

        var update = await client.PutAsJsonAsync($"/api/events/{id}", Weekly("Weekly", "Friday", "2027-06-15"), Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await ListAsync(client, "2027-06-01", "2027-06-30");
        Assert.Equal(["2027-06-04", "2027-06-11"], after.EnumerateArray().Select(i => i.GetProperty("startDate").GetString()));
    }

    [Fact]
    public async Task DeletedSeries_DisappearsFromTheListing()
    {
        var client = CreateClientFor(NewSub());
        var id = await CreateAsync(client, Weekly("Weekly", "Sunday", "2027-12-31"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/events/{id}", Ct)).StatusCode);

        Assert.Equal(0, (await ListAsync(client, "2027-06-01", "2027-06-30")).GetArrayLength());
    }

    [Fact]
    public async Task OwnerIsolation_OnlyTheCallersEventsAreListed()
    {
        var a = CreateClientFor(NewSub());
        var b = CreateClientFor(NewSub());
        await CreateAsync(a, new { title = "A secret", kind = "AllDay", startDate = "2027-06-10", endDate = "2027-06-10" });
        await CreateAsync(a, Weekly("A weekly", "Sunday", "2027-12-31"));
        await CreateAsync(b, new { title = "B event", kind = "AllDay", startDate = "2027-06-10", endDate = "2027-06-10" });

        var items = await ListAsync(b, "2027-06-01", "2027-06-30");

        Assert.Equal(["B event"], items.EnumerateArray().Select(i => i.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Range_Of370Days_IsAccepted_And371Returns400()
    {
        var client = CreateClientFor(NewSub());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/events?from=2027-01-01&to=2028-01-05", Ct)).StatusCode);

        var tooLong = await client.GetAsync("/api/events?from=2027-01-01&to=2028-01-06", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("application/problem+json", tooLong.Content.Headers.ContentType?.MediaType);
        var problem = await tooLong.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("to", out _), $"Expected an error for 'to': {problem}");
    }

    public static TheoryData<string, string> InvalidQueries => new()
    {
        { "", "from" },
        { "?from=2027-06-01", "to" },
        { "?to=2027-06-30", "from" },
        { "?from=01.06.2027&to=2027-06-30", "from" },
        { "?from=2027-06-01&to=2027-02-30", "to" },
        { "?from=2027-06-30&to=2027-06-01", "to" },
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task InvalidQuery_Returns400ProblemDetails(string queryString, string field)
    {
        var response = await CreateClientFor(NewSub()).GetAsync($"/api/events{queryString}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for '{field}': {problem}");
    }

    public static TheoryData<string> RejectedTokens => new() { "none", "foreign key", "wrong audience", "wrong issuer", "expired", "no sub" };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task MissingOrInvalidToken_Returns401(string token)
    {
        var sub = NewSub();
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

        var response = await client.GetAsync("/api/events?from=2027-06-01&to=2027-06-30", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
