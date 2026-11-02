using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Features.Events.CreateEvent;

/// <summary>
/// Body of <c>POST /api/events</c>. All dates and times are local Europe/Helsinki values; the client never sends UTC.
/// Fields are nullable so that missing values are reported by the validator as <c>400</c> problem details.
/// <list type="bullet">
/// <item>Single: <see cref="StartDate"/>, <see cref="StartTime"/>, <see cref="EndDate"/>, <see cref="EndTime"/>.</item>
/// <item>AllDay: <see cref="StartDate"/>, <see cref="EndDate"/> (inclusive).</item>
/// <item>Recurring: <see cref="StartDate"/>, <see cref="Recurrence"/>, and <see cref="StartTime"/> with <see cref="EndTime"/> for a timed series.</item>
/// </list>
/// </summary>
public record CreateEventRequest(
    string? Title,
    string? Description,
    EventKind? Kind,
    DateOnly? StartDate,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    TimeOnly? EndTime,
    CreateEventRecurrence? Recurrence);

/// <summary>The recurrence rule of a recurring event (spec section 5.1). Only the parameters of <see cref="RuleType"/> are set.</summary>
public record CreateEventRecurrence(
    RuleType? RuleType,
    int? Interval,
    int? DayOfMonth,
    int? Month,
    DayOfWeek? DayOfWeek,
    DateOnly? UntilDate);
