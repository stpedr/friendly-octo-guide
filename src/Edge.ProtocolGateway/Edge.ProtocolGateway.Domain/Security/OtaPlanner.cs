namespace Edge.ProtocolGateway.Domain.Security;

public enum OtaState { UpToDate, UpdateAvailable, Unsupported }

public sealed record OtaDecision(OtaState State, string? TargetVersion, string? Reason);

/// <summary>Versão de firmware alvo por modelo de device — o "canário" do rollout OTA.</summary>
public sealed record FirmwareTarget(string Model, string TargetVersion);

/// <summary>
/// Planeja atualização OTA dos gateways/sensores: dado o modelo e a versão atual do
/// device, decide se está em dia, se há update, ou se o modelo não é suportado.
/// Comparação por versão semântica (major.minor.patch) — versão maior nunca é
/// "rebaixada" por engano. Determinístico; o rollout real é gradual (canary) fora daqui.
/// </summary>
public sealed class OtaPlanner(IReadOnlyList<FirmwareTarget> targets)
{
    private readonly Dictionary<string, string> _targetByModel =
        targets.ToDictionary(t => t.Model, t => t.TargetVersion, StringComparer.OrdinalIgnoreCase);

    public OtaDecision Decide(string model, string currentVersion)
    {
        if (!_targetByModel.TryGetValue(model, out var target))
            return new OtaDecision(OtaState.Unsupported, null, $"Modelo '{model}' sem alvo de firmware.");

        return Compare(currentVersion, target) < 0
            ? new OtaDecision(OtaState.UpdateAvailable, target, null)
            : new OtaDecision(OtaState.UpToDate, target, null);
    }

    // -1 se a < b, 0 se igual, +1 se a > b. Segmento ausente conta como 0.
    private static int Compare(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;
            if (x != y)
                return x < y ? -1 : 1;
        }
        return 0;
    }

    private static int[] Parse(string version) =>
        [.. version.TrimStart('v', 'V').Split('.')
            .Select(p => int.TryParse(p, out var n) ? n : 0)];
}
