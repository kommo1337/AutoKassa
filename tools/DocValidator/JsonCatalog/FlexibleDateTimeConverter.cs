using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog;

/// <summary>
/// Десериализует <see cref="DateTimeOffset"/> из разных строковых форматов:
/// ISO 8601 с таймзоной, дата без времени, или возвращает null.
/// </summary>
public sealed class FlexibleDateTimeConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly string[] Formats =
    {
        "O",
        "yyyy-MM-ddTHH:mm:ss.fffzzz",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-dd"
    };

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for date, got {reader.TokenType}.");
        }

        string? value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParseExact(
                value,
                Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset result))
        {
            return result;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
        {
            return result;
        }

        throw new JsonException($"Cannot parse date/time value '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("CatalogLoader only reads JSON.");
    }
}
