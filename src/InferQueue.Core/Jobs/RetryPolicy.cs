namespace InferQueue.Core.Jobs;

/// <summary>
/// Quantas vezes insistir num job e quanto esperar entre as tentativas.
/// </summary>
public sealed class RetryPolicy
{
    // 2^31 segundos ja passa de 60 anos; o teto existe so para o calculo nao estourar.
    private const int MaxExponent = 30;

    public RetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);

        MaxAttempts = maxAttempts;
        BaseDelay = baseDelay;
        MaxDelay = maxDelay;
    }

    public int MaxAttempts { get; }

    public TimeSpan BaseDelay { get; }

    public TimeSpan MaxDelay { get; }

    /// <summary>
    /// Espera antes da proxima tentativa: exponencial, limitada por <see cref="MaxDelay"/>
    /// e com jitter.
    /// </summary>
    /// <remarks>
    /// O jitter nao e enfeite. Quando a OpenAI devolve 429 para um lote inteiro, todos os
    /// jobs falham no mesmo instante; sem jitter todos voltariam a bater exatamente juntos,
    /// mantendo o rate limit estourado indefinidamente. Espalhar +-20% quebra esse sincronismo.
    /// </remarks>
    public TimeSpan DelayFor(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var exponent = Math.Min(attempt - 1, MaxExponent);
        var seconds = BaseDelay.TotalSeconds * Math.Pow(2, exponent);
        var capped = Math.Min(seconds, MaxDelay.TotalSeconds);
        var jitter = 1 + ((Random.Shared.NextDouble() * 0.4) - 0.2);

        return TimeSpan.FromSeconds(capped * jitter);
    }
}
