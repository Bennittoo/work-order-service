using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Domain;

namespace WorkOrderService.Api.Persistence.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.ExternalId).IsRequired().HasMaxLength(FieldLengths.ExternalId);
        builder.HasIndex(w => w.ExternalId).IsUnique();

        builder.Property(w => w.SiteCode).IsRequired().HasMaxLength(FieldLengths.SiteCode);
        builder.HasIndex(w => w.SiteCode);

        builder.Property(w => w.Description).IsRequired().HasMaxLength(FieldLengths.Description);

        // Stored as text rather than an ordinal, so the table is readable and renumbering the enum
        // cannot silently reinterpret existing rows.
        builder.Property(w => w.Status).IsRequired().HasMaxLength(FieldLengths.StatusName).HasConversion<string>();
        builder.HasIndex(w => w.Status);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();

        builder.Property(w => w.RowVersion).IsRowVersion();

        builder.HasMany(w => w.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // The navigation is read-only, so EF has to populate the backing field directly rather than
        // through the property. This is what lets the domain keep its encapsulation.
        builder.Metadata
            .FindNavigation(nameof(WorkOrder.StatusHistory))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
