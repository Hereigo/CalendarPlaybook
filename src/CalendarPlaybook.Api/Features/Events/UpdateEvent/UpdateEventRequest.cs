using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Features.Events.UpdateEvent;

/// <summary>
/// Body of <c>PUT /api/events/{id}</c>: the full new definition, same fields and rules as <c>POST</c> (the kind may change).
/// All dates and times are local Europe/Helsinki values. For a recurring event the change applies to the whole series.
/// </summary>
public record UpdateEventRequest(
    string? Title,
    string? Description,
    EventKind? Kind,
    DateOnly? StartDate,
    TimeOnly? StartTime,
    DateOnly? EndDate,
    TimeOnly? EndTime,
    UpdateEventRecurrence? Recurrence)
{
    /// <summary>The same input as a create request; the validation and mapping rules are shared with <c>POST</c>.</summary>
    public CreateEventRequest ToCreateRequest() => new(
        Title, Description, Kind, StartDate, StartTime, EndDate, EndTime,
        Recurrence is null
            ? null
            : new CreateEventRecurrence(Recurrence.RuleType, Recurrence.Interval, Recurrence.DayOfMonth, Recurrence.Month, Recurrence.DayOfWeek, Recurrence.UntilDate));
}

/// <summary>The recurrence rule of a recurring event (spec section 5.1). Only the parameters of <see cref="RuleType"/> are set.</summary>
public record UpdateEventRecurrence(
    RuleType? RuleType,
    int? Interval,
    int? DayOfMonth,
    int? Month,
    DayOfWeek? DayOfWeek,
    DateOnly? UntilDate);
