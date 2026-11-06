namespace CalendarPlaybook.Api.Recurrence;

/// <summary>
/// Converts between Europe/Helsinki wall-clock time and UTC (spec sections 5.4 and 6).
/// </summary>
public static class HelsinkiTime
{
    public const string ZoneId = "Europe/Helsinki";

    /// <summary>The Windows id of the same zone, for hosts without ICU, where IANA ids cannot be resolved.</summary>
    public const string WindowsZoneId = "FLE Standard Time";

    private static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsZoneId);
        }
    }

    /// <summary>
    /// Returns the UTC instant of a local date and time.
    /// A time in the spring DST gap moves forward by the gap length (03:30 → 04:30).
    /// A time in the autumn repeated hour uses the earlier instant (summer offset).
    /// </summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (Zone.IsInvalidTime(local))
        {
            // The offset in force before the gap; a day earlier is safely outside it.
            var offsetBefore = Zone.GetUtcOffset(local.AddDays(-1));
            return DateTime.SpecifyKind(local - offsetBefore, DateTimeKind.Utc);
        }

        if (Zone.IsAmbiguousTime(local))
        {
            var largestOffset = Zone.GetAmbiguousTimeOffsets(local).Max();
            return DateTime.SpecifyKind(local - largestOffset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    /// <summary>Returns the local date and time of a UTC instant. An unspecified kind is treated as UTC.</summary>
    public static (DateOnly Date, TimeOnly Time) ToLocal(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        return (DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
    }
}
