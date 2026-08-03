namespace InferQueue.Core.Llm;

/// <summary>
/// Preco de um modelo, em dolares por milhao de tokens — a unidade em que a OpenAI publica.
/// </summary>
public sealed class ModelPrice
{
    public decimal InputPer1MTokens { get; set; }

    public decimal OutputPer1MTokens { get; set; }
}
