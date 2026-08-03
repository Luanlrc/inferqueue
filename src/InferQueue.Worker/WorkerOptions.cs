using System.ComponentModel.DataAnnotations;
using InferQueue.Core.Jobs;

namespace InferQueue.Worker;

public sealed class WorkerOptions : IValidatableObject
{
    public const string SectionName = "Worker";

    /// <summary>Quantos jobs sao reservados por rodada.</summary>
    [Range(1, 100)]
    public int BatchSize { get; set; } = 5;

    /// <summary>Espera entre rodadas quando a fila esta vazia.</summary>
    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// Validade do lease. Precisa ser maior que o pior tempo esperado de uma chamada a LLM:
    /// se expirar antes, outro worker retoma um job que ainda esta sendo processado.
    /// </summary>
    [Range(10, 3600)]
    public int LeaseSeconds { get; set; } = 120;

    /// <summary>Tentativas antes de o job ir para a dead-letter.</summary>
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Espera da primeira retentativa; dobra a cada falha seguinte.</summary>
    [Range(1, 3600)]
    public int BaseBackoffSeconds { get; set; } = 5;

    /// <summary>Teto do backoff exponencial.</summary>
    [Range(1, 86400)]
    public int MaxBackoffSeconds { get; set; } = 300;

    /// <summary>Frequencia com que o reaper procura leases vencidos.</summary>
    [Range(5, 3600)]
    public int ReaperIntervalSeconds { get; set; } = 30;

    [Range(1, 500)]
    public int ReaperBatchSize { get; set; } = 20;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseSeconds);

    public TimeSpan ReaperInterval => TimeSpan.FromSeconds(ReaperIntervalSeconds);

    public RetryPolicy ToRetryPolicy() => new(
        MaxAttempts,
        TimeSpan.FromSeconds(BaseBackoffSeconds),
        TimeSpan.FromSeconds(MaxBackoffSeconds));

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxBackoffSeconds < BaseBackoffSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(MaxBackoffSeconds)} nao pode ser menor que {nameof(BaseBackoffSeconds)}.",
                [nameof(MaxBackoffSeconds)]);
        }

        // Reaper mais lento que o lease deixa jobs orfaos parados por mais tempo que o
        // necessario; nao e erro fatal, mas com essa margem invertida o reaper vira enfeite.
        if (ReaperIntervalSeconds > LeaseSeconds)
        {
            yield return new ValidationResult(
                $"{nameof(ReaperIntervalSeconds)} deveria ser menor que {nameof(LeaseSeconds)}, "
                + "senao um job orfao espera mais que o dobro do lease para voltar a fila.",
                [nameof(ReaperIntervalSeconds)]);
        }
    }
}
