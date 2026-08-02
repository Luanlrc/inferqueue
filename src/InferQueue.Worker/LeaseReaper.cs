using InferQueue.Core.Jobs;
using Microsoft.Extensions.Options;

namespace InferQueue.Worker;

/// <summary>
/// Recupera jobs abandonados: worker que caiu, container que foi morto, deploy no meio
/// do processamento. Sem isto a linha fica em <c>Processing</c> para sempre — foi
/// exatamente o que aconteceu no primeiro teste de carga, e teve que ser desfeito na mao.
/// </summary>
public sealed class LeaseReaper(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    RetryPolicy retryPolicy,
    TimeProvider clock,
    ILogger<LeaseReaper> logger) : BackgroundService
{
    private readonly WorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Reaper iniciado. Varredura a cada {Interval}.", _options.ReaperInterval);

        using var timer = new PeriodicTimer(_options.ReaperInterval, clock);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                await ReclaimAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na varredura de leases. Tentando na proxima.");
            }
        }

        logger.LogInformation("Reaper encerrado.");
    }

    private async Task ReclaimAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        var now = clock.GetUtcNow();

        var abandoned = await store.ReclaimExpiredAsync(
            _options.ReaperBatchSize,
            _options.LeaseDuration,
            now,
            ct);

        if (abandoned.Count == 0)
        {
            return;
        }

        logger.LogWarning("{Count} job(s) com lease vencido; devolvendo a fila.", abandoned.Count);

        foreach (var job in abandoned)
        {
            // Mesma regra de uma falha qualquer: respeita MaxAttempts e o backoff.
            // A tentativa ja foi contada na reserva, entao nao se incrementa nada aqui.
            job.Fail("Lease expirado: o worker que reservou este job nao concluiu.", now, retryPolicy);

            await store.UpdateAsync(job, ct);

            logger.LogWarning(
                "Job {JobId} recuperado apos lease vencido; novo estado {Status} (tentativa {Attempts}/{Max}).",
                job.Id, job.Status, job.Attempts, retryPolicy.MaxAttempts);
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
