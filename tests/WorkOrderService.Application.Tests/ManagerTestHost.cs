using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using WorkOrderService.Application.Managers;
using WorkOrderService.Application.Persistence;

namespace WorkOrderService.Application.Tests;

/// <summary>
/// Builds managers over a throwaway SQLite database.
/// </summary>
/// <remarks>
/// A temporary file rather than in-memory, for the same reason the API tests use one: in-memory
/// SQLite lives inside a single connection, and a test that needs two contexts against the same data
/// then contends with itself.
/// </remarks>
public sealed class ManagerTestHost : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"work-order-manager-tests-{Guid.NewGuid():N}.db");

    private readonly string _connectionString;

    public ManagerTestHost()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            DefaultTimeout = 30
        }.ToString();

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    /// <summary>A fixed clock, so recorded timestamps are predictable.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 2, 8, 0, 0, TimeSpan.Zero));

    /// <summary>A fresh context over the same database, for asserting on what was written.</summary>
    public WorkOrderDbContext NewContext() =>
        new(new DbContextOptionsBuilder<WorkOrderDbContext>().UseSqlite(_connectionString).Options);

    /// <summary>
    /// A manager with its own context, mirroring how the application resolves one per scope.
    /// </summary>
    public WorkOrderServiceManager NewManager() =>
        new(NewContext(),
            new SqliteUniqueConstraintDetector(),
            Clock,
            NullLogger<WorkOrderServiceManager>.Instance);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A leftover temporary file is not worth failing a test run over.
            }
        }
    }
}
