using Microsoft.EntityFrameworkCore;

namespace WorkOrderService.Api.Persistence;

/// <summary>
/// Recognising a unique key violation means reading a provider specific error code, and the
/// integration tests run on a different provider from the application. Keeping that behind an
/// abstraction is what lets both use the same insert-and-catch logic.
/// </summary>
public interface IUniqueConstraintDetector
{
    bool IsUniqueViolation(DbUpdateException exception);
}
