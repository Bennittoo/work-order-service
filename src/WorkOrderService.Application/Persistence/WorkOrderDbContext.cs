using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WorkOrderService.Domain;

namespace WorkOrderService.Application.Persistence;

/// <summary>
/// The unit of work for this service. Used directly rather than behind a repository, because
/// <see cref="DbContext"/> already is one and wrapping it would add a layer to maintain and mock for
/// no gain.
/// </summary>
public sealed class WorkOrderDbContext : DbContext
{
    /// <summary>Creates the context.</summary>
    /// <param name="options">Provider and connection configuration.</param>
    public WorkOrderDbContext(DbContextOptions<WorkOrderDbContext> options)
        : base(options)
    {
    }

    /// <summary>The work orders, and the aggregate root for their status trails.</summary>
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    /// <summary>The deduplication ledger, and a record of every event received and what was decided.</summary>
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    // Status history is intentionally not exposed as a DbSet. It is part of the work order aggregate
    // and is only ever reached through it, which keeps the append-only rule enforceable in one place
    // instead of wherever someone happens to hold a DbContext.

    /// <inheritdoc />
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
            .Property(workOrder => workOrder.RowVersion)
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
