namespace CalendarPlaybook.Api.Infrastructure.LegacyImport;

/// <summary>Repeat mode of the old Calendarium app. Stored as a number in the backup.</summary>
public enum LegacyRepeat
{
    Once,
    Yearly,
    Monthly,
    EveryXdays,
}

/// <summary>Status of the old Calendarium app. Stored as a number in the backup.</summary>
public enum LegacyStatus
{
    Active,
    Disabled,
    Deleted,
}

/// <summary>
/// One row of the old <c>CalEvents</c> table as found in <c>database_backup.json</c>.
/// <see cref="Started"/> is the anchor date; <see cref="Day"/>, <see cref="Month"/> and <see cref="Year"/> are derived from it
/// (Month is 0 for monthly events, Year is 0 for yearly events). Dates and times are local Europe/Helsinki values.
/// </summary>
public record LegacyCalEvent(
    int Id,
    int Day,
    int Month,
    int Year,
    int? EveryXDays,
    LegacyRepeat Repeat,
    LegacyStatus Status,
    DateTime? Created,
    DateTime Modified,
    DateTime Started,
    TimeSpan Time,
    string? Description);
