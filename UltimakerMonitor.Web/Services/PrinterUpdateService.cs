using Microsoft.AspNetCore.SignalR;

using UltimakerMonitor.Web.Hubs;
using UltimakerMonitor.Web.Models;

namespace UltimakerMonitor.Web.Services;

public class PrinterUpdateService : BackgroundService
{
    private readonly ILogger<PrinterUpdateService> _logger;
    private readonly PrinterApiClient _api;
    private readonly IHubContext<PrinterHub> _hub;
    private readonly PrinterStateStore _store;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(3);

    private readonly Dictionary<string, Printer> _last = new(StringComparer.OrdinalIgnoreCase);
    private bool _primed; // optional: skip sending events on the very first poll

    public PrinterUpdateService(
        ILogger<PrinterUpdateService> logger,
        PrinterApiClient api,
        IHubContext<PrinterHub> hub,
        PrinterStateStore store)
    {
        _logger = logger;
        _api = api;
        _hub = hub;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PrinterUpdateService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var current = await _api.GetPrintersAsync();
                _store.SetSnapshot(current);

                var comparer = StringComparer.OrdinalIgnoreCase;
                var currentById = current.ToDictionary(p => p.Id, p => p, comparer);

                if (!_primed)
                {
                    // First run: just seed state (clients will call RequestSnapshot anyway)
                    _last.Clear();
                    foreach (var kv in currentById)
                        _last[kv.Key] = kv.Value;

                    _primed = true;
                }
                else
                {
                    var lastIds = new HashSet<string>(_last.Keys, comparer);
                    var currIds = new HashSet<string>(currentById.Keys, comparer);

                    var addedIds = currIds.Except(lastIds, comparer).ToList();
                    var removedIds = lastIds.Except(currIds, comparer).ToList();
                    var commonIds = currIds.Intersect(lastIds, comparer).ToList();

                    // ADDED
                    foreach (var id in addedIds)
                    {
                        var cur = currentById[id];
                        await _hub.Clients.All.SendAsync("PrinterAdded", cur, stoppingToken);
                        _last[id] = cur;
                        _logger.LogInformation("Printer added {Id} - {PrinterName}", id, cur.Name);
                    }

                    // CHANGED
                    foreach (var id in commonIds)
                    {
                        var prev = _last[id];
                        var cur = currentById[id];

                        if (HasChanged(prev, cur))
                        {
                            await _hub.Clients.All.SendAsync("PrinterChanged", cur, stoppingToken);
                            _last[id] = cur;
                            _logger.LogInformation("Printer changed {Id} - {PrinterName}", id, cur.Name);
                        }
                    }

                    // REMOVED
                    foreach (var id in removedIds)
                    {
                        await _hub.Clients.All.SendAsync("PrinterRemoved", id, stoppingToken);
                        _last.Remove(id);
                        _logger.LogInformation("Printer removed {Id}", id);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Polling printers failed.");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { /* normal on shutdown */ }
        }

        _logger.LogInformation("PrinterUpdateService stopping.");
    }

    private static bool HasChanged(Printer a, Printer b)
    {
        if (a.Status != b.Status) return true;
        if (a.BedTemperature != b.BedTemperature) return true;

        var aCount = a.Nozzles?.Count ?? 0;
        var bCount = b.Nozzles?.Count ?? 0;
        if (aCount != bCount) return true;
        for (int i = 0; i < Math.Min(aCount, bCount); i++)
        {
            if (a.Nozzles![i].Temperature != b.Nozzles![i].Temperature) return true;
            if (a.Nozzles![i].TargetTemperature != b.Nozzles![i].TargetTemperature) return true;
        }

        if ((a.CurrentJob?.ProgressPercentage ?? -1) != (b.CurrentJob?.ProgressPercentage ?? -1)) return true;
        if ((int)(a.CurrentJob?.State ?? 0) != (int)(b.CurrentJob?.State ?? 0)) return true;

        return false;
    }
}
