namespace BlazorShop.Infrastructure.Data.Configurations.Admin
{
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
    {
        public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
        {
            builder.Property(log => log.ActorUserId)
                .HasMaxLength(450);

            builder.Property(log => log.ActorEmail)
                .HasMaxLength(256);

            builder.Property(log => log.Action)
                .HasMaxLength(96)
                .IsRequired();

            builder.Property(log => log.EntityType)
                .HasMaxLength(96)
                .IsRequired();

            builder.Property(log => log.EntityId)
                .HasMaxLength(128);

            builder.Property(log => log.Summary)
                .HasMaxLength(1000)
                .IsRequired();

            builder.Property(log => log.IpAddress)
                .HasMaxLength(64);

            builder.Property(log => log.UserAgent)
                .HasMaxLength(512);

            builder.Property(log => log.CreatedOn)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(log => log.CreatedOn);
            builder.HasIndex(log => new { log.EntityType, log.EntityId });
            builder.HasIndex(log => log.ActorUserId);
        }
    }
}
