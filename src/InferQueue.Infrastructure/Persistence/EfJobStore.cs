using InferQueue.Core.Jobs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InferQueue.Infrastructure.Persistence;

internal sealed class EfJobStore(InferQueueDbContext db) : IJobStore
{
    private const string InFlightUniqueIndex = "ux_jobs_input_hash_inflight";
    private const string UniqueViolation = "23505";

    public async Task AddAsync(Job job, CancellationToken ct = default)
    {
        db.Jobs.Add(job);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
                  {
                      SqlState: UniqueViolation,
                      ConstraintName: InFlightUniqueIndex
                  })
        {
            // Outra requisicao criou o mesmo job no intervalo entre a consulta e o insert.
            // O banco e quem arbitra a corrida; aqui so traduzimos para a linguagem do dominio.
            db.Entry(job).State = EntityState.Detached;
            throw new DuplicateJobException(job.InputHash, ex);
        }
    }

    public Task<Job?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<Job?> FindReusableAsync(
        string inputHash,
        DateTimeOffset reusableSince,
        CancellationToken ct = default)
        => db.Jobs
            .AsNoTracking()
            .Where(j => j.InputHash == inputHash
                        && (j.Status == JobStatus.Pending
                            || j.Status == JobStatus.Processing
                            || (j.Status == JobStatus.Done && j.CompletedAt >= reusableSince)))
            // O mais recente primeiro: se ha um concluido e um em andamento, o em andamento
            // e o que reflete o estado atual do pedido.
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UsageByModel>> GetUsageAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var rows = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Status == JobStatus.Done && j.CompletedAt >= from && j.CompletedAt < to)
            .GroupBy(j => j.Model)
            .Select(g => new UsageByModel(
                g.Key,
                g.Count(),
                g.Sum(j => (long)(j.PromptTokens ?? 0)),
                g.Sum(j => (long)(j.CompletionTokens ?? 0)),
                g.Sum(j => j.CostUsd ?? 0m),
                g.Count(j => j.CostUsd == null)))
            .ToListAsync(ct);

        // Ordenacao em memoria: o SQL nao consegue ordenar pela projecao, e o resultado
        // tem uma linha por modelo — ordenar isso no cliente nao custa nada.
        return [.. rows.OrderByDescending(u => u.CostUsd)];
    }

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

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
                  {
                      SqlState: UniqueViolation,
                      ConstraintName: InFlightUniqueIndex
                  })
        {
            // Acontece ao reenfileirar um job da dead-letter enquanto outro job com o
            // mesmo conteudo ja voltou a rodar.
            throw new DuplicateJobException(job.InputHash, ex);
        }
    }
}
