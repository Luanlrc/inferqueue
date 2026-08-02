using System.ComponentModel.DataAnnotations;

namespace InferQueue.Worker;

public sealed class WorkerOptions
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

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseSeconds);
}
