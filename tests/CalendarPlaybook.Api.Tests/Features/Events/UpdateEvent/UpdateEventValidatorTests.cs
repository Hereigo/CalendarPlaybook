using CalendarPlaybook.Api.Features.Events.UpdateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Tests.Features.Events.UpdateEvent;

public class UpdateEventValidatorTests
{
    [Fact]
    public void ValidAllDay_HasNoErrors()
    {
        var request = new UpdateEventRequest("Trip", null, EventKind.AllDay, new DateOnly(2027, 3, 28), null, new DateOnly(2027, 3, 30), null, null);

        Assert.Empty(UpdateEventValidator.Validate(request));
    }

    [Fact]
    public void MissingTitleAndKind_AreReportedByFieldName()
    {
        var errors = UpdateEventValidator.Validate(new UpdateEventRequest(" ", null, null, null, null, null, null, null));

        Assert.Contains("title", errors.Keys);
        Assert.Contains("kind", errors.Keys);
    }

    [Fact]
    public void RecurrenceErrors_UseNestedFieldPaths()
    {
        var request = new UpdateEventRequest(
            "Forever", null, EventKind.Recurring, new DateOnly(2027, 1, 4), null, null, null,
            new UpdateEventRecurrence(RuleType.EveryDays, 0, null, null, null, null));

        var errors = UpdateEventValidator.Validate(request);

        Assert.Contains("recurrence.untilDate", errors.Keys);
        Assert.Contains("recurrence.interval", errors.Keys);
    }
}
