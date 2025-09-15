namespace UltimakerMonitor.ApiService.Options;

public record DiscoveryTestOptions
{
    public bool EnableSimulation { get; init; } = false;     // set true to enable
    public int AddEverySeconds { get; init; } = 90;          // how often to add a dummy
    public int LifetimeSeconds { get; init; } = 180;         // how long dummy lives
}
