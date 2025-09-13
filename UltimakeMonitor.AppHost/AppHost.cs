using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<UltimakeMonitor_ApiService>("apiservice");

builder.AddProject<UltimakeMonitor_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();