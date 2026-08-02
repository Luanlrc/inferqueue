namespace InferQueue.Core.Llm;

/// <summary>
/// Converte tokens em dolares. A tabela vem de configuracao, nao de codigo: preco de
/// modelo muda sem aviso e nao deveria exigir recompilar nada.
/// </summary>
public sealed class PricingCatalog(IReadOnlyDictionary<string, ModelPrice> prices)
{
    private const decimal TokensPerUnit = 1_000_000m;

    /// <summary>
    /// Custo da chamada, ou <c>null</c> se o modelo nao esta na tabela.
    /// Null e deliberado: registrar zero para um modelo desconhecido esconderia gasto real
    /// no relatorio, e um total que parece barato demais e pior que um total incompleto.
    /// </summary>
    public decimal? Estimate(string model, int promptTokens, int completionTokens)
    {
        if (!prices.TryGetValue(model, out var price))
        {
            return null;
        }

        var input = price.InputPer1MTokens * promptTokens / TokensPerUnit;
        var output = price.OutputPer1MTokens * completionTokens / TokensPerUnit;

        // 6 casas: a coluna e numeric(10,6) e uma chamada curta custa fracao de centavo.
        return Math.Round(input + output, 6, MidpointRounding.AwayFromZero);
    }

    public bool Knows(string model) => prices.ContainsKey(model);
}
