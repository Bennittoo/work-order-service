using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Application.Validations;

namespace WorkOrderService.Application.Persistence.Configurations;

/// <summary>Maps <see cref="ProcessedEvent"/> to the ProcessedEvents table.</summary>
public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents");

        // The primary key is the deduplication guard, so it has to be enforced by the database rather
        // than by a read-then-write in application code. Non-clustered because the key is a random
        // GUID: clustering on it would fragment an append-only table on every insert.
        builder.HasKey(processed => processed.EventId).IsClustered(false);
        builder.Property(processed => processed.EventId).ValueGeneratedNever();

        builder.HasIndex(processed => processed.ProcessedAt).IsClustered();

        builder.Property(processed => processed.WorkOrderExternalId)
            .IsRequired()
            .HasMaxLength(FieldLengths.ExternalId);
        builder.HasIndex(processed => processed.WorkOrderId);

        builder.Property(processed => processed.Outcome)
            .IsRequired()
            .HasMaxLength(FieldLengths.OutcomeName)
            .HasConversion<string>();
        builder.Property(processed => processed.Detail).HasMaxLength(FieldLengths.Details);

        builder.Property(processed => processed.OccurredAt).IsRequired();
        builder.Property(processed => processed.ProcessedAt).IsRequired();
    }
}
