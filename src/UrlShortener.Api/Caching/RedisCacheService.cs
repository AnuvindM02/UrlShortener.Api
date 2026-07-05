using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UrlShortener.Api.Options;

namespace UrlShortener.Api.Caching;

/// <summary>
/// Redis-backed cache using the Cache-Aside (Lazy Loading) pattern.
/// 
/// System Design Lesson — Cache-Aside Pattern:
///   1. Caller asks cache for data
///   2. Cache HIT  → return cached value (fast path, ~0.1ms)
///   3. Cache MISS → read from DB, write result to cache, return
/// 
/// Why Cache-Aside vs Write-Through?
///   - Cache-Aside only caches data that's actually requested (no wasted memory)
///   - Write-Through caches everything on write (good for write-heavy, predictable reads)
///   - URL shorteners are READ-HEAVY (1000:1 read:write ratio), so cache-aside is ideal
/// 
/// Resilience Design:
///   Redis is an OPTIMIZATION, not a requirement. If Redis goes down:
///   - GET failures → return null (cache miss, fall through to MongoDB)
///   - SET failures → silently swallow (we lose the cache benefit, but app works)
///   MongoDB remains the source of truth at all times.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _cacheTtl;
    private readonly ILogger<RedisCacheService> _logger;

    private const string KeyPrefix = "url:";

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<ShortUrlOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _cacheTtl = TimeSpan.FromHours(options.Value.CacheTtlHours);
        _logger = logger;
    }

    public async Task<string?> GetLongUrlAsync(string shortCode)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync($"{KeyPrefix}{shortCode}");
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            // Redis is down — treat as cache miss, fall through to MongoDB
            _logger.LogWarning(ex, "Redis GET failed for key {ShortCode}. Treating as cache miss.", shortCode);
            return null;
        }
    }

    public async Task SetLongUrlAsync(string shortCode, string longUrl)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"{KeyPrefix}{shortCode}", longUrl, _cacheTtl);
        }
        catch (Exception ex)
        {
            // Redis is down — silently swallow. We lose cache benefit but app keeps working.
            _logger.LogWarning(ex, "Redis SET failed for key {ShortCode}. Cache write skipped.", shortCode);
        }
    }
}
