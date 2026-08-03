using InferQueue.Core.Llm;
using Microsoft.Extensions.Logging;

namespace InferQueue.Infrastructure.Llm;

/// <summary>
/// Substituto do provedor real, usado quando nao ha chave configurada.
/// Deixa o pipeline inteiro (fila, lease, worker, persistencia) rodavel de ponta a
/// ponta sem chave e sem custo — que e exatamente o que os testes vao querer depois.
/// </summary>
internal sealed class FakeLlmClient(ILogger<FakeLlmClient> logger) : ILlmClient
{
    public async Task<LlmCompletion> CompleteAsync(
        string model,
        string input,
        CancellationToken ct = default)
    {
        logger.LogWarning(
            "Usando o cliente fake de LLM: nenhuma chamada real sera feita ao modelo {Model}.",
            model);

        // Latencia artificial para que o comportamento de lease e concorrencia
        // apareca de verdade em vez de tudo terminar instantaneamente.
        await Task.Delay(TimeSpan.FromMilliseconds(250), ct);

        var preview = input.Length <= 40 ? input : input[..40] + "...";

        return new LlmCompletion(
            $"[fake] analise simulada de: \"{preview}\"",
            PromptTokens: EstimateTokens(input),
            CompletionTokens: 12);
    }

    // Regra de bolso: ~4 caracteres por token. Serve so para o fake ter numeros plausiveis.
    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);
}
