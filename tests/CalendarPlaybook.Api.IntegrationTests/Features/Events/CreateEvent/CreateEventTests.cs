using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.IntegrationTests.Infrastructure;

namespace CalendarPlaybook.Api.IntegrationTests.Features.Events.CreateEvent;

/// <summary><c>POST /api/events</c> through the real pipeline: JwtBearer, validation, EF Core, SQL Server.</summary>
public class CreateEventTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private static readonly object SingleEvent = new
    {
        title = "Dentist",
        description = "Bring card",
        kind = "Single",
        startDate = "2027-06-01",
        startTime = "10:00",
        endDate = "2027-06-01",
        endTime = "11:30",
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Single_Returns201WithLocation_AndStoresUtcRowOwnedBySub()
    {
        var sub = NewSub();

        var response = await CreateClientFor(sub).PostAsJsonAsync("/api/events", SingleEvent, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/events/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Single", body.GetProperty("kind").GetString());

        var row = Assert.Single(await EventsOwnedBy(sub));
        Assert.Equal(id, row.Id);
        Assert.Equal("Dentist", row.Title);
        Assert.Equal("Bring card", row.Description);
        Assert.Equal(EventKind.Single, row.Kind);
        Assert.Equal("Europe/Helsinki", row.TimeZone);
        // 10:00 and 11:30 in Helsinki summer time (UTC+3).
        Assert.Equal(new DateTime(2027, 6, 1, 7, 0, 0), row.StartUtc);
        Assert.Equal(new DateTime(2027, 6, 1, 8, 30, 0), row.EndUtc);
        Assert.Null(row.StartDate);
        Assert.Null(row.RuleType);
    }

    [Fact]
    public async Task AllDay_StoresLocalDatesWithoutUtcConversion()
    {
        var sub = NewSub();

        var response = await CreateClientFor(sub).PostAsJsonAsync("/api/events", new
        {
            title = "Trip",
            kind = "AllDay",
            startDate = "2027-03-28",
            endDate = "2027-03-30",
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = Assert.Single(await EventsOwnedBy(sub));
        Assert.Equal(new DateOnly(2027, 3, 28), row.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 30), row.EndDate);
        Assert.Null(row.StartUtc);
        Assert.Null(row.EndUtc);
    }

    [Fact]
    public async Task WeekDayRule_RoundTripsAllSevenDayNamesThroughSqlServer()
    {
        var sub = NewSub();
        var client = CreateClientFor(sub);

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var response = await client.PostAsJsonAsync("/api/events", new
            {
                title = $"Weekly {day}",
                kind = "Recurring",
                startDate = "2027-01-04",
                startTime = "09:00",
                endTime = "09:30",
                recurrence = new { ruleType = "WeekDay", dayOfWeek = day.ToString(), untilDate = "2027-12-31" },
            }, Ct);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(day.ToString(), body.GetProperty("recurrence").GetProperty("dayOfWeek").GetString());
        }

        var rows = await EventsOwnedBy(sub);
        Assert.Equal(Enum.GetValues<DayOfWeek>().Order(), rows.Select(r => r.DayOfWeek!.Value).Order());
        Assert.All(rows, row =>
        {
            Assert.Equal(RuleType.WeekDay, row.RuleType);
            Assert.Equal(new TimeOnly(9, 0), row.StartLocalTime);
            Assert.Equal(new TimeOnly(9, 30), row.EndLocalTime);
            Assert.Null(row.StartUtc);
        });
    }

    public static TheoryData<string, object> InvalidRequests => new()
    {
        { "title", new { title = "", kind = "AllDay", startDate = "2027-03-28", endDate = "2027-03-28" } },
        { "endTime", new { title = "Late", kind = "Single", startDate = "2027-06-01", startTime = "11:00", endDate = "2027-06-01", endTime = "10:00" } },
        { "recurrence.untilDate", new { title = "Forever", kind = "Recurring", startDate = "2027-01-04", recurrence = new { ruleType = "EveryDays", interval = 1 } } },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequest_Returns400ProblemDetails_AndStoresNothing(string field, object request)
    {
        var sub = NewSub();

        var response = await CreateClientFor(sub).PostAsJsonAsync("/api/events", request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for '{field}': {problem}");
        Assert.Empty(await EventsOwnedBy(sub));
    }

    [Fact]
    public async Task NumericEnumValue_Returns400_AndStoresNothing()
    {
        var sub = NewSub();

        var response = await CreateClientFor(sub).PostAsJsonAsync("/api/events", new
        {
            title = "Trip",
            kind = 1,
            startDate = "2027-03-28",
            endDate = "2027-03-28",
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await EventsOwnedBy(sub));
    }

    public static TheoryData<string> RejectedTokens => new() { "none", "foreign key", "wrong audience", "wrong issuer", "expired", "no sub" };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task MissingOrInvalidToken_Returns401_AndStoresNothing(string token)
    {
        var sub = NewSub();
        var client = CreateClient(token switch
        {
            "none" => null,
            "foreign key" => TestTokens.Create(sub, key: TestTokens.ForeignKey),
            "wrong audience" => TestTokens.Create(sub, audience: "https://another-api.test"),
            "wrong issuer" => TestTokens.Create(sub, issuer: "https://another-tenant.cidaas.test"),
            "expired" => TestTokens.Create(sub, expiresUtc: DateTime.UtcNow.AddMinutes(-10)),
            "no sub" => TestTokens.Create(sub: null),
            _ => throw new ArgumentOutOfRangeException(nameof(token)),
        });

        var response = await client.PostAsJsonAsync("/api/events", SingleEvent, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await EventsOwnedBy(sub));
    }

    [Fact]
    public async Task OwnerIsolation_EachRowBelongsToTheTokenSub_AndBodyOwnerIdIsIgnored()
    {
        var userA = NewSub();
        var userB = NewSub();

        var responseA = await CreateClientFor(userA).PostAsJsonAsync("/api/events", SingleEvent, Ct);
        var responseB = await CreateClientFor(userB).PostAsJsonAsync("/api/events", new
        {
            title = "Not yours",
            kind = "AllDay",
            startDate = "2027-03-28",
            endDate = "2027-03-28",
            ownerId = userA,
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);
        var idA = (await responseA.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        var idB = (await responseB.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        Assert.Equal(idA, Assert.Single(await EventsOwnedBy(userA)).Id);
        Assert.Equal(idB, Assert.Single(await EventsOwnedBy(userB)).Id);
        Assert.Equal(userB, (await FindEvent(idB))!.OwnerId);
        Assert.False((await responseB.Content.ReadAsStringAsync(Ct)).Contains(userA), "B's response must not mention A.");
    }
}
