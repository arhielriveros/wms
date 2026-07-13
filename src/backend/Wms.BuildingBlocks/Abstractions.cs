using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wms.BuildingBlocks;

public interface ITenantContext
{
    Guid TenantId { get; }
    string ActorId { get; }
    Guid CorrelationId { get; }
    string? WarehouseScope { get; }
    bool IsResolved { get; }
}

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

public abstract class TenantEntity : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class WmsProblemException(
    string code,
    string message,
    int statusCode = 400,
    IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}

public static class Correlation
{
    public static Guid CurrentOrNew() =>
        Activity.Current?.GetBaggageItem("correlation.id") is { } value && Guid.TryParse(value, out var id)
            ? id
            : Guid.NewGuid();
}

public static class PayloadChecksum
{
    public static string Compute(JsonElement payload)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, payload);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    public static string Compute<T>(T payload)
    {
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(payload));
        return Compute(document.RootElement);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}

public sealed record CanonicalEnvelope<TPayload>(
    Guid MessageId,
    string MessageType,
    string SchemaVersion,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    string SourceSystem,
    Guid CorrelationId,
    Guid? CausationId,
    TPayload Payload);

public sealed record AdvanceShippingNotice(
    string ExternalId,
    string WarehouseCode,
    string OwnerCode,
    string? SupplierExternalId,
    DateTimeOffset ExpectedAt,
    IReadOnlyList<AdvanceShippingNoticeLine> Lines);

public sealed record AdvanceShippingNoticeLine(
    string ExternalLineId,
    string Sku,
    decimal Quantity,
    string Uom);

public sealed record SalesOrder(
    string ExternalId,
    string WarehouseCode,
    string OwnerCode,
    string? CustomerExternalId,
    int Priority,
    DateTimeOffset RequestedShipAt,
    IReadOnlyList<SalesOrderLine> Lines);

public sealed record SalesOrderLine(
    string ExternalLineId,
    string Sku,
    decimal Quantity,
    string Uom);

public sealed record OperationAccepted(Guid MessageId, Guid CorrelationId, string Status, bool AlreadyProcessed);
