namespace CalendarPlaybook.Api.Infrastructure.Events;

/// <summary>Event kinds from spec section 4. Stored and sent as names.</summary>
public enum EventKind
{
    Single,
    AllDay,
    Recurring,
}
