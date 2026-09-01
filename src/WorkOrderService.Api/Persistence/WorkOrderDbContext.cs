using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Persistence;

public sealed class WorkOrderDbContext : DbContext
{
    public WorkOrderDbContext(DbContextOptions<WorkOrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    // Status history is intentionally not exposed as a DbSet. It is part of the work order
    // aggregate and is only ever reached through it, which keeps the append-only rule enforceable
    // in one place instead of wherever someone happens to hold a DbContext.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkOrderDbContext).Assembly);

        if (!Database.IsSqlServer())
        {
            ConfigureForSqlite(modelBuilder);
        }
    }

    /// <summary>
    /// Two things SQL Server provides that SQLite does not, both of which the integration tests hit.
    /// Confining them here keeps the divergence in one readable place instead of spreading provider
    /// checks through the configurations.
    /// </summary>
    private static void ConfigureForSqlite(ModelBuilder modelBuilder)
    {
        // No rowversion. Keep the column but stop EF expecting the database to fill it, which means
        // concurrency conflicts are only genuinely exercised against SQL Server.
        modelBuilder.Entity<WorkOrder>()
            .Property(w => w.RowVersion)
            .ValueGeneratedNever();

        // No DateTimeOffset either. SQLite stores one as text including its offset, so the text does
        // not sort chronologically and EF refuses to translate ORDER BY over it. UTC ticks do sort,
        // and every value this service writes is already UTC.
        var utcTicks = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcTicks,
            stored => new DateTimeOffset(stored, TimeSpan.Zero));

        foreach (var property in modelBuilder.Model
                     .GetEntityTypes()
                     .SelectMany(entityType => entityType.GetProperties())
                     .Where(property => property.ClrType == typeof(DateTimeOffset)))
        {
            property.SetValueConverter(utcTicks);
        }
    }
}
