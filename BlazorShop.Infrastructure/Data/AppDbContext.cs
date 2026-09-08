namespace BlazorShop.Infrastructure.Data
{
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;

    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }

        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

        public DbSet<AdminSettings> AdminSettings { get; set; }

        public DbSet<Product> Products { get; set; }

        public DbSet<SeoRedirect> SeoRedirects { get; set; }

        public DbSet<SeoSettings> SeoSettings { get; set; }

        public DbSet<ProductVariant> ProductVariants { get; set; } // new

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<PaymentMethod> PaymentMethods { get; set; }

        public DbSet<OrderItem> CheckoutOrderItems { get; set; }

        public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderLine> OrderLines { get; set; }

        public DbSet<InventoryReservation> InventoryReservations { get; set; }

        public DbSet<CheckoutIdempotencyRecord> CheckoutIdempotencyRecords { get; set; }

        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        public DbSet<PaymentProviderEvent> PaymentProviderEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // ProductVariant configuration
            builder.Entity<ProductVariant>()
                   .HasIndex(v => new { v.ProductId, v.SizeScale, v.SizeValue })
                   .IsUnique();

            builder.Entity<ProductVariant>()
                   .HasOne(v => v.Product)
                   .WithMany(p => p.Variants)
                   .HasForeignKey(v => v.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<IdentityUserLogin<string>>()
                .Property(login => login.LoginProvider)
                .HasMaxLength(128);

            builder.Entity<IdentityUserLogin<string>>()
                .Property(login => login.ProviderKey)
                .HasMaxLength(128);

            builder.Entity<IdentityUserToken<string>>()
                .Property(token => token.LoginProvider)
                .HasMaxLength(128);

            builder.Entity<IdentityUserToken<string>>()
                .Property(token => token.Name)
                .HasMaxLength(128);

            builder.Entity<RefreshToken>()
                .Property(token => token.TokenHash)
                .HasMaxLength(64);

            builder.Entity<RefreshToken>()
                .Property(token => token.ReplacedByTokenHash)
                .HasMaxLength(64);

            builder.Entity<RefreshToken>()
                .Property(token => token.CreatedByIp)
                .HasMaxLength(64);

            builder.Entity<RefreshToken>()
                .Property(token => token.RevokedByIp)
                .HasMaxLength(64);

            builder.Entity<RefreshToken>()
                .Property(token => token.UserAgent)
                .HasMaxLength(512);

            builder.Entity<RefreshToken>()
                .HasIndex(token => token.TokenHash)
                .IsUnique();

            builder.Entity<RefreshToken>()
                .HasIndex(token => new { token.UserId, token.RevokedAtUtc });

            builder.Entity<RefreshToken>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AppUser>()
                .Property(user => user.CreatedOn)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Entity<Order>()
                .Property(order => order.AdminNote)
                .HasMaxLength(2000);

            builder.Entity<PaymentMethod>().HasData(
                new PaymentMethod
                {
                    Id = PaymentMethodIds.CreditCard,
                    Name = "Credit Card",
                },
                new PaymentMethod
                {
                    Id = PaymentMethodIds.CashOnDelivery,
                    Name = "Cash on Delivery",
                },
                new PaymentMethod
                {
                    Id = PaymentMethodIds.BankTransfer,
                    Name = "Bank Transfer",
                });

            builder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = "93f5cdac-43de-4895-8426-2048c228e76d",
                    ConcurrencyStamp = "02d86d56-8e63-4d2e-92f8-81b154ba0532",
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                },
                new IdentityRole
                {
                    Id = "b7af6842-02fa-4af4-8f61-ae04a49644a2",
                    ConcurrencyStamp = "75e8afa8-8df5-4431-a220-ac56b1fd0cda",
                    Name = "User",
                    NormalizedName = "USER"
                });

            // NewsletterSubscriber config
            builder.Entity<NewsletterSubscriber>()
                   .HasIndex(x => x.Email)
                   .IsUnique();

            builder.Entity<Order>()
                .HasIndex(o => o.Reference)
                .IsUnique();

            builder.Entity<Order>()
                .HasIndex(o => new { o.UserId, o.CreatedOn });

            builder.Entity<Order>()
                .HasIndex(o => o.CreatedOn);

            builder.Entity<Order>()
                .HasMany(o => o.Lines)
                .WithOne(l => l.Order!)
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
