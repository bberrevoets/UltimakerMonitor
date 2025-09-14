namespace UltimakeMonitor.Web.Models;

public class Printer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public PrinterStatus Status { get; set; }
    public double? BedTemperature { get; set; }
    public List<NozzleInfo> Nozzles { get; set; } = new();
    public PrintJob? CurrentJob { get; set; }
    public DateTime LastSeen { get; set; }
}

public class NozzleInfo
{
    public int Index { get; set; }
    public double? Temperature { get; set; }
    public double? TargetTemperature { get; set; }
}

public enum PrinterStatus
{
    Idle,
    Printing,
    Paused,
    Error,
    Offline,
    Maintenance
}

public class PrintJob
{
    public string Name { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public TimeSpan? TimeElapsed { get; set; }
    public TimeSpan? TimeRemaining { get; set; }
}