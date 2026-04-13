using System.IO;

using Microsoft.AspNetCore.Connections;

var builder = DistributedApplication.CreateBuilder(args);

try
{
    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume("blazorshop-postgres-data", isReadOnly: false);

    var database = postgres.AddDatabase("DefaultConnection", "blazorshop");

    var apiService = builder.AddProject<Projects.BlazorShop_API>("apiservice")
        .WithExternalHttpEndpoints()
        .WithReference(database)
        .WaitFor(database);

    builder.AddProject<Projects.BlazorShop_Web>("webfrontend")
        .WithExternalHttpEndpoints()
        .WithReference(apiService)
        .WaitFor(apiService);

    builder.Build().Run();
}
catch (IOException e) when (e.InnerException is AddressInUseException || e.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("The AppHost port is already in use. Stop the existing AppHost instance or run only one orchestration host at a time.");
    Environment.ExitCode = 1;
}
