using System.Net.Http.Json;
using UltimakeMonitor.Web.Models;

namespace UltimakeMonitor.Web.Services;

public class PrinterApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PrinterApiClient> _logger;

    public PrinterApiClient(HttpClient httpClient, ILogger<PrinterApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<Printer>> GetPrintersAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<Printer>>("/api/printers");
            return response ?? new List<Printer>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching printers");
            return new List<Printer>();
        }
    }

    public async Task<Printer?> GetPrinterAsync(string id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Printer>($"/api/printers/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching printer {PrinterId}", id);
            return null;
        }
    }

    public async Task<bool> DiscoverPrintersAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/printers/discover", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering printers");
            return false;
        }
    }
}