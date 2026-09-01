using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Persistence.Configurations;

public sealed class StatusHistoryEntryConfiguration : IEntityTypeConfiguration<StatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<StatusHistoryEntry> builder)
    {
        builder.ToTable("WorkOrderStatusHistory");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.FromStatus).HasMaxLength(FieldLengths.StatusName).HasConversion<string>();
        builder.Property(h => h.ToStatus).IsRequired().HasMaxLength(FieldLengths.StatusName).HasConversion<string>();
        builder.Property(h => h.Source).IsRequired().HasMaxLength(FieldLengths.StatusName).HasConversion<string>();

        builder.Property(h => h.Details).HasMaxLength(FieldLengths.Details);

        builder.Property(h => h.OccurredAt).IsRequired();
        builder.Property(h => h.RecordedAt).IsRequired();

        // Serves the only read this table has: one work order's trail, in the order it happened.
        builder.HasIndex(h => new { h.WorkOrderId, h.RecordedAt });

        builder.HasIndex(h => h.EventId);
    }
}
