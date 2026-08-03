using InferQueue.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace InferQueue.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class DeduplicationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Segundo_job_identico_em_andamento_e_barrado_pelo_banco()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await store.AddAsync(Job.Create("mesmo texto", "gpt-4o-mini", Agora));

        // A consulta previa da API evita o caso comum; este teste cobre a corrida,
        // em que dois inserts chegam ao banco antes de qualquer um enxergar o outro.
        await Should.ThrowAsync<DuplicateJobException>(
            () => store.AddAsync(Job.Create("mesmo texto", "gpt-4o-mini", Agora)));
    }

    [Fact]
    public async Task Texto_repetido_e_permitido_depois_do_job_anterior_concluir()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await store.AddAsync(Job.Create("mesmo texto", "gpt-4o-mini", Agora));

        var reservado = (await store.DequeueBatchAsync(1, Lease, Agora)).ShouldHaveSingleItem();
        reservado.MarkDone("resposta", 10, 5, 0.000009m, Agora);
        await store.UpdateAsync(reservado);

        // Reprocessar o mesmo texto meses depois e legitimo — o indice unico e parcial
        // justamente para nao proibir isto.
        await Should.NotThrowAsync(
            () => store.AddAsync(Job.Create("mesmo texto", "gpt-4o-mini", Agora.AddMonths(2))));
    }

    [Fact]
    public async Task Modelo_diferente_nao_deduplica()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await store.AddAsync(Job.Create("mesmo texto", "gpt-4o-mini", Agora));

        await Should.NotThrowAsync(
            () => store.AddAsync(Job.Create("mesmo texto", "gpt-4o", Agora)));
    }

    [Fact]
    public async Task Busca_por_reaproveitavel_respeita_a_janela_do_resultado()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        var job = Job.Create("texto", "gpt-4o-mini", Agora);
        await store.AddAsync(job);

        var reservado = (await store.DequeueBatchAsync(1, Lease, Agora)).ShouldHaveSingleItem();
        reservado.MarkDone("resposta", 10, 5, null, Agora);
        await store.UpdateAsync(reservado);

        var dentroDaJanela = await store.FindReusableAsync(job.InputHash, Agora.AddHours(-24));
        dentroDaJanela.ShouldNotBeNull();
        dentroDaJanela.Id.ShouldBe(job.Id);

        var foraDaJanela = await store.FindReusableAsync(job.InputHash, Agora.AddHours(24));
        foraDaJanela.ShouldBeNull();
    }
}
