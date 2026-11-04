using System.Security.Cryptography;
using System.Text;
using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Recurrence;

namespace CalendarPlaybook.LegacyImport;

/// <summary>
/// The result of mapping one legacy row. <see cref="Request"/> is null when the row is skipped (<see cref="SkipReason"/> set).
/// Warnings and skip reasons never contain the title or description.
/// </summary>
public sealed record LegacyMapping(
    int LegacyId,
    Guid Id,
    CreateEventRequest? Request,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<string> Warnings,
    string? SkipReason);

/// <summary>
/// Maps old Calendarium events to <see cref="CreateEventRequest"/>s of this app. Pure: no database or file access.
/// </summary>
public static class LegacyEventMapper
{
    /// <summary>Every migrated series runs until this date, because the spec has no "forever" (section 5.1).</summary>
    public static readonly DateOnly SeriesUntilDate = new(2099, 12, 31);

    public const string UntitledTitle = "(untitled)";

    private static readonly TimeOnly LatestEndTime = new(23, 59);

    public static LegacyMapping Map(LegacyCalEvent row, string ownerId)
    {
        var id = DeterministicId(ownerId, row.Id);
        var createdUtc = ToUtc(row.Created ?? row.Modified);
        var updatedUtc = ToUtc(row.Modified);
        var warnings = new List<string>();

        LegacyMapping Skip(string reason) => new(row.Id, id, null, createdUtc, updatedUtc, warnings, reason);

        if (row.Status != LegacyStatus.Active)
        {
            return Skip($"status is {row.Status}");
        }

        var startDate = DateOnly.FromDateTime(row.Started);
        var (startTime, endTime) = Times(row.Time, warnings);
        var interval = row.EveryXDays is > 0 ? row.EveryXDays : null;

        if (interval is not null && row.Repeat != LegacyRepeat.EveryXdays)
        {
            warnings.Add($"EveryXDays={interval} ignored for repeat {row.Repeat}");
        }

        CheckDerivedFields(row, startDate, warnings);

        CreateEventRecurrence Rule(RuleType ruleType, int? everyDays = null, int? dayOfMonth = null, int? month = null) =>
            new(ruleType, everyDays, dayOfMonth, month, null, SeriesUntilDate);

        CreateEventRequest request;
        switch (row.Repeat)
        {
            case LegacyRepeat.Once when startTime is null:
                request = new(Title(row), null, EventKind.AllDay, startDate, null, startDate, null, null);
                break;

            case LegacyRepeat.Once:
                request = new(Title(row), null, EventKind.Single, startDate, startTime, startDate, endTime, null);
                break;

            case LegacyRepeat.Yearly:
                request = new(Title(row), null, EventKind.Recurring, startDate, startTime, null, endTime,
                    Rule(RuleType.YearDay, dayOfMonth: startDate.Day, month: startDate.Month));
                break;

            case LegacyRepeat.Monthly:
                request = new(Title(row), null, EventKind.Recurring, startDate, startTime, null, endTime,
                    Rule(RuleType.MonthDay, dayOfMonth: startDate.Day));
                break;

            case LegacyRepeat.EveryXdays when interval is null:
                return Skip($"repeat EveryXdays without a positive interval (EveryXDays={row.EveryXDays?.ToString() ?? "null"})");

            case LegacyRepeat.EveryXdays:
                request = new(Title(row), null, EventKind.Recurring, startDate, startTime, null, endTime,
                    Rule(RuleType.EveryDays, everyDays: interval));
                break;

            default:
                return Skip($"unknown repeat value {(int)row.Repeat}");
        }

        return new LegacyMapping(row.Id, id, request, createdUtc, updatedUtc, warnings, null);
    }

    /// <summary>
    /// Same owner and legacy id always give the same id, so re-running the import finds rows it already wrote.
    /// A name-based (version 5 style) GUID from SHA-256.
    /// </summary>
    public static Guid DeterministicId(string ownerId, int legacyId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"calendarium:{ownerId}:{legacyId}"));
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>00:00 and 00:01 meant "no time" in the old app, so those rows become all-day. Others last one hour, at most until 23:59.</summary>
    private static (TimeOnly? Start, TimeOnly? End) Times(TimeSpan time, List<string> warnings)
    {
        var start = new TimeOnly(time.Hours, time.Minutes);
        if (start <= new TimeOnly(0, 1))
        {
            return (null, null);
        }

        if (start >= LatestEndTime)
        {
            warnings.Add($"time {start:HH\\:mm} leaves no room for an end time; imported as all-day");
            return (null, null);
        }

        var end = start >= LatestEndTime.AddHours(-1) ? LatestEndTime : start.AddHours(1);
        return (start, end);
    }

    private static void CheckDerivedFields(LegacyCalEvent row, DateOnly started, List<string> warnings)
    {
        var dayMatches = row.Day == started.Day;
        var monthMatches = row.Repeat == LegacyRepeat.Monthly || row.Month == started.Month;

        if (!dayMatches || !monthMatches)
        {
            warnings.Add($"Day/Month {row.Day}/{row.Month} differ from Started {started:yyyy-MM-dd}; Started is used");
        }
    }

    private static string Title(LegacyCalEvent row)
    {
        var title = row.Description?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return UntitledTitle;
        }

        return title.Length > CreateEventValidator.TitleMaxLength ? title[..CreateEventValidator.TitleMaxLength] : title;
    }

    /// <summary>Old timestamps are Helsinki wall-clock time. Truncated to milliseconds to match datetime2(3).</summary>
    private static DateTime ToUtc(DateTime local)
    {
        var utc = HelsinkiTime.ToUtc(DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
