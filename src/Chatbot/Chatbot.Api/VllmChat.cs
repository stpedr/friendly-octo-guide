using System.Text.Json;
using System.Text.Json.Serialization;
using Chatbot.Domain.Rag;

namespace Chatbot.Api;

/// <summary>
/// Conversa com o vLLM (mesmo GPU pool do bloco 3b, API OpenAI-compatible).
/// O contexto RAG entra como system prompt com as fontes identificadas —
/// resposta cita de onde veio, e o painel mostra a linhagem.
/// </summary>
public sealed class VllmChat(HttpClient http, IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<string> AskAsync(string question, IReadOnlyList<RagDocument> context, CancellationToken ct)
    {
        var systemPrompt = context.Count == 0
            ? "Você é o assistente da linha de produção. Se não houver contexto suficiente, diga que não sabe."
            : "Você é o assistente da linha de produção. Responda APENAS com base no contexto abaixo, citando a fonte.\n\n"
              + string.Join("\n\n", context.Select(d => $"[{d.Id}]\n{d.Content}"));

        var request = new
        {
            model = config["Vllm:Model"] ?? "meta-llama/Llama-3.1-8B-Instruct",
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = question },
            },
            max_tokens = 512,
            temperature = 0.1,
        };

        using var response = await http.PostAsJsonAsync("/v1/chat/completions", request, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletion>(JsonOpts, ct);
        return completion is { Choices: [var first, ..] }
            ? first.Message?.Content ?? "Não consegui gerar resposta agora — tente novamente."
            : "Não consegui gerar resposta agora — tente novamente.";
    }

    private sealed record ChatCompletion([property: JsonPropertyName("choices")] IReadOnlyList<Choice> Choices);
    private sealed record Choice([property: JsonPropertyName("message")] ChoiceMessage? Message);
    private sealed record ChoiceMessage([property: JsonPropertyName("content")] string? Content);
}
