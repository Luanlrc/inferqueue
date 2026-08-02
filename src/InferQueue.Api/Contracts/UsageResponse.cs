using InferQueue.Core.Jobs;

namespace InferQueue.Api.Contracts;

/// <param name="JobsWithoutPrice">
/// Jobs cujo modelo nao esta na tabela de precos. Sao contados a parte para deixar claro
/// que <paramref name="TotalCostUsd"/> e um piso, nao o gasto fechado.
/// </param>
public sealed record UsageResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalJobs,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    decimal TotalCostUsd,
    int JobsWithoutPrice,
    IReadOnlyList<UsageByModel> ByModel)
{
    public static UsageResponse Build(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<UsageByModel> byModel) => new(
        from,
        to,
        byModel.Sum(m => m.Jobs),
        byModel.Sum(m => m.PromptTokens),
        byModel.Sum(m => m.CompletionTokens),
        byModel.Sum(m => m.CostUsd),
        byModel.Sum(m => m.JobsWithoutPrice),
        byModel);
}
