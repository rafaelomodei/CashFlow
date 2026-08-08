using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashFlow.Application.Exceptions;

namespace CashFlow.Application.Transactions;

/// <summary>
/// Posição na ordenação <c>occurred_at DESC, id DESC</c> (ADR-014). Opaco para o
/// cliente: o formato interno pode mudar sem aviso, e é justamente por isso que
/// ele é codificado em vez de exposto campo a campo.
/// </summary>
public sealed record TransactionCursor(DateTimeOffset OccurredAt, Guid Id)
{
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public string Encode()
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CursorPayload
        {
            O = OccurredAt.ToUniversalTime().ToString(InstantFormat, CultureInfo.InvariantCulture),
            I = Id,
        });

        return Base64Url.EncodeToString(payload);
    }

    public static TransactionCursor Decode(string value)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CursorPayload>(Base64Url.DecodeFromChars(value));
            if (payload?.O is null)
            {
                throw new InvalidQueryException("Cursor is invalid.");
            }

            return new TransactionCursor(
                DateTimeOffset.ParseExact(payload.O, InstantFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                payload.I);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            // O cliente não constrói cursor: qualquer valor irreconhecível veio de
            // adulteração ou de truncamento, e nos dois casos a resposta é a mesma.
            throw new InvalidQueryException("Cursor is invalid.");
        }
    }

    /// <summary>Nomes curtos: o cursor viaja em query string a cada página.</summary>
    private sealed class CursorPayload
    {
        [JsonPropertyName("o")]
        public string? O { get; set; }

        [JsonPropertyName("i")]
        public Guid I { get; set; }
    }
}
