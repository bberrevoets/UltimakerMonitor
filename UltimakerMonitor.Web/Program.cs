using System.Net;
using UltimakerMonitor.Web.Services;

namespace UltimakerMonitor.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.Services.AddHttpClient("mjpeg", c => c.Timeout = Timeout.InfiniteTimeSpan);

        // Add services to the container.
        builder.Services.AddRazorPages();

        // Add HttpClient for API communication with service discovery
        builder.Services.AddHttpClient<PrinterApiClient>(client =>
        {
            // The URL will be automatically resolved by service discovery
            // "http://apiservice" matches the name we gave in AppHost
            client.BaseAddress = new Uri("http://apiservice");
        });

        var app = builder.Build();

        app.MapDefaultEndpoints();

        app.MapGet("/api/camera/stream", async (HttpContext ctx, IHttpClientFactory factory, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("CameraStream");

            // --- Read & validate host ---
            // e.g. /api/camera/stream?host=192.168.180.134 or 192.168.180.134:8080
            var hostParam = ctx.Request.Query["host"].ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(hostParam))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync(
                    "Missing host query parameter, e.g. /api/camera/stream?host=192.168.180.134[:port]");
                return;
            }

            // Split host:port if provided
            var hostOnly = hostParam;
            var port = 8080; // default for many MJPEG servers
            if (hostParam.Contains(':'))
            {
                var parts = hostParam.Split(':', 2);
                hostOnly = parts[0];
                if (!int.TryParse(parts[1], out port) || port < 1 || port > 65535)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("Invalid port.");
                    return;
                }
            }

            // Validate it's an IP and that it’s private (LAN) to prevent SSRF
            if (!IPAddress.TryParse(hostOnly, out var ip))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Invalid IP address.");
                return;
            }

            if (!IsPrivateOrLoopback(ip))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("IP must be private (RFC1918) or loopback.");
                return;
            }

            // Build upstream URL: http://<ip>:<port>/?action=stream
            var upstream = new UriBuilder(Uri.UriSchemeHttp, ip.ToString(), port)
            {
                Path = "/",
                Query = "action=stream"
            }.Uri;

            // --- Proxy the stream ---
            var client = factory.CreateClient("mjpeg");
            using var req = new HttpRequestMessage(HttpMethod.Get, upstream);
            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

            if (!res.IsSuccessStatusCode)
            {
                ctx.Response.StatusCode = (int)res.StatusCode;
                return;
            }

            if (res.Content.Headers.ContentType is { } ct)
                ctx.Response.ContentType = ct.ToString();

            ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Response.Headers.Pragma = "no-cache";
            ctx.Response.Headers.Expires = "0";

            await using var upstreamStream = await res.Content.ReadAsStreamAsync(ctx.RequestAborted);
            try
            {
                await upstreamStream.CopyToAsync(ctx.Response.Body, 64 * 1024, ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                log.LogDebug("Client canceled camera stream.");
            }
            catch (IOException) when (ctx.RequestAborted.IsCancellationRequested)
            {
                log.LogDebug("Client disconnected.");
            }

            // --- local helper ---
            static bool IsPrivateOrLoopback(IPAddress ipAddr)
            {
                if (IPAddress.IsLoopback(ipAddr)) return true;
                var b = ipAddr.GetAddressBytes();
                // 10.0.0.0/8
                if (b[0] == 10) return true;
                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return true;
                // Link-local 169.254.0.0/16 (optional)
                if (b[0] == 169 && b[1] == 254) return true;
                return false;
            }
        });


        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapRazorPages()
            .WithStaticAssets();

        app.Run();
    }
}