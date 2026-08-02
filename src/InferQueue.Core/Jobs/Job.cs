namespace InferQueue.Core.Jobs;

/// <summary>
/// Uma unidade de trabalho na fila: um texto que sera enviado para a LLM.
/// Os setters sao privados de proposito — quem muda o estado do job sao os
/// metodos de dominio, nao quem consome a entidade.
/// </summary>
public sealed class Job
{
    // O EF Core precisa de um construtor sem parametros; ele consegue usar um privado.
    private Job()
    {
        InputHash = string.Empty;
        InputText = string.Empty;
        Model = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>SHA-256 de modelo + texto. E a chave de deduplicacao.</summary>
    public string InputHash { get; private set; }

    public string InputText { get; private set; }

    public string Model { get; private set; }

    public JobStatus Status { get; private set; }

    /// <summary>Quantas vezes um worker ja tentou processar este job.</summary>
    public int Attempts { get; private set; }

    /// <summary>Antes deste instante o job nao deve ser puxado. E o que implementa o backoff.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Ate quando o lease do worker atual vale. Passou disso, outro worker pode retomar.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public string? Result { get; private set; }

    public string? Error { get; private set; }

    public int? PromptTokens { get; private set; }

    public int? CompletionTokens { get; private set; }

    public decimal? CostUsd { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static Job Create(string inputText, string model, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputText);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return new Job
        {
            // v7 e sequencial no tempo: evita fragmentar o indice da PK como um Guid aleatorio faria.
            Id = Guid.CreateVersion7(),
            InputHash = JobHash.Compute(inputText, model),
            InputText = inputText,
            Model = model,
            Status = JobStatus.Pending,
            Attempts = 0,
            NextAttemptAt = now,
            CreatedAt = now
        };
    }
}
