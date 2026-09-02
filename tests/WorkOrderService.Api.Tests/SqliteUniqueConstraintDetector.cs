using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkOrderService.Application.Abstractions;
using WorkOrderService.Application.Persistence;

namespace WorkOrderService.Api.Tests;

/// <summary>
/// The test counterpart to <see cref="SqlServerUniqueConstraintDetector"/>. Its existence is the
/// point of the abstraction: recognising a unique key violation is provider specific, so the tests
/// swap the interpretation rather than the logic that depends on it.
/// </summary>
public sealed class SqliteUniqueConstraintDetector : IUniqueConstraintDetector
{
    private const int ConstraintPrimaryKey = 1555;
    private const int ConstraintUnique = 2067;

    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException sqlite
        && sqlite.SqliteExtendedErrorCode is ConstraintPrimaryKey or ConstraintUnique;
}
