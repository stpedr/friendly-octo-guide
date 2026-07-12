using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ai.Worker.Llm;

/// <summary>
/// Cliente do vLLM (API OpenAI-compatible, /v1/chat/completions).
/// O modelo servido vem da config — o worker é genérico, o Deployment K8s
/// fixa (modelo, GPU pool) por instância; rollout controlado pelo Model Registry.
/// </summary>
public sealed class VllmClient(HttpClient http, IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var request = new
        {
            model = config["Vllm:Model"] ?? "meta-llama/Llama-3.1-8B-Instruct",
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = config.GetValue("Vllm:MaxTokens", 1024),
            temperature = 0.2,
        };

        using var response = await http.PostAsJsonAsync("/v1/chat/completions", request, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletion>(JsonOpts, ct)
            ?? throw new InvalidOperationException("vLLM devolveu corpo vazio.");
        return completion.Choices is [var first, ..]
            ? first.Message?.Content ?? throw new InvalidOperationException("vLLM devolveu choice sem conteúdo.")
            : throw new InvalidOperationException("vLLM devolveu resposta sem choices.");
    }

    private sealed record ChatCompletion([property: JsonPropertyName("choices")] IReadOnlyList<Choice> Choices);
    private sealed record Choice([property: JsonPropertyName("message")] ChoiceMessage? Message);
    private sealed record ChoiceMessage([property: JsonPropertyName("content")] string? Content);
}
