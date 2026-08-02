using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InferQueue.Core.Llm;
using Microsoft.Extensions.Options;

namespace InferQueue.Infrastructure.Llm;

/// <summary>
/// Cliente da API de chat completions da OpenAI. E um typed client: quem cuida do
/// ciclo de vida do <see cref="HttpClient"/> e do handler e o IHttpClientFactory.
/// </summary>
internal sealed class OpenAiLlmClient(HttpClient http, IOptions<LlmOptions> options) : ILlmClient
{
    public async Task<LlmCompletion> CompleteAsync(
        string model,
        string input,
        CancellationToken ct = default)
    {
        var request = new ChatRequest(
            model,
            [
                new ChatMessage("system", options.Value.SystemPrompt),
                new ChatMessage("user", input)
            ]);

        using var response = await http.PostAsJsonAsync("chat/completions", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            // O corpo do erro da OpenAI diz o motivo (rate limit, contexto, chave invalida).
            // Vale carregar para o job, mas truncado: nao queremos um HTML de proxy inteiro no banco.
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new LlmException(
                $"OpenAI respondeu {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(ct)
            ?? throw new LlmException("OpenAI respondeu 2xx com corpo vazio.");

        var content = payload.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new LlmException("OpenAI respondeu sem conteudo utilizavel.");
        }

        return new LlmCompletion(
            content,
            payload.Usage?.PromptTokens ?? 0,
            payload.Usage?.CompletionTokens ?? 0);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<Choice>? Choices,
        [property: JsonPropertyName("usage")] Usage? Usage);

    private sealed record Choice(
        [property: JsonPropertyName("message")] ResponseMessage? Message);

    private sealed record ResponseMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record Usage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
