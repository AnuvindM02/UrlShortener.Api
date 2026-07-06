using System.Diagnostics;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Endpoints;

/// <summary>
/// Minimal API endpoint definitions.
///
/// System Design Lessons:
///
/// 1. 302 vs 301 Redirect:
///    - 301 (Permanent): Browser caches it FOREVER. Future visits bypass our server
///      entirely → we lose ALL analytics after the first click.
///    - 302 (Temporary): Browser asks our server every time → we track every click.
///    - bit.ly, tinyurl all use 301 for performance. We use 302 to keep analytics.
///    - In production you'd choose based on your priority: speed (301) vs analytics (302).
///
/// 2. Fire-and-Forget Click Tracking:
///    - The redirect response is returned IMMEDIATELY to the user.
///    - Click count increment runs in the background via Task.Run.
///    - Trade-off: clickCount is "eventually consistent" — it might lag by a few ms.
///    - This is acceptable because analytics don't need real-time precision.
/// </summary>
public static class UrlEndpoints
{
    public static void MapUrlEndpoints(this WebApplication app)
    {
        app.MapPost("/api/shorten", ShortenUrl);
        app.MapGet("/api/stats/{shortCode}", GetStats);
        app.MapGet("/{shortCode}", RedirectToLongUrl);
    }

    private static async Task<IResult> ShortenUrl(
        ShortenRequest request,
        IUrlService urlService,
        ILogger<Program> logger)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await urlService.ShortenAsync(request);
            sw.Stop();

            logger.LogInformation(
                "POST /api/shorten → {ShortCode} ({ElapsedMs}ms)",
                response.ShortCode, sw.ElapsedMilliseconds);

            return Results.Created($"/{response.ShortCode}", response);
        }
        catch (ArgumentException ex)
        {
            sw.Stop();
            logger.LogWarning("POST /api/shorten → 400: {Message} ({ElapsedMs}ms)", ex.Message, sw.ElapsedMilliseconds);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            logger.LogWarning("POST /api/shorten → 409: {Message} ({ElapsedMs}ms)", ex.Message, sw.ElapsedMilliseconds);
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> RedirectToLongUrl(
        string shortCode,
        IUrlService urlService,
        ILogger<Program> logger)
    {
        var sw = Stopwatch.StartNew();

        var longUrl = await urlService.GetLongUrlAsync(shortCode);

        sw.Stop();

        if (longUrl is null)
        {
            logger.LogWarning("GET /{ShortCode} → 404 ({ElapsedMs}ms)", shortCode, sw.ElapsedMilliseconds);
            return Results.NotFound(new { error = $"Short code '{shortCode}' not found." });
        }

        logger.LogInformation(
            "GET /{ShortCode} → 302 → {LongUrl} ({ElapsedMs}ms)",
            shortCode, longUrl, sw.ElapsedMilliseconds);

        // Fire-and-forget: increment click count in background.
        // The redirect response is sent IMMEDIATELY — user doesn't wait for the DB write.
        _ = Task.Run(() => urlService.RecordClickAsync(shortCode));

        // 302 Temporary Redirect — browser will ask us again next time (we keep analytics)
        return Results.Redirect(longUrl, permanent: false);
    }

    private static async Task<IResult> GetStats(
        string shortCode,
        IUrlService urlService,
        ILogger<Program> logger)
    {
        var sw = Stopwatch.StartNew();

        var stats = await urlService.GetStatsAsync(shortCode);

        sw.Stop();

        if (stats is null)
        {
            logger.LogWarning("GET /api/stats/{ShortCode} → 404 ({ElapsedMs}ms)", shortCode, sw.ElapsedMilliseconds);
            return Results.NotFound(new { error = $"Short code '{shortCode}' not found." });
        }

        logger.LogInformation(
            "GET /api/stats/{ShortCode} → 200 (clicks: {ClickCount}, {ElapsedMs}ms)",
            shortCode, stats.ClickCount, sw.ElapsedMilliseconds);

        return Results.Ok(stats);
    }
}
