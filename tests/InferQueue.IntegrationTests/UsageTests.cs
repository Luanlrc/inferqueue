using InferQueue.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace InferQueue.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class UsageTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Agrega_tokens_e_custo_por_modelo_e_separa_o_que_nao_tem_preco()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await ConcluirAsync(store, "texto A", "gpt-4o-mini", 10, 5, 0.000009m);
        await ConcluirAsync(store, "texto B", "gpt-4o-mini", 20, 10, 0.000018m);
        await ConcluirAsync(store, "texto C", "o3-mini", 7, 3, custo: null);

        var uso = await store.GetUsageAsync(Agora.AddHours(-1), Agora.AddHours(1));

        uso.Count.ShouldBe(2);

        var mini = uso.Single(u => u.Model == "gpt-4o-mini");
        mini.Jobs.ShouldBe(2);
        mini.PromptTokens.ShouldBe(30);
        mini.CompletionTokens.ShouldBe(15);
        mini.CostUsd.ShouldBe(0.000027m);
        mini.JobsWithoutPrice.ShouldBe(0);

        var semPreco = uso.Single(u => u.Model == "o3-mini");
        semPreco.CostUsd.ShouldBe(0m);
        // O job aparece contado a parte para o total nao passar por completo sem ser.
        semPreco.JobsWithoutPrice.ShouldBe(1);
    }

    [Fact]
    public async Task Ignora_jobs_fora_do_periodo()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await ConcluirAsync(store, "texto A", "gpt-4o-mini", 10, 5, 0.000009m);

        var uso = await store.GetUsageAsync(Agora.AddDays(1), Agora.AddDays(2));

        uso.ShouldBeEmpty();
    }

    private static async Task ConcluirAsync(
        IJobStore store,
        string texto,
        string modelo,
        int promptTokens,
        int completionTokens,
        decimal? custo)
    {
        await store.AddAsync(Job.Create(texto, modelo, Agora));

        var reservado = (await store.DequeueBatchAsync(1, TimeSpan.FromMinutes(2), Agora))
            .Single(j => j.InputText == texto);

        reservado.MarkDone("resposta", promptTokens, completionTokens, custo, Agora);
        await store.UpdateAsync(reservado);
    }
}
