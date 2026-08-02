namespace InferQueue.Core.Jobs;

/// <summary>
/// Ja existe um job em andamento com o mesmo conteudo. Sinaliza a corrida entre duas
/// requisicoes simultaneas que o indice unico parcial barrou no banco.
/// </summary>
public sealed class DuplicateJobException(string inputHash, Exception? innerException = null)
    : Exception($"Ja existe um job nao concluido com o hash {inputHash}.", innerException)
{
    public string InputHash { get; } = inputHash;
}
