using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CalendarPlaybook.Api.Infrastructure.Events;

/// <summary>Maps <see cref="Event"/> to the Events table (spec section 10).</summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    private const string NoRule =
        "[RuleType] IS NULL AND [Interval] IS NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL AND [UntilDate] IS NULL";

    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", table =>
        {
            table.HasCheckConstraint(
                "CK_Events_Kind",
                "([Kind] = 'Single'"
                + " AND [StartUtc] IS NOT NULL AND [EndUtc] IS NOT NULL"
                + " AND [StartDate] IS NULL AND [EndDate] IS NULL AND [StartLocalTime] IS NULL AND [EndLocalTime] IS NULL"
                + $" AND {NoRule})"
                + " OR ([Kind] = 'AllDay'"
                + " AND [StartDate] IS NOT NULL AND [EndDate] IS NOT NULL"
                + " AND [StartUtc] IS NULL AND [EndUtc] IS NULL AND [StartLocalTime] IS NULL AND [EndLocalTime] IS NULL"
                + $" AND {NoRule})"
                + " OR ([Kind] = 'Recurring'"
                + " AND [StartDate] IS NOT NULL AND [RuleType] IS NOT NULL AND [UntilDate] IS NOT NULL"
                + " AND [StartUtc] IS NULL AND [EndUtc] IS NULL AND [EndDate] IS NULL"
                + " AND (([StartLocalTime] IS NULL AND [EndLocalTime] IS NULL) OR ([StartLocalTime] IS NOT NULL AND [EndLocalTime] IS NOT NULL)))");

            table.HasCheckConstraint(
                "CK_Events_RuleType",
                "[RuleType] IS NULL"
                + " OR ([RuleType] = 'EveryDays' AND [Interval] IS NOT NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL)"
                + " OR ([RuleType] = 'MonthDay' AND [DayOfMonth] IS NOT NULL AND [Interval] IS NULL AND [Month] IS NULL AND [DayOfWeek] IS NULL)"
                + " OR ([RuleType] = 'WeekDay' AND [DayOfWeek] IS NOT NULL AND [Interval] IS NULL AND [DayOfMonth] IS NULL AND [Month] IS NULL)"
                + " OR ([RuleType] = 'YearDay' AND [Month] IS NOT NULL AND [DayOfMonth] IS NOT NULL AND [Interval] IS NULL AND [DayOfWeek] IS NULL)");

            table.HasCheckConstraint(
                "CK_Events_DayOfWeek",
                "[DayOfWeek] IS NULL OR [DayOfWeek] IN ('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.OwnerId).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);

        builder.Property(e => e.StartUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.EndUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.StartDate).HasColumnType("date");
        builder.Property(e => e.EndDate).HasColumnType("date");
        builder.Property(e => e.StartLocalTime).HasColumnType("time");
        builder.Property(e => e.EndLocalTime).HasColumnType("time");
        builder.Property(e => e.TimeZone).HasMaxLength(64).IsRequired();

        builder.Property(e => e.RuleType).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.DayOfWeek).HasConversion<string>().HasMaxLength(9);
        builder.Property(e => e.UntilDate).HasColumnType("date");

        builder.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasIndex(e => new { e.OwnerId, e.StartUtc });
        builder.HasIndex(e => new { e.OwnerId, e.StartDate });
    }
}
