using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Features.Events.ListOccurrences;

/// <summary>
/// One occurrence of an event in the requested range (spec section 9). Timed occurrences carry
/// <see cref="OccurrenceStartUtc"/> and <see cref="OccurrenceEndUtc"/>; all-day occurrences carry only dates.
/// <see cref="StartDate"/> and <see cref="EndDate"/> are Europe/Helsinki local dates for every item, so the client can place it
/// in a calendar cell without converting time zones. <see cref="EndDate"/> is inclusive.
/// </summary>
public record ListOccurrencesItem(
    Guid EventId,
    EventKind Kind,
    string Title,
    DateTime? OccurrenceStartUtc,
    DateTime? OccurrenceEndUtc,
    DateOnly StartDate,
    DateOnly EndDate);
