namespace BlazorShop.Infrastructure.Data.Configurations;

using BlazorShop.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RegistrationConfirmationDeliveryConfiguration : IEntityTypeConfiguration<RegistrationConfirmationDelivery>
{
    public void Configure(EntityTypeBuilder<RegistrationConfirmationDelivery> builder)
    {
        builder.ToTable("RegistrationConfirmationDeliveries", table =>
        {
            table.HasCheckConstraint("CK_ConfirmationDelivery_Counts", "\"AttemptCount\" >= 0 AND \"Generation\" >= 1 AND \"RequestsInWindow\" >= 1");
            table.HasCheckConstraint("CK_ConfirmationDelivery_State", "\"State\" BETWEEN 0 AND 4");
        });
        builder.HasKey(row => row.Id);
        builder.Property(row => row.UserId).HasMaxLength(450).IsRequired();
        builder.Property(row => row.EmailSnapshot).HasMaxLength(256).IsRequired();
        builder.Property(row => row.SecurityStampHash).HasMaxLength(64).IsRequired();
        builder.Property(row => row.LastFailureCode).HasMaxLength(64);
        builder.HasIndex(row => row.UserId).IsUnique();
        builder.HasIndex(row => new { row.State, row.NextAttemptOn });
        builder.HasIndex(row => new { row.State, row.LeaseExpiresOn });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
