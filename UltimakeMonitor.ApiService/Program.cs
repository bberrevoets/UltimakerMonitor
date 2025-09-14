using UltimakeMonitor.ApiService.Models;

namespace UltimakeMonitor.ApiService;

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

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", (HttpContext httpContext) =>
        {
            var forecast =  Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = summaries[Random.Shared.Next(summaries.Length)]
                })
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast");

        // Test endpoint for printers
        app.MapGet("/api/printers", () =>
        {
            var testPrinters = new List<Printer>
            {
                new Printer
                {
                    Id = "printer-1",
                    Name = "Ultimaker 3 - Office",
                    IpAddress = "192.168.1.100",
                    Model = "Ultimaker 3",
                    Status = PrinterStatus.Printing,
                    BedTemperature = 60.5,
                    NozzleTemperature = 210.0,
                    CurrentJob = new PrintJob
                    {
                        Name = "test-part.gcode",
                        ProgressPercentage = 45,
                        TimeElapsed = TimeSpan.FromHours(1.5),
                        TimeRemaining = TimeSpan.FromHours(2)
                    },
                    LastSeen = DateTime.UtcNow
                },
                new Printer
                {
                    Id = "printer-2",
                    Name = "Ultimaker 3 Extended - Lab",
                    IpAddress = "192.168.1.101",
                    Model = "Ultimaker 3 Extended",
                    Status = PrinterStatus.Idle,
                    BedTemperature = 25.0,
                    NozzleTemperature = 25.0,
                    LastSeen = DateTime.UtcNow
                }
            };
            return testPrinters;
        })
        .WithName("GetPrinters");

        app.Run();
    }
}
