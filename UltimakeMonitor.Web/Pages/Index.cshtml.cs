using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimakeMonitor.Web.Models;
using UltimakeMonitor.Web.Services;

namespace UltimakeMonitor.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly PrinterApiClient _printerApiClient;

    public IndexModel(ILogger<IndexModel> logger, PrinterApiClient printerApiClient)
    {
        _logger = logger;
        _printerApiClient = printerApiClient;
    }

    public List<Printer> Printers { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            Printers = await _printerApiClient.GetPrintersAsync();
            _logger.LogInformation("Loaded {Count} printers", Printers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load printers");
            Printers = new List<Printer>();
        }
    }
}
