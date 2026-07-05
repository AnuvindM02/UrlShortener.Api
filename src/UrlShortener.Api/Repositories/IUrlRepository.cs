using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

public interface IUrlRepository
{
    Task CreateAsync(UrlMapping mapping);
    Task<UrlMapping?> GetByShortCodeAsync(string shortCode);
    Task IncrementClickCountAsync(string shortCode);
    Task<bool> ShortCodeExistsAsync(string shortCode);
}
