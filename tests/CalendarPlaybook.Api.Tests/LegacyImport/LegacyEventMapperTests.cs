using System.Text.Json;
using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Infrastructure.LegacyImport;

namespace CalendarPlaybook.Api.Tests.LegacyImport;

public class LegacyEventMapperTests
{
    private const string Owner = "auth0|legacy-owner";

    private static LegacyCalEvent Row(
        LegacyRepeat repeat,
        DateTime started,
        TimeSpan time = default,
        int? everyXDays = null,
        LegacyStatus status = LegacyStatus.Active,
        string? description = "Legacy event") => new(
            Id: 42,
            Day: started.Day,
            Month: repeat == LegacyRepeat.Monthly ? 0 : started.Month,
            Year: repeat == LegacyRepeat.Yearly ? 0 : started.Year,
            EveryXDays: everyXDays,
            Repeat: repeat,
            Status: status,
            Created: null,
            Modified: new DateTime(2025, 7, 1, 12, 0, 0),
            Started: started,
            Time: time,
            Description: description);

    [Fact]
    public void OnceAtMidnight_BecomesAllDayOnTheStartDate()
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2025, 5, 30)), Owner).Request!;

        Assert.Equal(EventKind.AllDay, request.Kind);
        Assert.Equal(new DateOnly(2025, 5, 30), request.StartDate);
        Assert.Equal(new DateOnly(2025, 5, 30), request.EndDate);
        Assert.Null(request.StartTime);
        Assert.Null(request.Recurrence);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    public void TimeZeroOrOneMinutePastMidnight_IsAllDay(int hour, int minute)
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.Yearly, new DateTime(2023, 3, 7), new TimeSpan(hour, minute, 0)), Owner).Request!;

        Assert.Null(request.StartTime);
        Assert.Null(request.EndTime);
    }

    [Fact]
    public void OnceWithTime_BecomesSingleLastingOneHour()
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2026, 5, 29), new TimeSpan(11, 11, 0)), Owner).Request!;

        Assert.Equal(EventKind.Single, request.Kind);
        Assert.Equal(new DateOnly(2026, 5, 29), request.StartDate);
        Assert.Equal(new DateOnly(2026, 5, 29), request.EndDate);
        Assert.Equal(new TimeOnly(11, 11), request.StartTime);
        Assert.Equal(new TimeOnly(12, 11), request.EndTime);
    }

    [Fact]
    public void LateTime_EndIsCappedAt2359()
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2026, 5, 29), new TimeSpan(23, 30, 0)), Owner).Request!;

        Assert.Equal(new TimeOnly(23, 30), request.StartTime);
        Assert.Equal(new TimeOnly(23, 59), request.EndTime);
    }

    [Fact]
    public void TimeAt2359_FallsBackToAllDayWithWarning()
    {
        var mapping = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2026, 5, 29), new TimeSpan(23, 59, 0)), Owner);

        Assert.Equal(EventKind.AllDay, mapping.Request!.Kind);
        Assert.Single(mapping.Warnings);
    }

    [Fact]
    public void Yearly_BecomesYearDayUntil2099()
    {
        var mapping = LegacyEventMapper.Map(Row(LegacyRepeat.Yearly, new DateTime(2023, 5, 18), new TimeSpan(11, 11, 0)), Owner);
        var request = mapping.Request!;

        Assert.Equal(EventKind.Recurring, request.Kind);
        Assert.Equal(new DateOnly(2023, 5, 18), request.StartDate);
        Assert.Equal(new TimeOnly(11, 11), request.StartTime);
        Assert.Equal(new CreateEventRecurrence(RuleType.YearDay, null, 18, 5, null, new DateOnly(2099, 12, 31)), request.Recurrence);
        Assert.Empty(mapping.Warnings);
    }

    [Fact]
    public void Monthly_BecomesMonthDay()
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.Monthly, new DateTime(2024, 10, 28)), Owner).Request!;

        Assert.Equal(new CreateEventRecurrence(RuleType.MonthDay, null, 28, null, null, LegacyEventMapper.SeriesUntilDate), request.Recurrence);
    }

    [Fact]
    public void EveryXDays_BecomesEveryDaysAnchoredOnStarted()
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.EveryXdays, new DateTime(2026, 1, 6), everyXDays: 14), Owner).Request!;

        Assert.Equal(new DateOnly(2026, 1, 6), request.StartDate);
        Assert.Equal(new CreateEventRecurrence(RuleType.EveryDays, 14, null, null, null, LegacyEventMapper.SeriesUntilDate), request.Recurrence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void EveryXDaysWithoutInterval_IsSkipped(int? interval)
    {
        var mapping = LegacyEventMapper.Map(Row(LegacyRepeat.EveryXdays, new DateTime(2026, 1, 6), everyXDays: interval), Owner);

        Assert.Null(mapping.Request);
        Assert.NotNull(mapping.SkipReason);
    }

    [Theory]
    [InlineData(LegacyRepeat.Once)]
    [InlineData(LegacyRepeat.Yearly)]
    [InlineData(LegacyRepeat.Monthly)]
    public void StrayEveryXDays_IsIgnoredWithWarning(LegacyRepeat repeat)
    {
        var mapping = LegacyEventMapper.Map(Row(repeat, new DateTime(2025, 2, 27), everyXDays: 90), Owner);

        Assert.NotEqual(RuleType.EveryDays, mapping.Request!.Recurrence?.RuleType);
        Assert.Contains(mapping.Warnings, w => w.Contains("EveryXDays=90"));
    }

    [Theory]
    [InlineData(LegacyStatus.Disabled)]
    [InlineData(LegacyStatus.Deleted)]
    public void InactiveStatus_IsSkipped(LegacyStatus status)
    {
        var mapping = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2025, 1, 1), status: status), Owner);

        Assert.Null(mapping.Request);
        Assert.Contains(status.ToString(), mapping.SkipReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void EmptyDescription_GetsPlaceholderTitle(string? description)
    {
        var request = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2025, 1, 1), description: description), Owner).Request!;

        Assert.Equal(LegacyEventMapper.UntitledTitle, request.Title);
    }

    [Fact]
    public void DescriptionBecomesTrimmedTitle_AndWarningsDoNotContainIt()
    {
        var mapping = LegacyEventMapper.Map(Row(LegacyRepeat.Monthly, new DateTime(2025, 1, 5), everyXDays: 25, description: "  Secret note  "), Owner);

        Assert.Equal("Secret note", mapping.Request!.Title);
        Assert.Null(mapping.Request.Description);
        Assert.DoesNotContain(mapping.Warnings, w => w.Contains("Secret"));
    }

    [Fact]
    public void Timestamps_AreHelsinkiLocalConvertedToUtc()
    {
        var mapping = LegacyEventMapper.Map(Row(LegacyRepeat.Once, new DateTime(2025, 1, 1)), Owner);

        // 1 July 2025 12:00 Helsinki summer time (UTC+3); Created is null so it falls back to Modified.
        Assert.Equal(new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc), mapping.UpdatedAtUtc);
        Assert.Equal(mapping.UpdatedAtUtc, mapping.CreatedAtUtc);
    }

    [Fact]
    public void DeterministicId_IsStablePerOwnerAndDiffersBetweenOwners()
    {
        var first = LegacyEventMapper.DeterministicId(Owner, 1055);

        Assert.Equal(first, LegacyEventMapper.DeterministicId(Owner, 1055));
        Assert.NotEqual(first, LegacyEventMapper.DeterministicId(Owner, 1056));
        Assert.NotEqual(first, LegacyEventMapper.DeterministicId("auth0|someone-else", 1055));
        Assert.Equal(5, first.Version);
    }

    [Fact]
    public void EveryMappedRequest_PassesCreateEventValidator()
    {
        foreach (var repeat in Enum.GetValues<LegacyRepeat>())
        {
            foreach (var time in new[] { TimeSpan.Zero, new TimeSpan(11, 11, 0), new TimeSpan(23, 30, 0) })
            {
                var request = LegacyEventMapper.Map(Row(repeat, new DateTime(2024, 2, 29), time, everyXDays: 7), Owner).Request!;
                Assert.Empty(CreateEventValidator.Validate(request));
            }
        }
    }

    /// <summary>The real backup is git-ignored personal data, so this only runs where the file exists.</summary>
    [Fact]
    public void LegacyBackup_WhenPresent_EveryRowMapsToAValidRequest()
    {
        var path = FindBackup();
        if (path is null)
        {
            return;
        }

        var rows = JsonSerializer.Deserialize<List<LegacyCalEvent>>(File.ReadAllText(path))!;
        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            var mapping = LegacyEventMapper.Map(row, Owner);
            Assert.True(mapping.Request is not null, $"Row {row.Id} skipped: {mapping.SkipReason}");
            Assert.Empty(CreateEventValidator.Validate(mapping.Request));
        }

        Assert.Equal(rows.Count, rows.Select(r => LegacyEventMapper.DeterministicId(Owner, r.Id)).Distinct().Count());
    }

    private static string? FindBackup()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "DATA_FROM_OLD_PROJECT", "database_backup.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
