using MongoDB.Bson.Serialization.Attributes;

namespace UrlShortener.Api.Models;

/// <summary>
/// MongoDB document representing a shortened URL.
/// 
/// System Design Lesson: We use shortCode as _id instead of ObjectId because:
/// 1. Every lookup is by shortCode — making it _id means lookups use the
///    primary index (fastest possible query, zero extra indexes needed).
/// 2. _id has a unique constraint built in — no duplicate short codes possible.
/// 3. ObjectId would require a separate unique index on shortCode anyway,
///    doubling the index storage for no benefit.
/// </summary>
public class UrlMapping
{
    /// <summary>
    /// The short code IS the document ID. e.g. "b" for id=1, "dnh" for id=12345.
    /// Using [BsonId] maps this to MongoDB's _id field.
    /// </summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("longUrl")]
    public string LongUrl { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this document expires. MongoDB's TTL index watches this field
    /// and automatically deletes the document when ExpiresAt &lt; DateTime.UtcNow.
    /// </summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("clickCount")]
    public long ClickCount { get; set; } = 0;
}
