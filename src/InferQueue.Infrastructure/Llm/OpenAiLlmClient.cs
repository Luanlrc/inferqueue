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

        HttpResponseMessage response;

        try
        {
            response = await http.PostAsJsonAsync("chat/completions", request, ct);
        }
        catch (HttpRequestException ex)
        {
            // DNS, conexao recusada, TLS. A rede pode voltar, entao vale retentar.
            throw new LlmException($"Falha de rede ao chamar a OpenAI: {ex.Message}", isTransient: true, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Sem o filtro, um shutdown do worker viraria "timeout" e consumiria uma tentativa.
            throw new LlmException(
                $"Chamada a OpenAI excedeu o timeout de {options.Value.TimeoutSeconds}s.",
                isTransient: true,
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;

                // 429 e 5xx passam sozinhos; 401 e 400 nao passam por mais que se insista.
                var isTransient = status is 408 or 429 or >= 500;

                // O corpo do erro da OpenAI diz o motivo (rate limit, contexto, chave invalida).
                // Vale carregar para o job, mas truncado: nao queremos um HTML de proxy inteiro no banco.
                var body = await response.Content.ReadAsStringAsync(ct);

                throw new LlmException($"OpenAI respondeu {status}: {Truncate(body, 500)}", isTransient);
            }

            var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(ct)
                ?? throw new LlmException("OpenAI respondeu 2xx com corpo vazio.", isTransient: true);

            var content = payload.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new LlmException("OpenAI respondeu sem conteudo utilizavel.", isTransient: true);
            }

            return new LlmCompletion(
                content,
                payload.Usage?.PromptTokens ?? 0,
                payload.Usage?.CompletionTokens ?? 0);
        }
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
