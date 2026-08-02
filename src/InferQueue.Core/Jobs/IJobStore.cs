namespace InferQueue.Core.Jobs;

/// <summary>
/// Porta de persistencia dos jobs. Fica no Core para que a API e o Worker
/// dependam da abstracao, e nao do EF Core.
/// </summary>
public interface IJobStore
{
    Task AddAsync(Job job, CancellationToken ct = default);

    Task<Job?> GetAsync(Guid id, CancellationToken ct = default);
}
