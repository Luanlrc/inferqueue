namespace InferQueue.Core.Jobs;

/// <param name="CostUsd">Soma dos custos conhecidos. Jobs de modelo fora da tabela nao entram.</param>
/// <param name="JobsWithoutPrice">Quantos jobs ficaram de fora do custo por falta de preco.</param>
public sealed record UsageByModel(
    string Model,
    int Jobs,
    long PromptTokens,
    long CompletionTokens,
    decimal CostUsd,
    int JobsWithoutPrice);
