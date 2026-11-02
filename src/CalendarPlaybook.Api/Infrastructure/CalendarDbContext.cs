using CalendarPlaybook.Api.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace CalendarPlaybook.Api.Infrastructure;

public class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);
    }
}
