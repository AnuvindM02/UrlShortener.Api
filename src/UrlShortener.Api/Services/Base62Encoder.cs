namespace UrlShortener.Api.Services;

/// <summary>
/// Converts numeric IDs to/from compact Base62 strings.
/// 
/// System Design Lesson: URL shorteners need short, URL-safe codes.
/// Base62 (a-z, A-Z, 0-9) is ideal because every character is URL-safe
/// without encoding, unlike Base64 which includes '+', '/', '='.
/// A 7-char Base62 string can represent 62^7 = ~3.5 trillion unique URLs.
/// </summary>
public static class Base62Encoder
{
    private const string Charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int Base = 62;

    /// <summary>
    /// Encodes a non-negative numeric ID into a Base62 string.
    /// Algorithm: repeatedly divide by 62, map each remainder to the charset, reverse.
    /// </summary>
    public static string Encode(long id)
    {
        if (id < 0)
            throw new ArgumentException("ID must be non-negative.", nameof(id));

        if (id == 0)
            return Charset[0].ToString(); // "a"

        var chars = new List<char>();

        while (id > 0)
        {
            chars.Add(Charset[(int)(id % Base)]);
            id /= Base;
        }

        // Digits were produced least-significant first, so reverse
        chars.Reverse();
        return new string(chars.ToArray());
    }

    /// <summary>
    /// Decodes a Base62 string back to its numeric ID.
    /// Each character's position in the charset is its digit value.
    /// </summary>
    public static long Decode(string shortCode)
    {
        if (string.IsNullOrEmpty(shortCode))
            throw new ArgumentException("Short code cannot be null or empty.", nameof(shortCode));

        long result = 0;

        foreach (var c in shortCode)
        {
            var index = Charset.IndexOf(c);
            if (index < 0)
                throw new ArgumentException($"Invalid character '{c}' in short code.", nameof(shortCode));

            result = result * Base + index;
        }

        return result;
    }
}
