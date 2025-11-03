namespace BlazorShop.API
{
    using System.Text.Json.Serialization;

    using BlazorShop.Application;
    using BlazorShop.Infrastructure;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;

    using Serilog;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("log/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();
            Log.Logger.Information("Application Starting...");

            builder.Services.AddControllers().AddJsonOptions(opt =>
                opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddSwaggerGen();

            builder.AddServiceDefaults();

            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddApplication(builder.Configuration);
            builder.Services.AddCors(
                co =>
                    {
                        co.AddDefaultPolicy(
                            opt =>
                                {
                                    opt.AllowAnyHeader()
                                        .AllowAnyMethod()
                                        .AllowCredentials()
                                    //.WithOrigins("https://localhost:7258");
                                        .SetIsOriginAllowed(origin =>
#if DEBUG 
                                  origin.StartsWith("http://localhost") ||
                                            origin.StartsWith("https://localhost"));
#else
                                origin.StartsWith("http://shop.mydomain.com") ||
                                            origin.StartsWith("https://shop.mydomain.com"));
#endif
                                });
                    });

            try
            {
                var app = builder.Build();
#if DEBUG

#else
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                    KnownProxies = { IPAddress.Parse("1.1.1.200") },
                    ForwardLimit = 1
                });
#endif
                // Create database schema at startup (no migrations present yet)
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();
                }

                app.UseCors();
                app.UseSerilogRequestLogging();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);

                Log.Logger.Information("ContentRootPath: {ContentRoot}", builder.Environment.ContentRootPath);
                Log.Logger.Information("Uploads path configured: {UploadsPath}", uploadsPath);

                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(uploadsPath),
                    RequestPath = "/uploads",
                    ServeUnknownFileTypes = true,
                    OnPrepareResponse = ctx =>
                        {
                            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
                        }
                });

                app.UseInfrastructure();

                app.UseHttpsRedirection();

                app.UseAuthentication();
                app.UseAuthorization();

                app.MapControllers();

                Log.Logger.Information("Application Started");

                app.Run();
            }
            catch (Exception e)
            {
                Log.Logger.Fatal(e, "The application failed to start correctly.");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
