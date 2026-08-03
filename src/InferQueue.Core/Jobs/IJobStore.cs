namespace InferQueue.Core.Jobs;

/// <summary>
/// Porta de persistencia dos jobs. Fica no Core para que a API e o Worker
/// dependam da abstracao, e nao do EF Core.
/// </summary>
public interface IJobStore
{
    /// <exception cref="DuplicateJobException">
    /// Ja existe um job nao concluido com o mesmo hash — corrida entre duas requisicoes.
    /// </exception>
    Task AddAsync(Job job, CancellationToken ct = default);

    /// <summary>
    /// Procura um job que torne desnecessario criar outro: um ainda em andamento com o
    /// mesmo conteudo, ou um ja concluido depois de <paramref name="reusableSince"/>.
    /// </summary>
    Task<Job?> FindReusableAsync(
        string inputHash,
        DateTimeOffset reusableSince,
        CancellationToken ct = default);

    /// <summary>Consumo de tokens e custo dos jobs concluidos no intervalo, por modelo.</summary>
    Task<IReadOnlyList<UsageByModel>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    Task<Job?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Reserva ate <paramref name="batchSize"/> jobs para este worker, marcando-os como
    /// <see cref="JobStatus.Processing"/> com um lease valido ate agora + <paramref name="leaseDuration"/>.
    /// A reserva e atomica: dois workers concorrentes nunca recebem o mesmo job.
    /// </summary>
    Task<IReadOnlyList<Job>> DequeueBatchAsync(
        int batchSize,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default);

    /// <summary>
    /// Retoma jobs cujo lease expirou — worker que morreu no meio do processamento.
    /// Devolve-os ainda em <see cref="JobStatus.Processing"/>, porem com o lease renovado
    /// em nome de quem chamou, para que a decisao de retentar ou matar seja tomada
    /// pelo dominio sem risco de outro processo mexer na mesma linha.
    /// </summary>
    Task<IReadOnlyList<Job>> ReclaimExpiredAsync(
        int batchSize,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task UpdateAsync(Job job, CancellationToken ct = default);
}
