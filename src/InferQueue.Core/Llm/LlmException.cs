namespace InferQueue.Core.Llm;

/// <summary>
/// Falha vinda do provedor de LLM. Existe para o Worker distinguir "a LLM recusou/caiu"
/// de um bug nosso, que sobe como qualquer outra excecao.
/// </summary>
public sealed class LlmException(string message, Exception? innerException = null)
    : Exception(message, innerException);
