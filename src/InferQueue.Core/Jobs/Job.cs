using System.Text.Json;

namespace InferQueue.Core.Jobs;

/// <summary>
/// Uma unidade de trabalho na fila: um texto que sera enviado para a LLM.
/// Os setters sao privados de proposito — quem muda o estado do job sao os
/// metodos de dominio, nao quem consome a entidade.
/// </summary>
public sealed class Job
{
    private static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web);

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

    /// <summary>
    /// Registra o sucesso. Libera o lease para que a linha nao pareca mais em posse de ninguem.
    /// </summary>
    /// <param name="costUsd">
    /// Nulo quando o modelo nao esta na tabela de precos. Fica nulo mesmo, em vez de zero:
    /// zero seria uma mentira que sumiria no meio de um SUM.
    /// </param>
    public void MarkDone(
        string content,
        int promptTokens,
        int completionTokens,
        decimal? costUsd,
        DateTimeOffset now)
    {
        EnsureProcessing();

        // A coluna e jsonb, entao o que entra aqui precisa ser JSON valido.
        // Serializar aqui dentro mantem essa invariante junto da entidade.
        Result = JsonSerializer.Serialize(new { content }, ResultSerializerOptions);
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        CostUsd = costUsd;
        Error = null;
        LockedUntil = null;
        Status = JobStatus.Done;
        CompletedAt = now;
    }

    /// <summary>
    /// Registra a falha e decide o destino do job: volta para a fila com espera,
    /// ou vai para a dead-letter.
    /// </summary>
    /// <param name="isRetryable">
    /// Falso para erros que nao adianta repetir (prompt recusado, chave invalida).
    /// Nesses casos o job morre na hora, sem consumir as tentativas restantes.
    /// </param>
    public void Fail(string error, DateTimeOffset now, RetryPolicy policy, bool isRetryable = true)
    {
        EnsureProcessing();
        ArgumentNullException.ThrowIfNull(policy);

        Error = error;

        // Solta o lease em qualquer um dos dois caminhos: a linha nao esta mais em posse de ninguem.
        LockedUntil = null;

        if (!isRetryable || Attempts >= policy.MaxAttempts)
        {
            Status = JobStatus.Dead;
            CompletedAt = now;
            return;
        }

        Status = JobStatus.Pending;
        NextAttemptAt = now + policy.DelayFor(Attempts);
    }

    /// <summary>
    /// Ressuscita um job da dead-letter, zerando o historico de tentativas.
    /// E uma acao deliberada de operacao — normalmente depois de corrigir a causa da falha.
    /// </summary>
    public void Requeue(DateTimeOffset now)
    {
        if (Status is not JobStatus.Dead)
        {
            throw new InvalidOperationException(
                $"Job {Id} esta em {Status}; so um job em {nameof(JobStatus.Dead)} pode ser reenfileirado.");
        }

        Status = JobStatus.Pending;
        Attempts = 0;
        Error = null;
        LockedUntil = null;
        NextAttemptAt = now;
        CompletedAt = null;
    }

    private void EnsureProcessing()
    {
        if (Status is not JobStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Job {Id} esta em {Status}; so um job em {nameof(JobStatus.Processing)} pode ser finalizado.");
        }
    }
}
