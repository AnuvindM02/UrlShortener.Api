namespace UrlShortener.Api.Options;

public class ShortUrlOptions
{
    public const string SectionName = "ShortUrl";

    public string BaseUrl { get; set; } = string.Empty;
    public int DefaultTtlDays { get; set; } = 1825;  // ~5 years
    public int CacheTtlHours { get; set; } = 24;
}
