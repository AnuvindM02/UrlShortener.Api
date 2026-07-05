namespace UrlShortener.Api.Caching;

public interface ICacheService
{
    Task<string?> GetLongUrlAsync(string shortCode);
    Task SetLongUrlAsync(string shortCode, string longUrl);
}
