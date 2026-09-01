using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WorkOrderService.Api.Persistence.Configurations;

public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents");

        // The primary key is the deduplication guard, so it has to be enforced by the database rather
        // than by a read-then-write in application code. Non-clustered because the key is a random
        // GUID: clustering on it would fragment an append-only table on every insert.
        builder.HasKey(e => e.EventId).IsClustered(false);
        builder.Property(e => e.EventId).ValueGeneratedNever();

        builder.HasIndex(e => e.ProcessedAt).IsClustered();

        builder.Property(e => e.WorkOrderExternalId).IsRequired().HasMaxLength(FieldLengths.ExternalId);
        builder.HasIndex(e => e.WorkOrderId);

        builder.Property(e => e.Outcome).IsRequired().HasMaxLength(FieldLengths.OutcomeName).HasConversion<string>();
        builder.Property(e => e.Detail).HasMaxLength(FieldLengths.Details);

        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();
    }
}
