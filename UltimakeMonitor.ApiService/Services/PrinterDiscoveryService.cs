using System.Collections.Concurrent;
using System.Net;
using Makaretu.Dns;
using UltimakeMonitor.ApiService.Models;

namespace UltimakeMonitor.ApiService.Services;

public class PrinterDiscoveryService : BackgroundService
{
    private readonly ILogger<PrinterDiscoveryService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, Printer> _printers = new();
    private readonly TimeSpan _discoveryInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);
    private MulticastService? _mdns;

    public PrinterDiscoveryService(ILogger<PrinterDiscoveryService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public IEnumerable<Printer> GetAllPrinters() => _printers.Values;

    public Printer? GetPrinter(string id) => _printers.GetValueOrDefault(id);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Printer Discovery Service started");

        // Initialize mDNS
        InitializeMdns();

        // Run discovery immediately on startup
        await DiscoverPrintersAsync(stoppingToken);

        // Create two timers - one for discovery, one for updates
        using var discoveryTimer = new PeriodicTimer(_discoveryInterval);
        using var updateTimer = new PeriodicTimer(_updateInterval);

        var discoveryTask = RunDiscoveryLoopAsync(discoveryTimer, stoppingToken);
        var updateTask = RunUpdateLoopAsync(updateTimer, stoppingToken);

        try
        {
            await Task.WhenAll(discoveryTask, updateTask);
        }
        finally
        {
            _mdns?.Stop();
            _mdns?.Dispose();
        }
    }

    private void InitializeMdns()
    {
        _mdns = new MulticastService();
        
        _mdns.AnswerReceived += async (s, e) =>
        {
            var answer = e.Message.Answers.OfType<ARecord>().FirstOrDefault();
            var ptrRecord = e.Message.Answers.OfType<PTRRecord>().FirstOrDefault();
            var srvRecord = e.Message.Answers.OfType<SRVRecord>().FirstOrDefault();
            
            if (answer != null && ptrRecord != null)
            {
                var ipAddress = answer.Address.ToString();
                var name = ptrRecord.DomainName.ToString();
                
                // Check if this is an Ultimaker printer
                if (name.Contains("ultimaker", StringComparison.OrdinalIgnoreCase) || 
                    name.Contains("_printer._tcp", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Discovered potential Ultimaker printer via mDNS: {Name} at {IP}", name, ipAddress);
                    
                    using var scope = _serviceProvider.CreateScope();
                    var ultimakerClient = scope.ServiceProvider.GetRequiredService<UltimakerApiClient>();
                    
                    await CheckAndAddPrinterAsync(ipAddress, ultimakerClient);
                }
            }
        };
        
        _mdns.Start();
    }

    private async Task DiscoverPrintersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting mDNS printer discovery...");
        
        if (_mdns == null) return;
        
        try
        {
            // Query for Ultimaker printers
            // Ultimaker printers typically advertise as "_printer._tcp.local" or "_ultimaker._tcp.local"
            _mdns.SendQuery("_printer._tcp.local", type: DnsType.PTR);
            _mdns.SendQuery("_ultimaker._tcp.local", type: DnsType.PTR);
            _mdns.SendQuery("_http._tcp.local", type: DnsType.PTR);
            
            // Also try specific Ultimaker service names
            _mdns.SendQuery("ultimaker.local", type: DnsType.A);
            
            // Wait a bit for responses
            await Task.Delay(5000, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mDNS discovery");
        }
        
        _logger.LogInformation("mDNS discovery completed. Found {Count} printers", _printers.Count);
    }

    private async Task CheckAndAddPrinterAsync(string ipAddress, UltimakerApiClient client)
    {
        try
        {
            var printerInfo = await client.GetPrinterInfoAsync(ipAddress);
            if (printerInfo != null)
            {
                var printer = new Printer
                {
                    Id = printerInfo.Guid ?? Guid.NewGuid().ToString(),
                    Name = printerInfo.Name ?? $"Ultimaker at {ipAddress}",
                    IpAddress = ipAddress,
                    Model = printerInfo.Variant ?? "Unknown",
                    Status = PrinterStatus.Idle,
                    LastSeen = DateTime.UtcNow
                };
                
                _printers.AddOrUpdate(printer.Id, printer, (_, _) => printer);
                _logger.LogInformation("Added/Updated printer: {Name} at {IP}", printer.Name, ipAddress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get info from {IP}, might not be an Ultimaker printer", ipAddress);
        }
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

    private async Task UpdatePrinterStatusesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var ultimakerClient = scope.ServiceProvider.GetRequiredService<UltimakerApiClient>();
        
        var updateTasks = _printers.Values.Select(printer => 
            UpdatePrinterStatusAsync(printer, ultimakerClient, cancellationToken));
        
        await Task.WhenAll(updateTasks);
    }

    private async Task UpdatePrinterStatusAsync(Printer printer, UltimakerApiClient client, CancellationToken cancellationToken)
    {
        try
        {
            // Get printer status
            var status = await client.GetPrinterStatusAsync(printer.IpAddress, cancellationToken);
            if (status != null)
            {
                printer.Status = MapPrinterStatus(status.Status);
                printer.BedTemperature = status.Bed?.Temperature?.Current;
                
                // Clear and update nozzles
                printer.Nozzles.Clear();
                if (status.Heads != null)
                {
                    for (int headIndex = 0; headIndex < status.Heads.Count; headIndex++)
                    {
                        var head = status.Heads[headIndex];
                        if (head.Extruders != null)
                        {
                            for (int extruderIndex = 0; extruderIndex < head.Extruders.Count; extruderIndex++)
                            {
                                var extruder = head.Extruders[extruderIndex];
                                if (extruder.Hotend != null)
                                {
                                    printer.Nozzles.Add(new NozzleInfo
                                    {
                                        Index = extruderIndex,
                                        Temperature = extruder.Hotend.Temperature?.Current,
                                        TargetTemperature = extruder.Hotend.Temperature?.Target
                                    });
                                }
                            }
                        }
                    }
                }
                
                printer.LastSeen = DateTime.UtcNow;
            }
            
            // Get print job if printing
            if (printer.Status == PrinterStatus.Printing)
            {
                var printJob = await client.GetPrintJobStatusAsync(printer.IpAddress, cancellationToken);
                if (printJob != null)
                {
                    printer.CurrentJob = new PrintJob
                    {
                        Name = printJob.Name ?? "Unknown",
                        ProgressPercentage = (int)(printJob.Progress * 100),
                        TimeElapsed = TimeSpan.FromSeconds(printJob.TimeElapsed),
                        TimeRemaining = TimeSpan.FromSeconds(Math.Max(0, printJob.TimeTotal - printJob.TimeElapsed))
                    };
                }
            }
            else
            {
                printer.CurrentJob = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for printer {PrinterId}", printer.Id);
            printer.Status = PrinterStatus.Offline;
        }
    }

    private static PrinterStatus MapPrinterStatus(string ultimakerStatus)
    {
        return ultimakerStatus?.ToLowerInvariant() switch
        {
            "idle" => PrinterStatus.Idle,
            "printing" => PrinterStatus.Printing,
            "paused" => PrinterStatus.Paused,
            "error" => PrinterStatus.Error,
            "maintenance" => PrinterStatus.Maintenance,
            _ => PrinterStatus.Offline
        };
    }
}
