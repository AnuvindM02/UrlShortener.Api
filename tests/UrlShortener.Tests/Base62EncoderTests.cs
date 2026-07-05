using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public class Base62EncoderTests
{
    [Fact]
    public void Encode_Zero_ReturnsFirstChar()
    {
        // 0 maps to the first character in the charset: 'a'
        var result = Base62Encoder.Encode(0);
        Assert.Equal("a", result);
    }

    [Fact]
    public void Encode_NegativeInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Base62Encoder.Encode(-1));
        Assert.Throws<ArgumentException>(() => Base62Encoder.Encode(-100));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(62)]
    [InlineData(1000)]
    [InlineData(123456789)]
    [InlineData(long.MaxValue / 2)]
    public void Encode_Decode_RoundTrip_ReturnsOriginal(long input)
    {
        // Encode → Decode should always return the original value.
        // This proves the algorithm is bijective (a perfect 1:1 mapping).
        var encoded = Base62Encoder.Encode(input);
        var decoded = Base62Encoder.Decode(encoded);

        Assert.Equal(input, decoded);
    }

    [Fact]
    public void Encode_KnownValues_ProducesExpectedLength()
    {
        // 62^1 = 62 possible 1-char codes (0..61)
        // So input 61 should be 1 char, input 62 should be 2 chars
        Assert.Single(Base62Encoder.Encode(61));
        Assert.Equal(2, Base62Encoder.Encode(62).Length);
    }

    [Fact]
    public void Decode_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Base62Encoder.Decode(""));
        Assert.Throws<ArgumentException>(() => Base62Encoder.Decode(null!));
    }

    [Fact]
    public void Decode_InvalidCharacter_ThrowsArgumentException()
    {
        // Characters like '!', '@', '#' are not in the Base62 charset
        Assert.Throws<ArgumentException>(() => Base62Encoder.Decode("abc!"));
    }

    [Fact]
    public void Encode_SequentialInputs_ProduceUniqueOutputs()
    {
        // Every unique ID must produce a unique short code
        var codes = Enumerable.Range(0, 1000)
            .Select(i => Base62Encoder.Encode(i))
            .ToHashSet();

        Assert.Equal(1000, codes.Count);
    }
}
