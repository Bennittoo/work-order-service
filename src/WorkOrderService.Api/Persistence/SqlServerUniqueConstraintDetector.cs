using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace WorkOrderService.Api.Persistence;

public sealed class SqlServerUniqueConstraintDetector : IUniqueConstraintDetector
{
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is UniqueIndexViolation or PrimaryKeyViolation;
}
