using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Features.Events.CreateEvent;

/// <summary>
/// The created event definition. Local dates and times are Europe/Helsinki; for a single timed event they are derived
/// from <see cref="StartUtc"/> and <see cref="EndUtc"/>, so a start in the DST gap shows the shifted time.
/// </summary>
public record CreateEventResponse(
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
    CreateEventRecurrenceResponse? Recurrence,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record CreateEventRecurrenceResponse(
    RuleType RuleType,
    int? Interval,
    int? DayOfMonth,
    int? Month,
    DayOfWeek? DayOfWeek,
    DateOnly UntilDate);
