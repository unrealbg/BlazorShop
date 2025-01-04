var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.BlazorShop_API>("apiservice");

builder.AddProject<Projects.BlazorShop_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
