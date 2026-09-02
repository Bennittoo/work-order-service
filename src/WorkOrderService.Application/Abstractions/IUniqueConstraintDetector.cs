using Microsoft.EntityFrameworkCore;

namespace WorkOrderService.Application.Abstractions;

/// <summary>
/// Recognises whether a failed save was a unique key violation.
/// </summary>
/// <remarks>
/// The answer depends on a provider specific error code, and the integration tests run on a
/// different provider from the application. Keeping it behind a port is what lets both share the
/// same insert-and-catch logic instead of duplicating it.
/// </remarks>
public interface IUniqueConstraintDetector
{
    /// <summary>Whether the exception was caused by a unique key or primary key violation.</summary>
    /// <param name="exception">The exception thrown by a failed save.</param>
    bool IsUniqueViolation(DbUpdateException exception);
}
