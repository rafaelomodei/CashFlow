using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Contracts;

/// <summary>
/// Formato de fio dos eventos de integração. Vive aqui, e não em cada serviço,
/// porque a serialização <b>é</b> parte do contrato: produtor e consumidor
/// divergirem no formato de data seria uma quebra que nenhum teste de um lado só
/// pegaria.
/// </summary>
public static class IntegrationEvents
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new UtcInstantConverter() },
    };

    /// <summary>
    /// Escreve instantes como <c>2026-08-08T14:32:11Z</c>, e não
    /// <c>+00:00</c>. Os dois são ISO 8601 válidos, mas o contrato documenta a
    /// primeira forma, e exemplo que não bate com o que trafega é exemplo que
    /// deixa de ser consultado.
    /// </summary>
    private sealed class UtcInstantConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
    }
}
