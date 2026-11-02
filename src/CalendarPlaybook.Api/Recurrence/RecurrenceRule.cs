namespace CalendarPlaybook.Api.Recurrence;

/// <summary>
/// A recurrence rule from spec section 5.1. Parameters are validated on construction.
/// </summary>
public abstract record RecurrenceRule
{
    /// <summary>Every X days, anchored on the series start date.</summary>
    public sealed record EveryDays : RecurrenceRule
    {
        public EveryDays(int interval)
        {
            if (interval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be at least 1.");
            }

            Interval = interval;
        }

        public int Interval { get; }
    }

    /// <summary>Day X of every month. Days missing in a month shift to the last day of that month.</summary>
    public sealed record MonthDay : RecurrenceRule
    {
        public MonthDay(int dayOfMonth)
        {
            if (dayOfMonth is < 1 or > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(dayOfMonth), dayOfMonth, "Day of month must be between 1 and 31.");
            }

            DayOfMonth = dayOfMonth;
        }

        public int DayOfMonth { get; }
    }

    /// <summary>Every week on one chosen weekday.</summary>
    public sealed record WeekDay : RecurrenceRule
    {
        public WeekDay(DayOfWeek dayOfWeek)
        {
            DayOfWeek = dayOfWeek;
        }

        public DayOfWeek DayOfWeek { get; }
    }

    /// <summary>Same month and day every year. Feb 29 shifts to Feb 28 in non-leap years; days that never exist are rejected.</summary>
    public sealed record YearDay : RecurrenceRule
    {
        public YearDay(int month, int dayOfMonth)
        {
            if (month is < 1 or > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
            }

            // A leap year is used so Feb 29 passes; days that never exist (Feb 30, Apr 31) are rejected (spec section 5.1).
            if (dayOfMonth < 1 || dayOfMonth > DateTime.DaysInMonth(2024, month))
            {
                throw new ArgumentOutOfRangeException(nameof(dayOfMonth), dayOfMonth, "Day of month does not exist in that month.");
            }

            Month = month;
            DayOfMonth = dayOfMonth;
        }

        public int Month { get; }

        public int DayOfMonth { get; }
    }
}
