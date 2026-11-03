using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Features.Events.GetEvent;

/// <summary>
/// The event definition (not occurrences). Local dates and times are Europe/Helsinki; for a single timed event they are
/// derived from <see cref="StartUtc"/> and <see cref="EndUtc"/>.
/// </summary>
public record GetEventResponse(
    Guid Id,
    string Title,
    string? Description,
    EventKind Kind,
    DateOnly? StartDate,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    TimeOnly? EndTime,
    DateTime? StartUtc,
    DateTime? EndUtc,
    string TimeZone,
    GetEventRecurrenceResponse? Recurrence,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record GetEventRecurrenceResponse(
    RuleType RuleType,
    int? Interval,
    int? DayOfMonth,
    int? Month,
    DayOfWeek? DayOfWeek,
    DateOnly UntilDate);
