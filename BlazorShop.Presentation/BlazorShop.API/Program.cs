
namespace BlazorShop.API
{
    using System.Text.Json.Serialization;

    using BlazorShop.Application;
    using BlazorShop.Infrastructure;

    using Microsoft.AspNetCore.Builder;

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

            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddApplication();
            builder.Services.AddCors(
                co =>
                    {
                        co.AddDefaultPolicy(
                            opt =>
                                {
                                    opt.AllowAnyHeader()
                                        .AllowAnyMethod()
                                        .AllowCredentials()
                                    .WithOrigins("https://localhost:7258");
                                });
                    });

            try
            {
                var app = builder.Build();

                app.UseCors();
                app.UseSerilogRequestLogging();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                app.UseStaticFiles();

                app.UseInfrastructure();

                app.UseHttpsRedirection();

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
