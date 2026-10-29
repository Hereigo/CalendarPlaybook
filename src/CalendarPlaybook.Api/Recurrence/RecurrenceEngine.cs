namespace CalendarPlaybook.Api.Recurrence;

/// <summary>
/// Expands a recurrence rule into local occurrence dates (spec section 5.4).
/// Works on dates only; time-of-day and time zone handling live elsewhere.
/// </summary>
public static class RecurrenceEngine
{
    /// <summary>
    /// Returns the occurrence dates of a series that fall within [from, to] and do not pass until.
    /// Dates are inclusive on both ends. The series start and until dates are inclusive.
    /// </summary>
    public static IEnumerable<DateOnly> Occurrences(
        RecurrenceRule rule,
        DateOnly start,
        DateOnly until,
        DateOnly from,
        DateOnly to)
    {
        if (until < start)
        {
            throw new ArgumentException("Until date must not be before the start date.", nameof(until));
        }

        var last = to < until ? to : until;
        var first = from > start ? from : start;

        if (first > last)
        {
            return [];
        }

        return rule switch
        {
            RecurrenceRule.EveryDays everyDays => EveryDaysOccurrences(everyDays, start, first, last),
            RecurrenceRule.MonthDay monthDay => MonthDayOccurrences(monthDay, start, first, last),
            RecurrenceRule.WeekDay weekDay => WeekDayOccurrences(weekDay, start, first, last),
            RecurrenceRule.YearDay yearDay => YearDayOccurrences(yearDay, start, first, last),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unsupported recurrence rule."),
        };
    }

    private static IEnumerable<DateOnly> EveryDaysOccurrences(RecurrenceRule.EveryDays rule, DateOnly start, DateOnly first, DateOnly last)
    {
        // Jump straight to the first step on or after `first` instead of walking from the start.
        var daysFromStart = first.DayNumber - start.DayNumber;
        var steps = (daysFromStart + rule.Interval - 1) / rule.Interval;

        for (var date = start.AddDays(steps * rule.Interval); date <= last; date = date.AddDays(rule.Interval))
        {
            yield return date;
        }
    }

    private static IEnumerable<DateOnly> MonthDayOccurrences(RecurrenceRule.MonthDay rule, DateOnly start, DateOnly first, DateOnly last)
    {
        var month = new DateOnly(first.Year, first.Month, 1);
        var lastMonth = new DateOnly(last.Year, last.Month, 1);

        for (; month <= lastMonth; month = month.AddMonths(1))
        {
            var day = Math.Min(rule.DayOfMonth, DateTime.DaysInMonth(month.Year, month.Month));
            var date = new DateOnly(month.Year, month.Month, day);

            if (date >= first && date <= last && date >= start)
            {
                yield return date;
            }
        }
    }

    private static IEnumerable<DateOnly> WeekDayOccurrences(RecurrenceRule.WeekDay rule, DateOnly start, DateOnly first, DateOnly last)
    {
        // First matching weekday on or after `first`, then every 7 days.
        var offset = ((int)rule.DayOfWeek - (int)first.DayOfWeek + 7) % 7;

        for (var date = first.AddDays(offset); date <= last; date = date.AddDays(7))
        {
            if (date >= start)
            {
                yield return date;
            }
        }
    }

    private static IEnumerable<DateOnly> YearDayOccurrences(RecurrenceRule.YearDay rule, DateOnly start, DateOnly first, DateOnly last)
    {
        for (var year = first.Year; year <= last.Year; year++)
        {
            var day = Math.Min(rule.DayOfMonth, DateTime.DaysInMonth(year, rule.Month));
            var date = new DateOnly(year, rule.Month, day);

            if (date >= first && date <= last && date >= start)
            {
                yield return date;
            }
        }
    }
}
