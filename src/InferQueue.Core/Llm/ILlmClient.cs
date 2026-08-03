namespace InferQueue.Core.Llm;

/// <summary>
/// Porta de saida para o provedor de LLM. O Worker depende disto, nao da OpenAI:
/// e o que permite rodar o pipeline inteiro com um fake, sem chave e sem custo.
/// </summary>
public interface ILlmClient
{
    Task<LlmCompletion> CompleteAsync(string model, string input, CancellationToken ct = default);
}
