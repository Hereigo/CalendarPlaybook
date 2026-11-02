using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Infrastructure.Database;

public static class DatabaseSetup
{
    /// <summary>
    /// Registers <see cref="CalendarDbContext"/> on SQL Server. The connection string <c>ConnectionStrings:Calendar</c>
    /// comes from environment variables or user secrets, never from committed config.
    /// </summary>
    public static IServiceCollection AddCalendarDb(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddDbContext<CalendarDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Calendar")));
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
