var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("blazorshop-postgres-data", isReadOnly: false);

var database = postgres.AddDatabase("DefaultConnection", "blazorshop");

var apiService = builder.AddProject<Projects.BlazorShop_API>("apiservice")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.BlazorShop_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
