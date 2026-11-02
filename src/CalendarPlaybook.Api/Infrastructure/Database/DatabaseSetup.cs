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

    /// <summary>Applies pending migrations on startup in Development only. Production uses an explicit step (spec section 10).</summary>
    public static void MigrateInDevelopment(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CalendarDbContext>().Database.Migrate();
    }
}
