using InferQueue.Api.Contracts;
using InferQueue.Core.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace InferQueue.Api.Endpoints;

public static class UsageEndpoints
{
    private const int DefaultWindowDays = 30;

    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/usage", GetAsync)
            .WithTags("Usage")
            .WithSummary("Tokens consumidos e custo dos jobs concluidos no periodo.");

        return app;
    }

    private static async Task<Results<Ok<UsageResponse>, ValidationProblem>> GetAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        IJobStore store,
        TimeProvider clock,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var start = from ?? now.AddDays(-DefaultWindowDays);
        var end = to ?? now;

        if (end <= start)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["to"] = ["O fim do periodo precisa ser posterior ao inicio."]
            });
        }

        var byModel = await store.GetUsageAsync(start, end, ct);

        return TypedResults.Ok(UsageResponse.Build(start, end, byModel));
    }
}
