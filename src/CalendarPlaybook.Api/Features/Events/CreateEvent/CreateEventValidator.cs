using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Recurrence;

namespace CalendarPlaybook.Api.Features.Events.CreateEvent;

/// <summary>
/// Input rules for <c>POST /api/events</c> (spec sections 4, 5.1 and 9). Error keys are the JSON field paths.
/// Each kind and rule type requires its own fields and rejects the others, matching the CHECK constraints of section 10.
/// </summary>
public static class CreateEventValidator
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    /// <summary>Returns the validation errors by field. An empty dictionary means the request is valid.</summary>
    public static Dictionary<string, string[]> Validate(CreateEventRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                errors[field] = messages = [];
            }

            messages.Add(message);
        }

        void Require(object? value, string field)
        {
            if (value is null)
            {
                Add(field, "This field is required.");
            }
        }

        void Forbid(object? value, string field, string context)
        {
            if (value is not null)
            {
                Add(field, $"This field must not be set for {context}.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            Add("title", "Title is required.");
        }
        else if (request.Title.Length > TitleMaxLength)
        {
            Add("title", $"Title must be at most {TitleMaxLength} characters.");
        }

        if (request.Description is { Length: > DescriptionMaxLength })
        {
            Add("description", $"Description must be at most {DescriptionMaxLength} characters.");
        }

        switch (request.Kind)
        {
            case null:
                Add("kind", "Kind is required.");
                break;

            case EventKind.Single:
                Require(request.StartDate, "startDate");
                Require(request.StartTime, "startTime");
                Require(request.EndDate, "endDate");
                Require(request.EndTime, "endTime");
                Forbid(request.Recurrence, "recurrence", "a single event");

                // Compared in UTC, so a start in the DST gap is judged at its shifted time.
                if (request is { StartDate: { } startDate, StartTime: { } startTime, EndDate: { } endDate, EndTime: { } endTime }
                    && HelsinkiTime.ToUtc(endDate, endTime) <= HelsinkiTime.ToUtc(startDate, startTime))
                {
                    Add("endTime", "End must be after start.");
                }

                break;

            case EventKind.AllDay:
                Require(request.StartDate, "startDate");
                Require(request.EndDate, "endDate");
                Forbid(request.StartTime, "startTime", "an all-day event");
                Forbid(request.EndTime, "endTime", "an all-day event");
                Forbid(request.Recurrence, "recurrence", "an all-day event");

                if (request.EndDate < request.StartDate)
                {
                    Add("endDate", "End date must not be before the start date.");
                }

                break;

            case EventKind.Recurring:
                Require(request.StartDate, "startDate");
                Require(request.Recurrence, "recurrence");
                Forbid(request.EndDate, "endDate", "a recurring event");

                if (request.StartTime is null != request.EndTime is null)
                {
                    Add(request.StartTime is null ? "startTime" : "endTime", "Start time and end time must both be set for a timed series, or both be empty for an all-day series.");
                }
                else if (request.EndTime <= request.StartTime)
                {
                    Add("endTime", "End time must be after start time on the same day.");
                }

                if (request.Recurrence is { } recurrence)
                {
                    ValidateRecurrence(recurrence, request.StartDate, Add, Require, Forbid);
                }

                break;

            default:
                Add("kind", "Kind is not a known value.");
                break;
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    private static void ValidateRecurrence(
        CreateEventRecurrence recurrence,
        DateOnly? startDate,
        Action<string, string> add,
        Action<object?, string> require,
        Action<object?, string, string> forbid)
    {
        require(recurrence.UntilDate, "recurrence.untilDate");

        if (recurrence.UntilDate < startDate)
        {
            add("recurrence.untilDate", "Until date must not be before the start date.");
        }

        switch (recurrence.RuleType)
        {
            case null:
                add("recurrence.ruleType", "Rule type is required.");
                break;

            case RuleType.EveryDays:
                require(recurrence.Interval, "recurrence.interval");
                forbid(recurrence.DayOfMonth, "recurrence.dayOfMonth", "an EveryDays rule");
                forbid(recurrence.Month, "recurrence.month", "an EveryDays rule");
                forbid(recurrence.DayOfWeek, "recurrence.dayOfWeek", "an EveryDays rule");

                if (recurrence.Interval < 1)
                {
                    add("recurrence.interval", "Interval must be at least 1.");
                }

                break;

            case RuleType.MonthDay:
                require(recurrence.DayOfMonth, "recurrence.dayOfMonth");
                forbid(recurrence.Interval, "recurrence.interval", "a MonthDay rule");
                forbid(recurrence.Month, "recurrence.month", "a MonthDay rule");
                forbid(recurrence.DayOfWeek, "recurrence.dayOfWeek", "a MonthDay rule");

                if (recurrence.DayOfMonth is < 1 or > 31)
                {
                    add("recurrence.dayOfMonth", "Day of month must be between 1 and 31.");
                }

                break;

            case RuleType.WeekDay:
                require(recurrence.DayOfWeek, "recurrence.dayOfWeek");
                forbid(recurrence.Interval, "recurrence.interval", "a WeekDay rule");
                forbid(recurrence.DayOfMonth, "recurrence.dayOfMonth", "a WeekDay rule");
                forbid(recurrence.Month, "recurrence.month", "a WeekDay rule");

                if (recurrence.DayOfWeek is { } dayOfWeek && !Enum.IsDefined(dayOfWeek))
                {
                    add("recurrence.dayOfWeek", "Day of week is not a known value.");
                }

                break;

            case RuleType.YearDay:
                require(recurrence.Month, "recurrence.month");
                require(recurrence.DayOfMonth, "recurrence.dayOfMonth");
                forbid(recurrence.Interval, "recurrence.interval", "a YearDay rule");
                forbid(recurrence.DayOfWeek, "recurrence.dayOfWeek", "a YearDay rule");

                if (recurrence.Month is < 1 or > 12)
                {
                    add("recurrence.month", "Month must be between 1 and 12.");
                }
                else if (recurrence is { Month: { } month, DayOfMonth: { } day } && (day < 1 || day > DateTime.DaysInMonth(2024, month)))
                {
                    // A leap year is used so Feb 29 passes; days that never exist (Feb 30, Apr 31) are rejected (spec section 5.1).
                    add("recurrence.dayOfMonth", "Day of month does not exist in that month.");
                }

                break;

            default:
                add("recurrence.ruleType", "Rule type is not a known value.");
                break;
        }
    }
}
