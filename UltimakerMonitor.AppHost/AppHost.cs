using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<UltimakerMonitor_ApiService>("apiservice");

builder.AddProject<UltimakerMonitor_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();