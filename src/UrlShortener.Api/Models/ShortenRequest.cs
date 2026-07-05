namespace UrlShortener.Api.Models;

public class ShortenRequest
{
    public required string LongUrl { get; set; }
    public string? CustomAlias { get; set; }
    public int? TtlDays { get; set; }
}
