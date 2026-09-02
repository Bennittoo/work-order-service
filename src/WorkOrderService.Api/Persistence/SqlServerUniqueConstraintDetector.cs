using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkOrderService.Application.Abstractions;

namespace WorkOrderService.Api.Persistence;

/// <summary>
/// Reads SQL Server error numbers to recognise a unique key violation. The SQL Server adapter for
/// the port the application layer depends on.
/// </summary>
public sealed class SqlServerUniqueConstraintDetector : IUniqueConstraintDetector
{
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    /// <inheritdoc />
    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is UniqueIndexViolation or PrimaryKeyViolation;
}
