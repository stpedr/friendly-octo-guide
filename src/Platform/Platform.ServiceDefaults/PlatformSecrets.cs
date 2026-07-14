using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Platform.ServiceDefaults;

/// <summary>
/// Cliente mínimo pro KV v2 do OpenBao (API compatível com Vault — sem SDK extra).
/// Sem OpenBao:Addr configurado (dev local sem o container), volta null e quem
/// chamou decide o fallback — mesmo padrão de "fase 0/fase 1" do resto do repo.
/// </summary>
public static class PlatformSecrets
{
    public static async Task<string?> TryGetAsync(IConfiguration config, string path, string key, CancellationToken ct = default)
    {
        var addr = config["OpenBao:Addr"];
        var token = config["OpenBao:Token"];
        if (string.IsNullOrEmpty(addr) || string.IsNullOrEmpty(token))
            return null;

        using var http = new HttpClient { BaseAddress = new Uri(addr) };
        http.DefaultRequestHeaders.Add("X-Vault-Token", token);

        try
        {
            var response = await http.GetAsync($"/v1/secret/data/{path}", ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<VaultKvResponse>(cancellationToken: ct);
            return body?.Data?.Data?.GetValueOrDefault(key);
        }
        catch (HttpRequestException)
        {
            return null; // OpenBao fora do ar não pode derrubar o boot do serviço
        }
    }

    private sealed class VaultKvResponse
    {
        public VaultKvData? Data { get; set; }
    }

    private sealed class VaultKvData
    {
        public Dictionary<string, string>? Data { get; set; }
    }
}
