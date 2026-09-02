using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WorkOrderService.Api.Security;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Persistence;

namespace WorkOrderService.Api.Tests;

/// <summary>
/// Runs the real application, swapping only the database provider and the one service that depends
/// on which provider is in use.
/// </summary>
/// <remarks>
/// A temporary SQLite file rather than an in-memory database. In-memory SQLite lives inside a single
/// open connection, and this service has two concurrent writers by design: the request path and the
/// background processor. Sharing one connection between them produces "database is locked" rather
/// than the behaviour under test. A file gives each context its own connection, real lock handling
/// through the busy timeout, and WAL so a reader is not blocked by the worker mid-write.
/// The remaining divergences from SQL Server are listed in the README; the important one is that
/// SQLite has no rowversion, so concurrency conflicts are not exercised here.
/// </remarks>
public sealed class WorkOrderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"work-order-tests-{Guid.NewGuid():N}.db");

    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>Set through configuration below, so the tests do not depend on the committed value.</summary>
    public const string ApiKey = "integration-test-key";

    private readonly string _connectionString;

    public WorkOrderApiFactory() =>
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            DefaultTimeout = 30
        }.ToString();

    public WorkOrderDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseSqlite(_connectionString)
            .Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Replaced below, so the value is never used. It only stops the real registration from
        // needing a SQL Server to exist on whatever machine runs the tests.
        builder.UseSetting("ConnectionStrings:WorkOrders", "Server=unused;Database=unused;");
        builder.UseSetting($"{ApiKeyOptions.SectionName}:{nameof(ApiKeyOptions.Value)}", ApiKey);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WorkOrderDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<WorkOrderDbContext>();
            services.AddDbContext<WorkOrderDbContext>(options => options.UseSqlite(_connectionString));

            services.RemoveAll<IUniqueConstraintDetector>();
            services.AddSingleton<IUniqueConstraintDetector, SqliteUniqueConstraintDetector>();
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(ApiKeyHeader, ApiKey);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        // Built from the model rather than by running the migration, because the migration is SQL
        // Server specific. The consequence, noted in the README, is that these tests prove the model
        // works and not that the migration does.
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

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
