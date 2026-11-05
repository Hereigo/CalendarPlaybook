// One-off import of old Calendarium events (database_backup.json) for one owner.
// Usage: dotnet run --project tools/CalendarPlaybook.LegacyImport -- --file <path> --owner <auth0 sub> [--dry-run]
// The connection string is ConnectionStrings:someed from the Api's user secrets or the environment.
// Titles and descriptions are never printed (CLAUDE.md: logs must not contain them).

using System.Text.Json;
using CalendarPlaybook.Api.Features.Events.CreateEvent;
using CalendarPlaybook.Api.Infrastructure;
using CalendarPlaybook.Api.Infrastructure.Database;
using CalendarPlaybook.Api.Infrastructure.Events;
using CalendarPlaybook.Api.Infrastructure.LegacyImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

string? file = null;
string? owner = null;
var dryRun = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--file" when i + 1 < args.Length:
            file = args[++i];
            break;
        case "--owner" when i + 1 < args.Length:
            owner = args[++i];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        default:
            return Usage($"Unknown or incomplete argument '{args[i]}'.");
    }
}

if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(owner))
{
    return Usage("--file and --owner are required.");
}

if (!File.Exists(file))
{
    return Usage($"File not found: {file}");
}

var rows = JsonSerializer.Deserialize<List<LegacyCalEvent>>(
    await File.ReadAllTextAsync(file),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

var utcNow = DateTime.UtcNow;
var mapped = rows.Select(row => LegacyEventMapper.Map(row, owner)).ToList();

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(CalendarDbContext).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var hasDatabase = !string.IsNullOrWhiteSpace(configuration.GetConnectionString(DatabaseSetup.ConnectionStringName));
if (!hasDatabase && !dryRun)
{
    return Usage($"ConnectionStrings:{DatabaseSetup.ConnectionStringName} is not set (Api user secrets or ConnectionStrings__{DatabaseSetup.ConnectionStringName}).");
}

await using var services = new ServiceCollection().AddCalendarDb(configuration).BuildServiceProvider();
await using var scope = services.CreateAsyncScope();
CalendarDbContext? db = hasDatabase ? scope.ServiceProvider.GetRequiredService<CalendarDbContext>() : null;

HashSet<Guid> existing = [];
if (db is not null)
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Console.Error.WriteLine($"The database has {pending.Count} pending migration(s). Run the Api once (Development applies them) and retry.");
        return 2;
    }

    var ids = mapped.Select(m => m.Id).ToList();
    existing = (await db.Events.Where(e => e.OwnerId == owner && ids.Contains(e.Id)).Select(e => e.Id).ToListAsync()).ToHashSet();
}
else
{
    Console.WriteLine("No connection string: dry run without checking which rows already exist.");
}

var toInsert = new List<Event>();
int skipped = 0, invalid = 0, present = 0;

foreach (var m in mapped)
{
    var prefix = $"{m.LegacyId,6} -> {m.Id}";
    var warnings = m.Warnings.Count > 0 ? $"  [{string.Join("; ", m.Warnings)}]" : "";

    if (m.Request is not { } request)
    {
        skipped++;
        Console.WriteLine($"{prefix}  SKIPPED: {m.SkipReason}{warnings}");
        continue;
    }

    var errors = CreateEventValidator.Validate(request);
    if (errors.Count > 0)
    {
        invalid++;
        var details = string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}"));
        Console.WriteLine($"{prefix}  INVALID: {details}{warnings}");
        continue;
    }

    var time = request.StartTime is { } start ? $"{start:HH\\:mm}-{request.EndTime:HH\\:mm}" : "all-day";
    var rule = request.Recurrence is { } r ? $"{r.RuleType}({r.Interval ?? r.DayOfMonth}{(r.Month is { } mo ? $"/{mo}" : "")})" : "-";
    var summary = $"{prefix}  {request.Kind,-9} {rule,-15} {request.StartDate:yyyy-MM-dd} {time,-11}";

    if (existing.Contains(m.Id))
    {
        present++;
        Console.WriteLine($"{summary} already present{warnings}");
        continue;
    }

    var entity = CreateEventHandler.ToEntity(request, owner, utcNow);
    entity.Id = m.Id;
    entity.CreatedAtUtc = m.CreatedAtUtc;
    entity.UpdatedAtUtc = m.UpdatedAtUtc;
    toInsert.Add(entity);
    Console.WriteLine($"{summary} {(dryRun ? "would import" : "import")}{warnings}");
}

if (!dryRun && db is not null && toInsert.Count > 0)
{
    db.Events.AddRange(toInsert);
    await db.SaveChangesAsync();
}

Console.WriteLine();
Console.WriteLine($"Rows: {rows.Count}. {(dryRun ? "Would import" : "Imported")}: {toInsert.Count}. Already present: {present}. Skipped: {skipped}. Invalid: {invalid}.");
return invalid > 0 ? 1 : 0;

static int Usage(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("Usage: CalendarPlaybook.LegacyImport --file <database_backup.json> --owner <auth0 sub> [--dry-run]");
    return 64;
}
