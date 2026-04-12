namespace BlazorShop.Infrastructure.Data
{
    using System.IO;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;
    using Microsoft.Extensions.Configuration;

    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = this.ResolveApiProjectPath();
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets<AppDbContextFactory>(optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                                   ?? "Host=localhost;Port=5432;Database=blazorshop;Username=postgres;Password=change-me";

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }

        private string ResolveApiProjectPath()
        {
            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (currentDirectory is not null)
            {
                var apiProjectPath = Path.Combine(currentDirectory.FullName, "BlazorShop.Presentation", "BlazorShop.API");
                if (Directory.Exists(apiProjectPath))
                {
                    return apiProjectPath;
                }

                if (File.Exists(Path.Combine(currentDirectory.FullName, "appsettings.json"))
                    && File.Exists(Path.Combine(currentDirectory.FullName, "BlazorShop.API.csproj")))
                {
                    return currentDirectory.FullName;
                }

                currentDirectory = currentDirectory.Parent;
            }

            return Directory.GetCurrentDirectory();
        }
    }
}
