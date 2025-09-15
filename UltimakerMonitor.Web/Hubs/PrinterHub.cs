using Microsoft.AspNetCore.SignalR;
using UltimakerMonitor.Web.Models;

namespace UltimakerMonitor.Web.Hubs;

public class PrinterHub : Hub
{
    private readonly PrinterStateStore _store;

    public PrinterHub(PrinterStateStore store)
    {
        _store = store;
    }

    // Let a client request an initial snapshot
    public Task RequestSnapshot()
    {
        var snapshot = _store.GetSnapshot();
        return Clients.Caller.SendAsync("PrintersSnapshot", snapshot);
    }
}

// Simple in-memory store for the last known snapshot
public class PrinterStateStore
{
    private readonly object _lock = new();
    private List<Printer> _printers = new();

    public void SetSnapshot(List<Printer> printers)
    {
        lock (_lock)
        {
            _printers = printers;
        }
    }

    public List<Printer> GetSnapshot()
    {
        lock (_lock)
        {
            // shallow copy to avoid mutation from outside
            return _printers.ToList();
        }
    }
}