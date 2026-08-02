using InferQueue.Api.Contracts;
using InferQueue.Core.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

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

        return app;
    }

    private static async Task<Results<Accepted<JobResponse>, ValidationProblem>> CreateAsync(
        CreateJobRequest request,
        IJobStore store,
        TimeProvider clock,
        IConfiguration configuration,
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
            ? configuration["Llm:DefaultModel"]!
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
            ? TypedResults.Problem(
                title: "Job nao encontrado.",
                detail: $"Nao existe job com o id {id}.",
                statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(JobResponse.From(job));
    }
}
