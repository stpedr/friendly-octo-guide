using System.Text.Json;

namespace Gateway.Domain;

/// <summary>
/// Beacon de RUM que o PWA envia via sendBeacon pro Gateway (POST /rum).
/// Entrada é anônima e não confiável — o parse valida tudo e recusa o resto:
/// rota curta e sã (vira tag de métrica, cardinalidade importa), status HTTP
/// plausível e duração não negativa.
/// </summary>
public sealed record RumBeacon(string Route, int Status, double DurationMs)
{
    public const int MaxPayloadBytes = 2048;
    private const int MaxRouteLength = 128;

    public static bool TryParse(ReadOnlySpan<byte> payload, out RumBeacon? beacon)
    {
        beacon = null;
        if (payload.Length is 0 or > MaxPayloadBytes)
            return false;

        try
        {
            var reader = new Utf8JsonReader(payload);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!root.TryGetProperty("route", out var routeEl) || routeEl.ValueKind != JsonValueKind.String)
                return false;
            if (!root.TryGetProperty("status", out var statusEl) || !statusEl.TryGetInt32(out var status))
                return false;
            if (!root.TryGetProperty("durationMs", out var durEl) || !durEl.TryGetDouble(out var durationMs))
                return false;

            var route = routeEl.GetString()!;
            if (route.Length is 0 or > MaxRouteLength || !route.StartsWith('/'))
                return false;
            if (status is < 100 or > 599)
                return false;
            if (durationMs < 0 || !double.IsFinite(durationMs))
                return false;

            beacon = new RumBeacon(route, status, durationMs);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
