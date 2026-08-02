using InferQueue.Core.Jobs;

namespace InferQueue.Api.Contracts;

public sealed record JobResponse(
    Guid Id,
    string Status,
    string Model,
    int Attempts,
    string? Result,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    // O InputText nao volta na resposta: quem mandou o texto ja o tem, e ele pode ser grande.
    public static JobResponse From(Job job) => new(
        job.Id,
        job.Status.ToString(),
        job.Model,
        job.Attempts,
        job.Result,
        job.Error,
        job.CreatedAt,
        job.CompletedAt);
}
