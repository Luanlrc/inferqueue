using InferQueue.Core.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InferQueue.Infrastructure.Persistence;

internal sealed class EfJobStore(InferQueueDbContext db) : IJobStore
{
    public async Task AddAsync(Job job, CancellationToken ct = default)
    {
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
    }

    public Task<Job?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);

    /// <remarks>
    /// O coracao da fila. Tres detalhes que nao sao acidentais:
    ///
    /// 1. <c>FOR UPDATE SKIP LOCKED</c> — cada worker pula as linhas que outro ja travou
    ///    em vez de ficar esperando por elas. Sem isso, N workers formam fila atras do
    ///    mesmo job e o paralelismo vira zero.
    /// 2. Reserva e leitura na mesma instrucao — o CTE seleciona e o UPDATE marca
    ///    <c>Processing</c> num unico round trip, sem janela entre ver o job e reserva-lo.
    /// 3. A transacao fecha aqui, antes da chamada a LLM. Manter uma transacao aberta
    ///    durante uma chamada de rede de segundos seguraria locks e conexoes a toa;
    ///    quem protege o job durante o processamento e o lease em <c>locked_until</c>.
    /// </remarks>
    public async Task<IReadOnlyList<Job>> DequeueBatchAsync(
        int batchSize,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var lockedUntil = now.Add(leaseDuration);

        return await db.Jobs
            .FromSql(
                $"""
                 WITH claimed AS (
                     SELECT id
                     FROM jobs
                     WHERE status = 'Pending' AND next_attempt_at <= {now}
                     ORDER BY next_attempt_at
                     LIMIT {batchSize}
                     FOR UPDATE SKIP LOCKED
                 )
                 UPDATE jobs j
                 SET status = 'Processing',
                     locked_until = {lockedUntil},
                     attempts = j.attempts + 1
                 FROM claimed c
                 WHERE j.id = c.id
                 RETURNING j.*
                 """)
            .ToListAsync(ct);
    }

    /// <remarks>
    /// Mesmo padrao do dequeue, com uma diferenca: o status continua <c>Processing</c>.
    /// A linha so troca de dono — o lease e renovado em nome deste reaper — e quem decide
    /// se ela volta para a fila ou vai para a dead-letter e <see cref="Job.Fail"/>.
    /// Fazer essa escolha em SQL duplicaria a regra de retry em dois lugares.
    /// </remarks>
    public async Task<IReadOnlyList<Job>> ReclaimExpiredAsync(
        int batchSize,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var lockedUntil = now.Add(leaseDuration);

        return await db.Jobs
            .FromSql(
                $"""
                 WITH expired AS (
                     SELECT id
                     FROM jobs
                     WHERE status = 'Processing' AND locked_until < {now}
                     ORDER BY locked_until
                     LIMIT {batchSize}
                     FOR UPDATE SKIP LOCKED
                 )
                 UPDATE jobs j
                 SET locked_until = {lockedUntil}
                 FROM expired e
                 WHERE j.id = e.id
                 RETURNING j.*
                 """)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Job job, CancellationToken ct = default)
    {
        db.Jobs.Update(job);
        await db.SaveChangesAsync(ct);
    }
}
