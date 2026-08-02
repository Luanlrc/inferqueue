using InferQueue.Core.Jobs;

namespace InferQueue.UnitTests;

/// <summary>
/// Monta um <see cref="Job"/> em qualquer estado, escrevendo nos setters privados —
/// o mesmo que o EF Core faz ao materializar uma linha.
/// </summary>
/// <remarks>
/// Precisa existir porque <c>Attempts</c> so e incrementado pelo SQL da reserva, entao
/// nao ha caminho publico para chegar a um job com tentativas gastas. A alternativa seria
/// duplicar a regra de reserva no dominio so para os testes, o que seria pior.
/// </remarks>
internal static class JobFactory
{
    public static Job InState(
        JobStatus status,
        int attempts = 1,
        string input = "texto qualquer",
        string model = "gpt-4o-mini")
    {
        var job = Job.Create(input, model, DateTimeOffset.UnixEpoch);

        Set(job, nameof(Job.Status), status);
        Set(job, nameof(Job.Attempts), attempts);

        return job;
    }

    private static void Set(Job job, string property, object? value)
        => typeof(Job)
            .GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(job, [value]);
}
