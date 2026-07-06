using Microsoft.Extensions.Options;
using UrlShortener.Api.Caching;
using UrlShortener.Api.Models;
using UrlShortener.Api.Options;
using UrlShortener.Api.Repositories;

namespace UrlShortener.Api.Services;

/// <summary>
/// Orchestrates the full URL shortening lifecycle:
///   Counter → Base62 → MongoDB → Redis cache
///
/// System Design Lesson — Separation of Concerns:
///   This service doesn't know HOW IDs are generated (counter vs snowflake),
///   HOW data is stored (MongoDB vs Postgres), or HOW caching works (Redis vs Memcached).
///   It only knows the FLOW. Swapping any component requires zero changes here.
/// </summary>
public class UrlService : IUrlService
{
    private readonly IUrlRepository _repository;
    private readonly ICacheService _cache;
    private readonly ICounterService _counter;
    private readonly ShortUrlOptions _options;
    private readonly ILogger<UrlService> _logger;

    public UrlService(
        IUrlRepository repository,
        ICacheService cache,
        ICounterService counter,
        IOptions<ShortUrlOptions> options,
        ILogger<UrlService> logger)
    {
        _repository = repository;
        _cache = cache;
        _counter = counter;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ShortenResponse> ShortenAsync(ShortenRequest request)
    {
        // 1. Validate URL
        if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ArgumentException("Invalid URL. Must be an absolute HTTP or HTTPS URL.");
        }

        string shortCode;

        // 2. Determine short code
        if (!string.IsNullOrWhiteSpace(request.CustomAlias))
        {
            // Custom alias requested — check uniqueness
            if (await _repository.ShortCodeExistsAsync(request.CustomAlias))
            {
                throw new InvalidOperationException($"Custom alias '{request.CustomAlias}' is already taken.");
            }
            shortCode = request.CustomAlias;
        }
        else
        {
            // Auto-generate: atomic counter → Base62
            var nextId = await _counter.GetNextIdAsync();
            shortCode = Base62Encoder.Encode(nextId);
        }

        // 3. Calculate expiry
        var ttlDays = request.TtlDays ?? _options.DefaultTtlDays;
        var now = DateTime.UtcNow;

        // 4. Persist to MongoDB
        var mapping = new UrlMapping
        {
            Id = shortCode,
            LongUrl = request.LongUrl,
            CreatedAt = now,
            ExpiresAt = now.AddDays(ttlDays)
        };

        await _repository.CreateAsync(mapping);

        _logger.LogInformation("Shortened {LongUrl} → {ShortCode}", request.LongUrl, shortCode);

        // 5. Return response
        return new ShortenResponse
        {
            ShortCode = shortCode,
            ShortUrl = $"{_options.BaseUrl}/{shortCode}",
            LongUrl = request.LongUrl,
            CreatedAt = mapping.CreatedAt,
            ExpiresAt = mapping.ExpiresAt
        };
    }

    /// <summary>
    /// Cache-aside read path:
    ///   1. Check Redis (fast, ~0.1ms)
    ///   2. On miss → check MongoDB (slower, ~1-5ms)
    ///   3. On DB hit → backfill Redis for next time
    /// </summary>
    public async Task<string?> GetLongUrlAsync(string shortCode)
    {
        // 1. Try cache first
        var cached = await _cache.GetLongUrlAsync(shortCode);
        if (cached is not null)
        {
            _logger.LogInformation("Cache HIT for {ShortCode}", shortCode);
            return cached;
        }

        _logger.LogInformation("Cache MISS for {ShortCode}, querying MongoDB", shortCode);

        // 2. Cache miss → query MongoDB
        var mapping = await _repository.GetByShortCodeAsync(shortCode);
        if (mapping is null)
            return null;

        // 3. Backfill cache for next request
        await _cache.SetLongUrlAsync(shortCode, mapping.LongUrl);

        return mapping.LongUrl;
    }

    public async Task RecordClickAsync(string shortCode)
    {
        await _repository.IncrementClickCountAsync(shortCode);
    }

    /// <summary>
    /// Stats bypass the cache intentionally — we want the freshest clickCount
    /// from MongoDB, not a potentially stale cached value.
    /// </summary>
    public async Task<StatsResponse?> GetStatsAsync(string shortCode)
    {
        var mapping = await _repository.GetByShortCodeAsync(shortCode);
        if (mapping is null)
            return null;

        return new StatsResponse
        {
            ShortCode = mapping.Id,
            LongUrl = mapping.LongUrl,
            CreatedAt = mapping.CreatedAt,
            ExpiresAt = mapping.ExpiresAt,
            ClickCount = mapping.ClickCount
        };
    }
}
