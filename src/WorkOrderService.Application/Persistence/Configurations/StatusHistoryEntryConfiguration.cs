using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Application.Validations;
using WorkOrderService.Domain;

namespace WorkOrderService.Application.Persistence.Configurations;

/// <summary>Maps <see cref="StatusHistoryEntry"/> to the WorkOrderStatusHistory table.</summary>
public sealed class StatusHistoryEntryConfiguration : IEntityTypeConfiguration<StatusHistoryEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StatusHistoryEntry> builder)
    {
        builder.ToTable("WorkOrderStatusHistory");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.FromStatus)
            .HasMaxLength(FieldLengths.StatusName)
            .HasConversion<string>();
        builder.Property(entry => entry.ToStatus)
            .IsRequired()
            .HasMaxLength(FieldLengths.StatusName)
            .HasConversion<string>();
        builder.Property(entry => entry.Source)
            .IsRequired()
            .HasMaxLength(FieldLengths.StatusName)
            .HasConversion<string>();

        builder.Property(entry => entry.Details).HasMaxLength(FieldLengths.Details);

        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.Property(entry => entry.RecordedAt).IsRequired();

        // Serves the only read this table has: one work order's trail, in the order it happened.
        builder.HasIndex(entry => new { entry.WorkOrderId, entry.RecordedAt });

        builder.HasIndex(entry => entry.EventId);
    }
}
