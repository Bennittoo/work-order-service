using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Application.Validations;
using WorkOrderService.Domain;

namespace WorkOrderService.Application.Persistence.Configurations;

/// <summary>Maps <see cref="WorkOrder"/> to the WorkOrders table.</summary>
public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");

        builder.HasKey(workOrder => workOrder.Id);
        builder.Property(workOrder => workOrder.Id).ValueGeneratedNever();

        builder.Property(workOrder => workOrder.ExternalId).IsRequired().HasMaxLength(FieldLengths.ExternalId);
        builder.HasIndex(workOrder => workOrder.ExternalId).IsUnique();

        builder.Property(workOrder => workOrder.SiteCode).IsRequired().HasMaxLength(FieldLengths.SiteCode);
        builder.HasIndex(workOrder => workOrder.SiteCode);

        builder.Property(workOrder => workOrder.Description).IsRequired().HasMaxLength(FieldLengths.Description);

        // Stored as text rather than an ordinal, so the table is readable and renumbering the enum
        // cannot silently reinterpret existing rows.
        builder.Property(workOrder => workOrder.Status)
            .IsRequired()
            .HasMaxLength(FieldLengths.StatusName)
            .HasConversion<string>();
        builder.HasIndex(workOrder => workOrder.Status);

        builder.Property(workOrder => workOrder.CreatedAt).IsRequired();
        builder.Property(workOrder => workOrder.UpdatedAt).IsRequired();

        builder.Property(workOrder => workOrder.RowVersion).IsRowVersion();

        builder.HasMany(workOrder => workOrder.StatusHistory)
            .WithOne()
            .HasForeignKey(entry => entry.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // The navigation is read-only, so EF has to populate the backing field directly rather than
        // through the property. This is what lets the domain keep its encapsulation.
        builder.Metadata
            .FindNavigation(nameof(WorkOrder.StatusHistory))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
