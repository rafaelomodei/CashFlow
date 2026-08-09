using System.Text.Json;
using System.Text.Json.Serialization;

namespace Consolidation.Api.Http;

/// <summary>
/// Formato de fio da API (§1.1 e §1.3): `camelCase` e instantes em UTC com
/// sufixo `Z`.
/// </summary>
public static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = Configure(new JsonSerializerOptions());

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Converters.Add(new UtcInstantConverter());

        return options;
    }

    /// <summary>
    /// Escreve `2026-08-08T14:30:00Z`, e não `+00:00`. Os dois são ISO 8601
    /// válidos, mas o contrato documenta a primeira forma, e exemplo que não bate
    /// com o que trafega deixa de ser consultado.
    /// </summary>
    private sealed class UtcInstantConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

        public override DateTimeOffset Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString(
                Format, System.Globalization.CultureInfo.InvariantCulture));
    }
}
