using Makaretu.Dns;

using Microsoft.Extensions.Options;

using System.Collections.Concurrent;

using UltimakerMonitor.ApiService.Models;
using UltimakerMonitor.ApiService.Options;

namespace UltimakerMonitor.ApiService.Services;

public class PrinterDiscoveryService : BackgroundService
{
    private readonly TimeSpan _discoveryInterval = TimeSpan.FromMinutes(5);
    private readonly ILogger<PrinterDiscoveryService> _logger;
    private readonly ConcurrentDictionary<string, Printer> _printers = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10);
    private MulticastService? _mdns; 
    private readonly DiscoveryTestOptions _testOptions;
    private readonly ConcurrentDictionary<string, DateTime> _simulatedExpiry = new();

    public PrinterDiscoveryService(ILogger<PrinterDiscoveryService> logger, IServiceProvider serviceProvider, IOptions<DiscoveryTestOptions> testOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _testOptions = testOptions.Value;
    }

    public IEnumerable<Printer> GetAllPrinters()
    {
        return _printers.Values;
    }

    public Printer? GetPrinter(string id)
    {
        return _printers.GetValueOrDefault(id);
    }

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

        Task? simulationTask = null;
        if (_testOptions.EnableSimulation)
        {
            var simTimer = new PeriodicTimer(TimeSpan.FromSeconds(_testOptions.AddEverySeconds));
            simulationTask = RunSimulationLoopAsync(simTimer, stoppingToken);
        }

        try
        {
            if (simulationTask is null)
                await Task.WhenAll(discoveryTask, updateTask);
            else
                await Task.WhenAll(discoveryTask, updateTask, simulationTask);
        }
        finally
        {
            _mdns?.Stop();
            _mdns?.Dispose();
        }

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

    private async Task RunSimulationLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        var rnd = new Random();

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                // Pick a real printer (if any) to clone, otherwise synthesize
                var real = _printers.Values.FirstOrDefault(p => !p.IsSimulated);
                var id = Guid.NewGuid().ToString("N");
                var nameSuffix = rnd.Next(100, 999);
                var printer = new Printer
                {
                    Id = $"sim-{id}",
                    Name = real is null ? $"Ultimaker SIM {nameSuffix}" : $"{real.Name} (SIM {nameSuffix})",
                    IpAddress = "192.168.180.134",  // fixed address for simulated printer
                    Model = real?.Model ?? "Ultimaker-SIM",
                    Status = PrinterStatus.Printing,
                    BedTemperature = (real?.BedTemperature ?? 60) + rnd.Next(-2, 3),
                    LastSeen = DateTime.UtcNow,
                    IsSimulated = true,
                    Nozzles =
                {
                    new NozzleInfo { Index = 0, Temperature = 205 + rnd.Next(-3, 4), TargetTemperature = 210 },
                    new NozzleInfo { Index = 1, Temperature = 0, TargetTemperature = 0 }
                },
                    CurrentJob = new PrintJob
                    {
                        Name = $"Dummy Job {nameSuffix}",
                        ProgressPercentage = 0,
                        TimeElapsed = TimeSpan.Zero,
                        TimeRemaining = TimeSpan.FromMinutes(25),
                        State = JobState.Printing
                    }
                };

                _printers[printer.Id] = printer;
                var expiry = DateTime.UtcNow.AddSeconds(_testOptions.LifetimeSeconds);
                _simulatedExpiry[printer.Id] = expiry;

                _logger.LogInformation("📦 Added simulated printer {Name} ({Id}) until {Expiry:u}", printer.Name, printer.Id, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Simulation loop failed to add a dummy printer");
            }
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
                    _logger.LogInformation("Discovered potential Ultimaker printer via mDNS: {Name} at {IP}", name,
                        ipAddress);

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
        while (await timer.WaitForNextTickAsync(stoppingToken)) await DiscoverPrintersAsync(stoppingToken);
    }

    private async Task RunUpdateLoopAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken)) await UpdatePrinterStatusesAsync(stoppingToken);
    }

    private async Task UpdatePrinterStatusesAsync(CancellationToken cancellationToken)
    {
        // 6a) Remove expired simulated printers
        var now = DateTime.UtcNow;
        foreach (var kvp in _simulatedExpiry.ToArray())
        {
            if (kvp.Value <= now)
            {
                if (_printers.TryRemove(kvp.Key, out var removed))
                {
                    _simulatedExpiry.TryRemove(kvp.Key, out _);
                    _logger.LogInformation("🗑️ Removed simulated printer {Name} ({Id})", removed.Name, removed.Id);
                }
                else
                {
                    _simulatedExpiry.TryRemove(kvp.Key, out _);
                }
            }
        }

        using var scope = _serviceProvider.CreateScope();
        var ultimakerClient = scope.ServiceProvider.GetRequiredService<UltimakerApiClient>();

        var updateTasks = _printers.Values.Select(printer =>
            UpdatePrinterStatusAsync(printer, ultimakerClient, cancellationToken));

        await Task.WhenAll(updateTasks);
    }

    private async Task UpdatePrinterStatusAsync(Printer printer, UltimakerApiClient client,
        CancellationToken cancellationToken)
    {
        if (printer.IsSimulated)
        {
            SimulatePrinterTick(printer);
            return;
        }

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
                    for (var headIndex = 0; headIndex < status.Heads.Count; headIndex++)
                    {
                        var head = status.Heads[headIndex];
                        if (head.Extruders != null)
                            for (var extruderIndex = 0; extruderIndex < head.Extruders.Count; extruderIndex++)
                            {
                                var extruder = head.Extruders[extruderIndex];
                                if (extruder.Hotend != null)
                                    printer.Nozzles.Add(new NozzleInfo
                                    {
                                        Index = extruderIndex,
                                        Temperature = extruder.Hotend.Temperature?.Current,
                                        TargetTemperature = extruder.Hotend.Temperature?.Target
                                    });
                            }
                    }

                printer.LastSeen = DateTime.UtcNow;
            }

            // Get print job if printing
            if (printer.Status == PrinterStatus.Printing)
            {
                var printJob = await client.GetPrintJobStatusAsync(printer.IpAddress, cancellationToken);

                if (printJob != null)
                    printer.CurrentJob = new PrintJob
                    {
                        Name = printJob.Name ?? "Unknown",
                        ProgressPercentage = (int)(printJob.Progress * 100),
                        TimeElapsed = TimeSpan.FromSeconds(printJob.TimeElapsed),
                        TimeRemaining = TimeSpan.FromSeconds(Math.Max(0, printJob.TimeTotal - printJob.TimeElapsed)),
                        State = MapJobState(printJob.State)
                    };
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

    private void SimulatePrinterTick(Printer p)
    {
        // simple state machine: mostly "Printing", then "PostPrint" near 100%, then back to "Idle"
        p.LastSeen = DateTime.UtcNow;

        if (p.CurrentJob == null)
        {
            p.Status = PrinterStatus.Idle;
            return;
        }

        var progress = p.CurrentJob.ProgressPercentage + 3; // +3% each tick (~every 10s by your _updateInterval)
        progress = Math.Min(progress, 100);

        p.CurrentJob.ProgressPercentage = progress;
        p.CurrentJob.TimeElapsed = (p.CurrentJob.TimeElapsed ?? TimeSpan.Zero).Add(TimeSpan.FromSeconds(_updateInterval.TotalSeconds));
        var remaining = TimeSpan.FromMinutes(25) * (100 - progress) / 100.0;
        p.CurrentJob.TimeRemaining = remaining;

        // wiggle temps a bit
        if (p.Nozzles.Count > 0)
        {
            var rnd = new Random();
            p.Nozzles[0].Temperature = (p.Nozzles[0].Temperature ?? 205) + rnd.Next(-1, 2);
        }
        p.BedTemperature = (p.BedTemperature ?? 60) + (new Random().Next(-1, 2));

        if (progress >= 100)
        {
            p.Status = PrinterStatus.Offline;
            p.CurrentJob.State = JobState.PostPrint;
            // small grace before it turns idle (until expiry removes it)
            if (p.CurrentJob.TimeRemaining <= TimeSpan.FromSeconds(0))
            {
                p.Status = PrinterStatus.Idle;
                p.CurrentJob = null;
            }
        }
        else
        {
            p.Status = PrinterStatus.Printing;
            p.CurrentJob.State = JobState.Printing;
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

    private static JobState MapJobState(string jobState)
    {
        return jobState.ToLowerInvariant() switch
        {
            "pre_print" => JobState.Preparing,
            "printing" => JobState.Printing,
            "pausing" => JobState.Pausing,
            "paused" => JobState.Paused,
            "resuming" => JobState.Resuming,
            "post_print" => JobState.PostPrint,
            "wait_cleanup" => JobState.WaitCleanup,
            _ => JobState.NoJob
        };
    }
}