using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Infrastructure.LegacyImport;

/// <summary>
/// Seeds the database from the old Calendarium <c>database_backup.json</c> on startup (after a deploy), but only when the
/// <c>Events</c> table has at most <see cref="MaxExistingRows"/> row. Configured by the <c>Seed:Legacy</c> section:
/// <c>Enabled</c>, <c>FilePath</c> (relative paths resolve against the app folder) and <c>OwnerId</c>.
/// The file is copied to the build output as <c>SeedData/database_backup.json</c> by the csproj; publish leaves it out.
/// The seed never fails startup: problems are logged as warnings and the app keeps running.
/// Logs carry counts only, never titles or descriptions (CLAUDE.md).
/// </summary>
public static class LegacyDataSeeder
{
    public const string ConfigSection = "Seed:Legacy";

    /// <summary>The seed runs when the table is empty or holds a single row (for example one test event).</summary>
    public const int MaxExistingRows = 1;

    public static bool ShouldSeed(int existingRows) => existingRows <= MaxExistingRows;

    /// <summary>Relative paths resolve against the app folder, not the working directory (IIS starts elsewhere).</summary>
    public static string ResolvePath(string filePath, string baseDirectory) =>
        Path.IsPathRooted(filePath) ? filePath : Path.GetFullPath(Path.Combine(baseDirectory, filePath));

    public static async Task SeedLegacyIfEmptyAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LegacyDataSeeder");
        var section = app.Configuration.GetSection(ConfigSection);

        if (!section.GetValue("Enabled", false))
        {
            logger.LogInformation("Legacy seed disabled ({Section}:Enabled is not true).", ConfigSection);
            return;
        }

        var filePath = section["FilePath"];
        var ownerId = section["OwnerId"];

        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(ownerId))
        {
            logger.LogWarning(
                "Legacy seed not configured. Set {Section}:FilePath and {Section}:OwnerId to enable it.", ConfigSection, ConfigSection);
            return;
        }

        var fullPath = ResolvePath(filePath, AppContext.BaseDirectory);

        try
        {
            await SeedAsync(app.Services, fullPath, ownerId, logger);
        }
        catch (DbUpdateException ex)
        {
            // Another instance seeded at the same time and the deterministic ids collided. Its rows are in place.
            logger.LogWarning(ex, "Legacy seed could not be saved; another instance may have seeded the database.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Legacy seed skipped: the file at {Path} could not be read.", fullPath);
        }
        catch (Exception ex)
        {
            // Most often the database is unreachable or has no schema yet. Startup must go on.
            logger.LogWarning(ex, "Legacy seed skipped: the database could not be queried.");
        }
    }

    private static async Task SeedAsync(IServiceProvider services, string filePath, string ownerId, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();

        // Migrations first: on a database without the schema, the Events query below would fail.
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogWarning("Legacy seed skipped: the database has pending migrations. Run the migration step first.");
            return;
        }

        var existing = await db.Events.CountAsync();
        if (!ShouldSeed(existing))
        {
            logger.LogInformation("Legacy seed skipped: the events table already has {Count} rows.", existing);
            return;
        }

        if (!File.Exists(filePath))
        {
            logger.LogWarning("Legacy seed skipped: the file {Path} does not exist.", filePath);
            return;
        }

        var rows = JsonSerializer.Deserialize<List<LegacyCalEvent>>(
            await File.ReadAllTextAsync(filePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var plan = LegacySeedPlan.Build(rows, ownerId, DateTime.UtcNow);

        // The single existing row may itself be a seeded one (deterministic id); never insert it twice.
        var ids = plan.Entities.Select(e => e.Id).ToList();
        var present = (await db.Events.Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync()).ToHashSet();
        var toInsert = plan.Entities.Where(e => !present.Contains(e.Id)).ToList();

        if (toInsert.Count == 0)
        {
            logger.LogInformation("Legacy seed: nothing to insert from {Rows} row(s).", plan.Rows);
            return;
        }

        db.Events.AddRange(toInsert);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Legacy seed imported {Imported} of {Rows} row(s). Already present: {Present}. Skipped: {Skipped}. Invalid: {Invalid}.",
            toInsert.Count, plan.Rows, present.Count, plan.Skipped, plan.Invalid);
    }
}
