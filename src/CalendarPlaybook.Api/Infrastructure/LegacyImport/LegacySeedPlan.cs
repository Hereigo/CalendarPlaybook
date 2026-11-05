using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure.Events;

namespace CalendarPlaybook.Api.Infrastructure.LegacyImport;

/// <summary>The entities to insert for one owner, plus counts of the rows left out. Titles are never included.</summary>
public sealed record LegacySeedPlan(IReadOnlyList<Event> Entities, int Skipped, int Invalid, int Rows)
{
    /// <summary>
    /// Maps every legacy row with <see cref="LegacyEventMapper"/>, checks it with <see cref="CreateEventValidator"/>
    /// and builds the entity with <see cref="CreateEventHandler.ToEntity"/>. Pure: no database or file access.
    /// </summary>
    public static LegacySeedPlan Build(IEnumerable<LegacyCalEvent> rows, string ownerId, DateTime utcNow)
    {
        var entities = new List<Event>();
        var skipped = 0;
        var invalid = 0;
        var count = 0;

        foreach (var row in rows)
        {
            count++;
            var mapping = LegacyEventMapper.Map(row, ownerId);

            if (mapping.Request is not { } request)
            {
                skipped++;
                continue;
            }

            if (CreateEventValidator.Validate(request).Count > 0)
            {
                invalid++;
                continue;
            }

            var entity = CreateEventHandler.ToEntity(request, ownerId, utcNow);
            entity.Id = mapping.Id;
            entity.CreatedAtUtc = mapping.CreatedAtUtc;
            entity.UpdatedAtUtc = mapping.UpdatedAtUtc;
            entities.Add(entity);
        }

        return new LegacySeedPlan(entities, skipped, invalid, count);
    }
}
