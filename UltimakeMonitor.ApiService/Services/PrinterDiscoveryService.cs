using System.Collections.Concurrent;
using UltimakeMonitor.ApiService.Models;

namespace UltimakeMonitor.ApiService.Services;

public class PrinterDiscoveryService : BackgroundService
{
    private readonly ILogger<PrinterDiscoveryService> _logger;
    private readonly ConcurrentDictionary<string, Printer> _printers = new();
    private readonly TimeSpan _discoveryInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);

    public PrinterDiscoveryService(ILogger<PrinterDiscoveryService> logger)
    {
        _logger = logger;
        InitializeTestPrinters(); // For testing, we'll start with test data
    }

    public IEnumerable<Printer> GetAllPrinters() => _printers.Values;

    public Printer? GetPrinter(string id) => _printers.GetValueOrDefault(id);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Printer Discovery Service started");

        // Run discovery immediately on startup
        await DiscoverPrintersAsync(stoppingToken);

        // Create two timers - one for discovery, one for updates
        using var discoveryTimer = new PeriodicTimer(_discoveryInterval);
        using var updateTimer = new PeriodicTimer(_updateInterval);

        var discoveryTask = RunDiscoveryLoopAsync(discoveryTimer, stoppingToken);
        var updateTask = RunUpdateLoopAsync(updateTimer, stoppingToken);

        await Task.WhenAll(discoveryTask, updateTask);
    }

    private async Task RunDiscoveryLoopAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DiscoverPrintersAsync(stoppingToken);
        }
    }

    private async Task RunUpdateLoopAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await UpdatePrinterStatusesAsync(stoppingToken);
        }
    }

    private async Task DiscoverPrintersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting printer discovery...");
        
        // TODO: Implement actual network discovery
        // For now, we'll just ensure test printers exist
        await Task.Delay(100, cancellationToken); // Simulate network delay
        
        _logger.LogInformation("Printer discovery completed. Found {Count} printers", _printers.Count);
    }

    private async Task UpdatePrinterStatusesAsync(CancellationToken cancellationToken)
    {
        foreach (var printer in _printers.Values)
        {
            // TODO: Implement actual printer status polling
            // For now, simulate status updates
            await Task.Delay(10, cancellationToken); // Simulate network delay
            
            printer.LastSeen = DateTime.UtcNow;
            
            // Simulate print progress
            if (printer is { CurrentJob: not null, Status: PrinterStatus.Printing })
            {
                printer.CurrentJob.ProgressPercentage = Math.Min(100, printer.CurrentJob.ProgressPercentage + Random.Shared.Next(1, 5));
                if (printer.CurrentJob.ProgressPercentage >= 100)
                {
                    printer.Status = PrinterStatus.Idle;
                    printer.CurrentJob = null;
                    printer.BedTemperature = 25.0;
                    printer.NozzleTemperature = 25.0;
                }
            }
        }
    }

    private void InitializeTestPrinters()
    {
        var testPrinters = new[]
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

        foreach (var printer in testPrinters)
        {
            _printers.TryAdd(printer.Id, printer);
        }
    }
}
