namespace CalendarPlaybook.Api.Infrastructure.Events;

/// <summary>
/// A row of the Events table (spec section 10). Which nullable columns are set depends on <see cref="Kind"/>.
/// </summary>
public class Event
{
    public Guid Id { get; set; }

    /// <summary>Auth0 <c>sub</c> of the owner.</summary>
    public string OwnerId { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public EventKind Kind { get; set; }

    /// <summary>Timed single events only.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>Timed single events only.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>All-day events and the first local date of every recurring series.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Timed recurring events: local wall-clock start time.</summary>
    public TimeOnly? StartLocalTime { get; set; }

    /// <summary>Timed recurring events: local wall-clock end time, same day, after the start time.</summary>
    public TimeOnly? EndLocalTime { get; set; }

    /// <summary>All-day single events. Inclusive.</summary>
    public DateOnly? EndDate { get; set; }

    public string TimeZone { get; set; } = "";

    public RuleType? RuleType { get; set; }

    public int? Interval { get; set; }

    public int? DayOfMonth { get; set; }

    public int? Month { get; set; }

    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>Recurring events only. Inclusive.</summary>
    public DateOnly? UntilDate { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
