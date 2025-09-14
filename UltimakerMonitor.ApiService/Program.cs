using UltimakerMonitor.ApiService.Services;

namespace UltimakerMonitor.ApiService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // Add HttpClient factory
        builder.Services.AddHttpClient();

        // Register the Ultimaker API client
        builder.Services.AddScoped<UltimakerApiClient>();

        // Register the printer discovery service
        builder.Services.AddSingleton<PrinterDiscoveryService>();
        builder.Services.AddHostedService<PrinterDiscoveryService>(provider =>
            provider.GetRequiredService<PrinterDiscoveryService>());

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        // Update endpoints to use the discovery service
        app.MapGet("/api/printers",
                (PrinterDiscoveryService discoveryService) => { return discoveryService.GetAllPrinters(); })
            .WithName("GetPrinters");

        app.MapGet("/api/printers/{id}", (string id, PrinterDiscoveryService discoveryService) =>
            {
                var printer = discoveryService.GetPrinter(id);
                return printer is not null ? Results.Ok(printer) : Results.NotFound();
            })
            .WithName("GetPrinter");

        app.MapPost("/api/printers/discover", async (PrinterDiscoveryService discoveryService) =>
            {
                // Trigger manual discovery if needed
                // For now, just return success since discovery runs automatically
                await Task.Delay(100); // Simulate some work
                return Results.Ok(new { message = "Discovery initiated" });
            })
            .WithName("DiscoverPrinters");

        app.Run();
    }
}