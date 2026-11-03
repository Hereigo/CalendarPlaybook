using System.Globalization;

namespace CalendarPlaybook.Api.Features.Events.ListOccurrences;

/// <summary>A validated date range: local Europe/Helsinki dates, both ends inclusive.</summary>
public record ListOccurrencesQuery(DateOnly From, DateOnly To)
{
    public const int MaxRangeDays = 370;

    private const string DateFormat = "yyyy-MM-dd";

    // Keeps the UTC conversion of the range ends inside DateTime limits.
    private static readonly DateOnly Earliest = new(2, 1, 1);
    private static readonly DateOnly Latest = new(9998, 12, 31);

    /// <summary>
    /// Parses and validates the <c>from</c> and <c>to</c> query values (spec section 9). Error keys are the query parameter names.
    /// The range is counted in days with both ends inclusive, so <see cref="MaxRangeDays"/> is the largest allowed count.
    /// </summary>
    public static (ListOccurrencesQuery? Query, Dictionary<string, string[]> Errors) Parse(string? from, string? to)
    {
        var errors = new Dictionary<string, string[]>();

        var fromDate = ParseDate(from, "from", errors);
        var toDate = ParseDate(to, "to", errors);

        if (fromDate is { } start && toDate is { } end)
        {
            if (end < start)
            {
                errors["to"] = ["To must not be before from."];
            }
            else if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
            {
                errors["to"] = [$"The range must be at most {MaxRangeDays} days."];
            }
        }

        return errors.Count > 0 ? (null, errors) : (new ListOccurrencesQuery(fromDate!.Value, toDate!.Value), errors);
    }

    private static DateOnly? ParseDate(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["This field is required."];
            return null;
        }

        if (!DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors[field] = [$"Use a date in the form {DateFormat}."];
            return null;
        }

        if (date < Earliest || date > Latest)
        {
            errors[field] = ["Date is out of the supported range."];
            return null;
        }

        return date;
    }
}
