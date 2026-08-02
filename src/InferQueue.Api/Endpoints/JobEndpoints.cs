using InferQueue.Api.Contracts;
using InferQueue.Core.Jobs;
using InferQueue.Core.Llm;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace InferQueue.Api.Endpoints;

public static class JobEndpoints
{
    private const int MaxInputLength = 10_000;

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/jobs").WithTags("Jobs");

        group.MapPost("/", CreateAsync)
            .WithSummary("Enfileira um job. Responde na hora, sem esperar a LLM.");

        group.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Consulta o estado de um job.");

        group.MapPost("/{id:guid}/retry", RetryAsync)
            .WithSummary("Devolve a fila um job que parou na dead-letter.");

        return app;
    }

    private static async Task<Results<Accepted<JobResponse>, ValidationProblem>> CreateAsync(
        CreateJobRequest request,
        IJobStore store,
        TimeProvider clock,
        IOptions<LlmOptions> llmOptions,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            errors[nameof(request.Input)] = ["O texto de entrada e obrigatorio."];
        }
        else if (request.Input.Length > MaxInputLength)
        {
            errors[nameof(request.Input)] = [$"O texto de entrada excede {MaxInputLength} caracteres."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? llmOptions.Value.DefaultModel
            : request.Model;

        var job = Job.Create(request.Input!, model, clock.GetUtcNow());
        await store.AddAsync(job, ct);

        // 202 e nao 201: o recurso existe, mas o trabalho dele ainda nao aconteceu.
        return TypedResults.Accepted($"/v1/jobs/{job.Id}", JobResponse.From(job));
    }

    private static async Task<Results<Ok<JobResponse>, ProblemHttpResult>> GetAsync(
        Guid id,
        IJobStore store,
        CancellationToken ct)
    {
        var job = await store.GetAsync(id, ct);

        return job is null
            ? NotFound(id)
            : TypedResults.Ok(JobResponse.From(job));
    }

    private static async Task<Results<Ok<JobResponse>, ProblemHttpResult>> RetryAsync(
        Guid id,
        IJobStore store,
        TimeProvider clock,
        CancellationToken ct)
    {
        var job = await store.GetAsync(id, ct);

        if (job is null)
        {
            return NotFound(id);
        }

        if (job.Status is not JobStatus.Dead)
        {
            // 409 e nao 400: o pedido esta bem formado, o estado atual do recurso e que nao permite.
            return TypedResults.Problem(
                title: "Job nao esta na dead-letter.",
                detail: $"O job {id} esta em {job.Status}; so jobs em Dead podem ser reenfileirados.",
                statusCode: StatusCodes.Status409Conflict);
        }

        job.Requeue(clock.GetUtcNow());
        await store.UpdateAsync(job, ct);

        return TypedResults.Ok(JobResponse.From(job));
    }

    private static ProblemHttpResult NotFound(Guid id)
        => TypedResults.Problem(
            title: "Job nao encontrado.",
            detail: $"Nao existe job com o id {id}.",
            statusCode: StatusCodes.Status404NotFound);
}
