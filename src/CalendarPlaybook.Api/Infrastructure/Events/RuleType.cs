namespace CalendarPlaybook.Api.Infrastructure.Events;

/// <summary>Recurrence rule types from spec section 5.1. Stored and sent as names.</summary>
public enum RuleType
{
    EveryDays,
    MonthDay,
    WeekDay,
    YearDay,
}
