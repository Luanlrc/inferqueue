using InferQueue.Core.Jobs;
using InferQueue.Core.Llm;
using Microsoft.Extensions.Options;

namespace InferQueue.Worker;

/// <summary>
/// Laco principal do worker: reserva um lote, processa job a job, repete.
/// </summary>
public sealed class JobDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    TimeProvider clock,
    ILogger<JobDispatcher> logger) : BackgroundService
{
    private readonly WorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker iniciado. Lote de {BatchSize}, lease de {Lease}, poll de {Poll}.",
            _options.BatchSize, _options.LeaseDuration, _options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;

            try
            {
                processed = await RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Banco fora do ar, por exemplo. Loga e tenta de novo no proximo ciclo:
                // derrubar o worker por causa de uma falha transitoria seria pior.
                logger.LogError(ex, "Falha ao processar o lote. Tentando novamente no proximo ciclo.");
            }

            // Teve trabalho, volta imediatamente — a fila pode estar cheia.
            // Nao teve, espera, para nao martelar o banco com consultas vazias.
            if (processed > 0)
            {
                continue;
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Worker encerrado.");
    }

    private async Task<int> RunBatchAsync(CancellationToken ct)
    {
        // Um escopo por lote: o DbContext e scoped e nao pode viver o processo inteiro.
        using var scope = scopeFactory.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
        var llm = scope.ServiceProvider.GetRequiredService<ILlmClient>();

        var jobs = await store.DequeueBatchAsync(
            _options.BatchSize,
            _options.LeaseDuration,
            clock.GetUtcNow(),
            ct);

        if (jobs.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("Reservados {Count} job(s).", jobs.Count);

        // Sequencial de proposito: o paralelismo deste sistema vem de rodar mais
        // instancias do worker, nao de disparar N chamadas dentro de uma so.
        foreach (var job in jobs)
        {
            await ProcessAsync(job, store, llm, ct);
        }

        return jobs.Count;
    }

    private async Task ProcessAsync(Job job, IJobStore store, ILlmClient llm, CancellationToken ct)
    {
        try
        {
            var completion = await llm.CompleteAsync(job.Model, job.InputText, ct);

            job.MarkDone(
                completion.Content,
                completion.PromptTokens,
                completion.CompletionTokens,
                clock.GetUtcNow());

            logger.LogInformation(
                "Job {JobId} concluido em {Model} ({PromptTokens}+{CompletionTokens} tokens).",
                job.Id, job.Model, completion.PromptTokens, completion.CompletionTokens);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown no meio do processamento: nao mexe no job. Ele fica em Processing
            // com o lease vencendo, e o reaper devolve para a fila.
            logger.LogWarning("Job {JobId} interrompido pelo shutdown; sera retomado.", job.Id);
            throw;
        }
        catch (Exception ex)
        {
            // Sem retry ainda: por enquanto qualquer falha vai direto para a dead-letter.
            job.MarkDead(ex.Message, clock.GetUtcNow());
            logger.LogError(ex, "Job {JobId} falhou.", job.Id);
        }

        await store.UpdateAsync(job, ct);
    }
}
