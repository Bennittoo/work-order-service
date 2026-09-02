using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkOrderService.Application.Abstractions;

namespace WorkOrderService.Application.Tests;

/// <summary>
/// The SQLite adapter for the unique constraint port. Its existence is the point of the port:
/// recognising a unique key violation is provider specific, so the tests swap the interpretation
/// rather than the logic that depends on it.
/// </summary>
public sealed class SqliteUniqueConstraintDetector : IUniqueConstraintDetector
{
    private const int ConstraintPrimaryKey = 1555;
    private const int ConstraintUnique = 2067;

    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException sqlite
        && sqlite.SqliteExtendedErrorCode is ConstraintPrimaryKey or ConstraintUnique;
}
