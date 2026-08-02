namespace InferQueue.Core.Jobs;

/// <summary>
/// Estados possiveis de um job na fila.
/// </summary>
public enum JobStatus
{
    /// <summary>Aguardando ser puxado por um worker. Tambem e o estado de um job que falhou e vai ser retentado.</summary>
    Pending,

    /// <summary>Puxado por um worker e com lease ativo.</summary>
    Processing,

    /// <summary>Processado com sucesso.</summary>
    Done,

    /// <summary>Esgotou as tentativas. Fica na dead-letter ate alguem reenfileirar manualmente.</summary>
    Dead
}
