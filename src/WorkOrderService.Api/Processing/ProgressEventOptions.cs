namespace WorkOrderService.Api.Processing;

public sealed class ProgressEventOptions
{
    public const string SectionName = "ProgressEvents";

    /// <summary>
    /// How many accepted events may be waiting at once. Bounded on purpose: an unbounded queue does
    /// not remove backpressure, it converts it into memory growth and a longer window in which a
    /// restart loses work.
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>
    /// How many times one event is reprocessed after losing a concurrency race. Transient database
    /// faults are handled separately by the EF Core execution strategy.
    /// </summary>
    public int MaxProcessingAttempts { get; set; } = 3;
}
