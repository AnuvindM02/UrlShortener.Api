using MongoDB.Driver;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Repositories;

/// <summary>
/// MongoDB implementation of IUrlRepository.
/// 
/// System Design Lessons:
/// 
/// 1. TTL Index: MongoDB has a background thread that runs every 60 seconds
///    and deletes documents where the indexed DateTime field &lt; now.
///    Setting expireAfterSeconds=0 means "delete exactly when the field's
///    value is reached." This gives us automatic URL expiration for free —
///    no cron jobs, no manual cleanup.
/// 
/// 2. Idempotent Index Creation: CreateIndexAsync with the same key+options
///    is a no-op if the index already exists. This means we can safely call it
///    on every app startup without "index already exists" errors. Critical for
///    containerized deployments where instances restart frequently.
/// 
/// 3. $inc for Click Counts: Instead of read-modify-write (which has race
///    conditions under concurrency), $inc is an atomic server-side operation.
///    If 100 users click simultaneously, each $inc is applied atomically —
///    you never lose a click. Read-modify-write would cause lost updates.
/// </summary>
public class MongoUrlRepository : IUrlRepository
{
    private readonly IMongoCollection<UrlMapping> _urls;

    public MongoUrlRepository(IMongoDatabase database)
    {
        _urls = database.GetCollection<UrlMapping>("urls");
        CreateIndexes().GetAwaiter().GetResult();
    }

    private async Task CreateIndexes()
    {
        // TTL index: MongoDB auto-deletes documents when expiresAt < now.
        // expireAfterSeconds: 0 means "use the field's value as the exact expiry time."
        var indexModel = new CreateIndexModel<UrlMapping>(
            Builders<UrlMapping>.IndexKeys.Ascending(x => x.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }
        );

        await _urls.Indexes.CreateOneAsync(indexModel);
    }

    public async Task CreateAsync(UrlMapping mapping)
    {
        await _urls.InsertOneAsync(mapping);
    }

    public async Task<UrlMapping?> GetByShortCodeAsync(string shortCode)
    {
        // shortCode IS the _id, so this uses the primary index — O(1) lookup
        return await _urls.Find(x => x.Id == shortCode).FirstOrDefaultAsync();
    }

    public async Task IncrementClickCountAsync(string shortCode)
    {
        var filter = Builders<UrlMapping>.Filter.Eq(x => x.Id, shortCode);
        var update = Builders<UrlMapping>.Update.Inc(x => x.ClickCount, 1L);
        await _urls.UpdateOneAsync(filter, update);
    }

    public async Task<bool> ShortCodeExistsAsync(string shortCode)
    {
        var count = await _urls.CountDocumentsAsync(x => x.Id == shortCode);
        return count > 0;
    }
}
