namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
    {
        public void Configure(EntityTypeBuilder<InventoryReservation> builder)
        {
            builder.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryReservations_Quantity_Positive",
                    "\"Quantity\" > 0");
                table.HasCheckConstraint(
                    "CK_InventoryReservations_Lifecycle",
                    "(\"Status\" = 'Reserved' AND \"ConsumedOn\" IS NULL AND \"ReleasedOn\" IS NULL) OR "
                    + "(\"Status\" = 'Consumed' AND \"ConsumedOn\" IS NOT NULL AND \"ReleasedOn\" IS NULL) OR "
                    + "(\"Status\" = 'Released' AND \"ConsumedOn\" IS NULL AND \"ReleasedOn\" IS NOT NULL)");
            });

            builder.Property(reservation => reservation.Status)
                .HasConversion<string>()
                .HasMaxLength(16);

            builder.Property(reservation => reservation.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(reservation => reservation.OrderId);
            builder.HasIndex(reservation => new { reservation.OrderId, reservation.ProductVariantId })
                .IsUnique()
                .HasFilter("\"ProductVariantId\" IS NOT NULL");
            builder.HasIndex(reservation => new { reservation.OrderId, reservation.ProductId })
                .IsUnique()
                .HasFilter("\"ProductVariantId\" IS NULL");

            builder.HasOne(reservation => reservation.Order)
                .WithMany()
                .HasForeignKey(reservation => reservation.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
