using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Recurrence;
using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Features.Events.ListOccurrences;

public static class ListOccurrencesHandler
{
    public static async Task<List<ListOccurrencesItem>> HandleAsync(
        CalendarDbContext db,
        string ownerId,
        ListOccurrencesQuery query,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var (from, to) = (query.From, query.To);
        var (rangeStartUtc, rangeEndUtc) = RangeUtc(from, to);

        // Narrows the rows in SQL; Expand applies the exact rules, so the two stay consistent.
        var events = await db.Events
            .AsNoTracking()
            .Where(e => e.OwnerId == ownerId)
            .Where(e =>
                (e.Kind == EventKind.Single && e.StartUtc < rangeEndUtc && e.EndUtc > rangeStartUtc)
                || (e.Kind == EventKind.AllDay && e.StartDate <= to && e.EndDate >= from)
                || (e.Kind == EventKind.Recurring && e.StartDate <= to && e.UntilDate >= from))
            .ToListAsync(cancellationToken);

        var items = Expand(events, from, to);

        // Counts only: titles and descriptions never go to logs (spec section 11).
        logger.LogInformation("Listed {Count} occurrences of {EventCount} events", items.Count, events.Count);

        return items;
    }

    /// <summary>
    /// Expands events into the occurrences that touch [from, to] (local dates, inclusive), ordered by local start date,
    /// all-day before timed, then start time, title and event id. A timed single event is included when it overlaps the
    /// range as an instant interval; an all-day event when its date span overlaps; a recurring event per occurrence date.
    /// </summary>
    public static List<ListOccurrencesItem> Expand(IEnumerable<Event> events, DateOnly from, DateOnly to)
    {
        var (rangeStartUtc, rangeEndUtc) = RangeUtc(from, to);
        var items = new List<ListOccurrencesItem>();

        foreach (var entity in events)
        {
            switch (entity)
            {
                case { Kind: EventKind.Single, StartUtc: { } startUtc, EndUtc: { } endUtc }
                    when startUtc < rangeEndUtc && endUtc > rangeStartUtc:
                    {
                        // Rows read from SQL Server have an unspecified kind; mark them UTC so they serialize with a Z.
                        var start = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
                        var end = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
                        items.Add(new ListOccurrencesItem(
                            entity.Id, entity.Kind, entity.Title, start, end,
                            HelsinkiTime.ToLocal(start).Date, HelsinkiTime.ToLocal(end).Date));
                        break;
                    }

                case { Kind: EventKind.AllDay, StartDate: { } startDate, EndDate: { } endDate }
                    when startDate <= to && endDate >= from:
                    items.Add(new ListOccurrencesItem(entity.Id, entity.Kind, entity.Title, null, null, startDate, endDate));
                    break;

                case { Kind: EventKind.Recurring, StartDate: { } seriesStart, UntilDate: { } until }:
                    foreach (var date in RecurrenceEngine.Occurrences(ToRule(entity), seriesStart, until, from, to))
                    {
                        items.Add(ToRecurringItem(entity, date));
                    }

                    break;
            }
        }

        return items
            .OrderBy(i => i.StartDate)
            .ThenBy(i => i.OccurrenceStartUtc is not null)
            .ThenBy(i => i.OccurrenceStartUtc)
            .ThenBy(i => i.Title, StringComparer.Ordinal)
            .ThenBy(i => i.EventId)
            .ToList();
    }

    /// <summary>The UTC instants of the start of <paramref name="from"/> and the end of <paramref name="to"/> in Helsinki. Midnight never falls in the DST gap (03:00-04:00).</summary>
    private static (DateTime StartUtc, DateTime EndUtc) RangeUtc(DateOnly from, DateOnly to) =>
        (HelsinkiTime.ToUtc(from, TimeOnly.MinValue), HelsinkiTime.ToUtc(to.AddDays(1), TimeOnly.MinValue));

    private static ListOccurrencesItem ToRecurringItem(Event entity, DateOnly date)
    {
        if (entity is { StartLocalTime: { } startTime, EndLocalTime: { } endTime })
        {
            // Wall-clock times per occurrence, so 10:00 stays 10:00 local across DST changes (spec section 5.4).
            return new ListOccurrencesItem(
                entity.Id, entity.Kind, entity.Title,
                HelsinkiTime.ToUtc(date, startTime), HelsinkiTime.ToUtc(date, endTime),
                date, date);
        }

        return new ListOccurrencesItem(entity.Id, entity.Kind, entity.Title, null, null, date, date);
    }

    private static RecurrenceRule ToRule(Event entity) => entity.RuleType switch
    {
        RuleType.EveryDays => new RecurrenceRule.EveryDays(entity.Interval!.Value),
        RuleType.MonthDay => new RecurrenceRule.MonthDay(entity.DayOfMonth!.Value),
        RuleType.WeekDay => new RecurrenceRule.WeekDay(entity.DayOfWeek!.Value),
        RuleType.YearDay => new RecurrenceRule.YearDay(entity.Month!.Value, entity.DayOfMonth!.Value),
        _ => throw new InvalidOperationException($"Event {entity.Id} has an unsupported rule type."),
    };
}
