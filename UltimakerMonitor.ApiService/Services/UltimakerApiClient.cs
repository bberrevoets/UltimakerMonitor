using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltimakerMonitor.ApiService.Services;

public class UltimakerApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<UltimakerApiClient> _logger;

    public UltimakerApiClient(IHttpClientFactory httpClientFactory, ILogger<UltimakerApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<UltimakerPrinterInfo?> GetPrinterInfoAsync(string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = CreateHttpClient();
            var response = await httpClient.GetAsync($"http://{ipAddress}/api/v1/system", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<UltimakerPrinterInfo>(json, _jsonOptions);
            }

            _logger.LogWarning("Failed to get printer info from {IpAddress}: {StatusCode}", ipAddress,
                response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting printer info from {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<UltimakerPrintJobStatus?> GetPrintJobStatusAsync(string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = CreateHttpClient();
            var response = await httpClient.GetAsync($"http://{ipAddress}/api/v1/print_job", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<UltimakerPrintJobStatus>(json, _jsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print job status from {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<UltimakerPrinterStatus?> GetPrinterStatusAsync(string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = CreateHttpClient();
            var response = await httpClient.GetAsync($"http://{ipAddress}/api/v1/printer", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<UltimakerPrinterStatus>(json, _jsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting printer status from {IpAddress}", ipAddress);
            return null;
        }
    }

    private HttpClient CreateHttpClient()
    {
        var httpClient = _httpClientFactory.CreateClient("UltimakerApi");
        httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for local network
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}

// DTOs for Ultimaker API responses
public class UltimakerPrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string Firmware { get; set; } = string.Empty;
}

public class UltimakerPrintJobStatus
{
    public string State { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Progress { get; set; }

    [JsonPropertyName("time_elapsed")] public int TimeElapsed { get; set; }

    [JsonPropertyName("time_total")] public int TimeTotal { get; set; }
}

public class UltimakerPrinterStatus
{
    public string Status { get; set; } = string.Empty;
    public UltimakerBedStatus Bed { get; set; } = new();
    public List<UltimakerHeadStatus> Heads { get; set; } = new();
}

public class UltimakerBedStatus
{
    public UltimakerTemperature Temperature { get; set; } = new();
}

public class UltimakerHeadStatus
{
    public List<UltimakerExtruderStatus> Extruders { get; set; } = new();
}

public class UltimakerExtruderStatus
{
    public UltimakerHotendStatus Hotend { get; set; } = new();
}

public class UltimakerHotendStatus
{
    public UltimakerTemperature Temperature { get; set; } = new();
}

public class UltimakerTemperature
{
    public double Current { get; set; }
    public double Target { get; set; }
}