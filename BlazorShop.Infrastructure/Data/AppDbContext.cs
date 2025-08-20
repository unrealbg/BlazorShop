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

        public DbSet<Product> Products { get; set; }

        public DbSet<ProductVariant> ProductVariants { get; set; } // new

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<PaymentMethod> PaymentMethods { get; set; }

        public DbSet<OrderItem> CheckoutOrderItems { get; set; }

        public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderLine> OrderLines { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ProductVariant configuration
            builder.Entity<ProductVariant>()
                   .HasIndex(v => new { v.ProductId, v.SizeScale, v.SizeValue })
                   .IsUnique();

            builder.Entity<ProductVariant>()
                   .HasOne(v => v.Product)
                   .WithMany(p => p.Variants)
                   .HasForeignKey(v => v.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PaymentMethod>().HasData(
                new PaymentMethod
                {
                    Id = Guid.Parse("3604fc1d-cd6a-46ad-ace4-9b5f8e03f43b"),
                    Name = "Credit Card",
                },
                new PaymentMethod
                {
                    Id = Guid.Parse("a3bb23e6-6a7c-4b7d-9c73-7d5f2bc2f7b1"),
                    Name = "PayPal",
                },
                new PaymentMethod
                {
                    Id = Guid.Parse("6f2c2a7e-9f9b-4a0d-9f7f-2a1b3c4d5e6f"),
                    Name = "Cash on Delivery",
                },
                new PaymentMethod
                {
                    Id = Guid.Parse("b2e5c1d4-7a9f-4d2c-8f1e-3a4b5c6d7e8f"),
                    Name = "Bank Transfer",
                });

            builder.Entity<IdentityRole>().HasData(
                new IdentityRole
                    {
                        Id = "93f5cdac-43de-4895-8426-2048c228e76d",
                        Name = "Admin",
                        NormalizedName = "ADMIN"
                    },
                new IdentityRole
                    {
                        Id = "b7af6842-02fa-4af4-8f61-ae04a49644a2",
                        Name = "User",
                        NormalizedName = "USER"
                    });

            // NewsletterSubscriber config
            builder.Entity<NewsletterSubscriber>()
                   .HasIndex(x => x.Email)
                   .IsUnique();

            builder.Entity<Order>()
                .HasMany(o => o.Lines)
                .WithOne(l => l.Order!)
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
