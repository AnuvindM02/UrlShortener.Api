using MongoDB.Bson;
using MongoDB.Driver;

namespace UrlShortener.Api.Services;

/// <summary>
/// Generates unique sequential IDs using MongoDB's atomic findOneAndUpdate.
/// 
/// System Design Lesson: This is an "atomic counter" pattern. The key insight
/// is that findOneAndUpdate with $inc is a single atomic operation in MongoDB —
/// even if 100 requests hit simultaneously, each gets a unique value.
/// 
/// The counter document looks like: { _id: "url_id", value: 12345 }
/// - IsUpsert: true  → creates the document on first call (no manual seeding)
/// - ReturnDocument.After → returns the value AFTER incrementing
/// 
/// Trade-off: This is a single-document bottleneck. For massive scale (millions
/// of writes/sec), you'd switch to range-based allocation or Snowflake IDs.
/// For our learning project, this is perfectly appropriate.
/// </summary>
public class CounterService : ICounterService
{
    private readonly IMongoCollection<BsonDocument> _counters;

    public CounterService(IMongoDatabase database)
    {
        _counters = database.GetCollection<BsonDocument>("counters");
    }

    public async Task<long> GetNextIdAsync()
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", "url_id");
        var update = Builders<BsonDocument>.Update.Inc("value", 1L);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _counters.FindOneAndUpdateAsync(filter, update, options);
        return result["value"].AsInt64;
    }
}
