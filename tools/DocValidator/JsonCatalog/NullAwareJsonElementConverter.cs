using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog;

/// <summary>
/// Десериализует <see cref="JsonElement"/>, передавая JSON-токен <c>null</c> в конвертер.
/// По умолчанию System.Text.Json присваивает <see cref="Nullable{T}"/> свойству <c>null</c>
/// и при отсутствующем поле, и при явном <c>"field": null</c>, — не позволяя их различить.
/// С <see cref="HandleNull"/> = <c>true</c> явный null становится <see cref="JsonElement"/>
/// с <see cref="JsonValueKind.Null"/>, а отсутствующее поле остаётся <c>null</c>.
/// </summary>
public sealed class NullAwareJsonElementConverter : JsonConverter<JsonElement?>
{
    public override bool HandleNull => true;

    public override JsonElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // ParseValue корректно обрабатывает и токен Null, возвращая элемент с ValueKind.Null.
        return JsonElement.ParseValue(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, JsonElement? value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("CatalogLoader only reads JSON.");
    }
}
