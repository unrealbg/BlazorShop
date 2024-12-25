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

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<PaymentMethod> PaymentMethods { get; set; }

        public DbSet<OrderItem> CheckoutOrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PaymentMethod>().HasData(
                new PaymentMethod
                {
                    Id = Guid.Parse("3604fc1d-cd6a-46ad-ace4-9b5f8e03f43b"),
                    Name = "Credit Card",
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
        }
    }
}
