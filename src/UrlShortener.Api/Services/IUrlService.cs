using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services;

public interface IUrlService
{
    Task<ShortenResponse> ShortenAsync(ShortenRequest request);
    Task<string?> GetLongUrlAsync(string shortCode);
    Task RecordClickAsync(string shortCode);
    Task<StatsResponse?> GetStatsAsync(string shortCode);
}
