using System.Text.Json.Nodes;
using InferQueue.Core.Jobs;

namespace InferQueue.Api.Contracts;

public sealed record JobResponse(
    Guid Id,
    string Status,
    string Model,
    int Attempts,
    JsonNode? Result,
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
        // A coluna e jsonb e guarda JSON. Devolver a string crua faria o cliente receber
        // um JSON escapado dentro de outro e ter que desserializar duas vezes.
        job.Result is null ? null : JsonNode.Parse(job.Result),
        job.Error,
        job.CreatedAt,
        job.CompletedAt);
}
