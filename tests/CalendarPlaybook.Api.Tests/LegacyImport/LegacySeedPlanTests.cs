using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Infrastructure.LegacyImport;

namespace CalendarPlaybook.Api.Tests.LegacyImport;

public class LegacySeedPlanTests
{
    private const string Owner = "auth0|legacy-owner";
    private static readonly DateTime UtcNow = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

    private static LegacyCalEvent Row(int id, LegacyRepeat repeat, LegacyStatus status = LegacyStatus.Active) => new(
        Id: id,
        Day: 5,
        Month: repeat == LegacyRepeat.Monthly ? 0 : 3,
        Year: repeat == LegacyRepeat.Yearly ? 0 : 2024,
        EveryXDays: null,
        Repeat: repeat,
        Status: status,
        Created: null,
        Modified: new DateTime(2025, 7, 1, 12, 0, 0),
        Started: new DateTime(2024, 3, 5),
        Time: new TimeSpan(11, 11, 0),
        Description: "Secret title");

    [Fact]
    public void Build_MapsEachImportableRowToAnEntityOwnedBySeedOwner()
    {
        var plan = LegacySeedPlan.Build([Row(1, LegacyRepeat.Once), Row(2, LegacyRepeat.Monthly)], Owner, UtcNow);

        Assert.Equal(2, plan.Entities.Count);
        Assert.All(plan.Entities, e => Assert.Equal(Owner, e.OwnerId));
        Assert.Equal(2, plan.Rows);
        Assert.Equal(0, plan.Skipped);
        Assert.Equal(0, plan.Invalid);
    }

    [Fact]
    public void Build_UsesDeterministicIds()
    {
        var first = LegacySeedPlan.Build([Row(7, LegacyRepeat.Once)], Owner, UtcNow).Entities.Single();
        var second = LegacySeedPlan.Build([Row(7, LegacyRepeat.Once)], Owner, UtcNow).Entities.Single();

        Assert.Equal(LegacyEventMapper.DeterministicId(Owner, 7), first.Id);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void Build_SkipsInactiveRowsAndCountsThem()
    {
        var plan = LegacySeedPlan.Build(
            [Row(1, LegacyRepeat.Once), Row(2, LegacyRepeat.Once, LegacyStatus.Deleted)],
            Owner,
            UtcNow);

        Assert.Single(plan.Entities);
        Assert.Equal(1, plan.Skipped);
        Assert.Equal(2, plan.Rows);
    }

    [Fact]
    public void Build_UsesTheGivenTimestampForRowsWithoutOwnDates()
    {
        var entity = LegacySeedPlan.Build([Row(3, LegacyRepeat.Yearly)], Owner, UtcNow).Entities.Single();

        Assert.Equal(EventKind.Recurring, entity.Kind);
        Assert.Equal(new DateTime(2025, 7, 1, 9, 0, 0, DateTimeKind.Utc), entity.UpdatedAtUtc);
    }

    [Fact]
    public void Build_WithNoRows_ReturnsEmptyPlan()
    {
        var plan = LegacySeedPlan.Build([], Owner, UtcNow);

        Assert.Empty(plan.Entities);
        Assert.Equal(0, plan.Rows);
    }
}
