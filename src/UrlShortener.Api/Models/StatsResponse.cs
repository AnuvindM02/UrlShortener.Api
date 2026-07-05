namespace UrlShortener.Api.Models;

public class StatsResponse
{
    public string ShortCode { get; set; } = string.Empty;
    public string LongUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public long ClickCount { get; set; }
}
