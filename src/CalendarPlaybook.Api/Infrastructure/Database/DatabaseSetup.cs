using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Infrastructure.Database;

public static class DatabaseSetup
{
    /// <summary>The configuration key under <c>ConnectionStrings</c>: <c>ConnectionStrings:Calendar</c>.</summary>
    public const string ConnectionStringName = "Calendar";

    /// <summary>
    /// Registers <see cref="CalendarDbContext"/> on SQL Server. The connection string <c>ConnectionStrings:Calendar</c>
    /// comes from environment variables or user secrets, never from committed config.
    /// </summary>
    public static IServiceCollection AddCalendarDb(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddDbContext<CalendarDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Calendar is not set. Set it with " +
                    "`dotnet user-secrets set ConnectionStrings:Calendar \"<connection string>\" --project src/CalendarPlaybook.Api` " +
                    "or the ConnectionStrings__Calendar environment variable.");
            }

            options.UseSqlServer(connectionString);
        });
    }

    /// <summary>
    /// Development only: starts the local SQL Server container (if configured) and applies pending migrations.
    /// Production uses an explicit migration step (spec section 10).
    /// </summary>
    public static void MigrateInDevelopment(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        DevSqlContainer.EnsureRunning(app);

        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CalendarDbContext>().Database.Migrate();
    }
}
