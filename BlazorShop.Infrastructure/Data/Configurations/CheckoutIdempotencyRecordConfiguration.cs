namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class CheckoutIdempotencyRecordConfiguration
        : IEntityTypeConfiguration<CheckoutIdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<CheckoutIdempotencyRecord> builder)
        {
            builder.ToTable("CheckoutIdempotencyRecords");

            builder.Property(record => record.UserId).HasMaxLength(450);
            builder.Property(record => record.RequestFingerprint).HasMaxLength(64);
            builder.Property(record => record.OrderReference).HasMaxLength(64);
            builder.Property(record => record.State)
                .HasConversion<string>()
                .HasMaxLength(32);
            builder.Property(record => record.OutcomeJson).HasColumnType("jsonb");
            builder.Property(record => record.CreatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(record => record.UpdatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(record => new { record.UserId, record.IdempotencyKey }).IsUnique();
            builder.HasIndex(record => record.OrderId);
            builder.HasIndex(record => new { record.State, record.LeaseExpiresOn });
            builder.HasIndex(record => new { record.State, record.ExpiresOn });
        }
    }
}
