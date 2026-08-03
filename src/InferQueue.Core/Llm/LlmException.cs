namespace InferQueue.Core.Llm;

/// <summary>
/// Falha vinda do provedor de LLM. Existe para o Worker distinguir "a LLM recusou/caiu"
/// de um bug nosso, que sobe como qualquer outra excecao.
/// </summary>
public sealed class LlmException(string message, bool isTransient, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// Se a falha tem chance de passar sozinha. Rate limit e indisponibilidade sao
    /// transitorios; chave invalida ou prompt recusado nao sao — insistir neles so
    /// gasta tentativa e atrasa a fila.
    /// </summary>
    public bool IsTransient { get; } = isTransient;
}
