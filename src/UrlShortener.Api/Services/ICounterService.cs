namespace UrlShortener.Api.Services;

/// <summary>
/// Provides atomically incrementing unique IDs.
/// 
/// System Design Lesson: In distributed systems, you need a way to generate
/// globally unique, monotonically increasing IDs. Options include:
///   - Auto-increment in SQL (single point of failure)
///   - UUIDs (random, not sequential, poor for Base62)
///   - Snowflake IDs (Twitter's approach, complex)
///   - Atomic counter in a shared store (what we use here)
/// 
/// MongoDB's findOneAndUpdate with $inc is atomic even under concurrency,
/// making it a simple but effective distributed counter.
/// </summary>
public interface ICounterService
{
    Task<long> GetNextIdAsync();
}
