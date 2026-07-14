using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Predictive.Domain.Registry;

namespace Predictive.Worker;

/// <summary>
/// Cliente do MLflow Tracking/Registry (REST): registra o run de recalibração
/// (params + métricas) e resolve a versão de modelo ativa (stage Production) no
/// boot. É a proveniência do modelo — nenhuma recalibração acontece sem virar run
/// versionado, e o worker sabe qual versão está servindo.
/// </summary>
public sealed class MlflowClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Registra um run completo (create → log-batch → finish) e devolve o run_id.</summary>
    public async Task<string> RegisterRunAsync(string experimentId, RecalibrationRun run, CancellationToken ct)
    {
        var created = await PostAsync<CreateRunResponse>("runs/create", new
        {
            experiment_id = experimentId,
            start_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        }, ct);
        var runId = created.Run.Info.RunId;

        await PostAsync<object>("runs/log-batch", new
        {
            run_id = runId,
            @params = run.Parameters.Select(p => new { key = p.Key, value = p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
            metrics = run.Metrics.Select(m => new { key = m.Key, value = m.Value, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), step = 0 }),
        }, ct);

        await PostAsync<object>("runs/update", new { run_id = runId, status = "FINISHED", end_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, ct);
        return runId;
    }

    /// <summary>Versão do modelo em Production (a que o worker deveria estar servindo), ou null.</summary>
    public async Task<string?> ActiveModelVersionAsync(string modelName, CancellationToken ct)
    {
        var response = await http.GetAsync(
            $"/api/2.0/mlflow/registered-models/get-latest-versions?name={Uri.EscapeDataString(modelName)}&stages=Production", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadFromJsonAsync<LatestVersionsResponse>(JsonOpts, ct);
        return body?.ModelVersions is [{ Version: { } v }, ..] ? v : null;
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"/api/2.0/mlflow/{path}", body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct)
            ?? throw new InvalidOperationException($"MLflow devolveu corpo vazio em {path}.");
    }

    private sealed record CreateRunResponse([property: JsonPropertyName("run")] RunEnvelope Run);
    private sealed record RunEnvelope([property: JsonPropertyName("info")] RunInfo Info);
    private sealed record RunInfo([property: JsonPropertyName("run_id")] string RunId);
    private sealed record LatestVersionsResponse([property: JsonPropertyName("model_versions")] IReadOnlyList<ModelVersion>? ModelVersions);
    private sealed record ModelVersion([property: JsonPropertyName("version")] string Version);
}
