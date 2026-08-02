using InferQueue.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace InferQueue.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class JobQueueTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Reserva_marca_como_processando_e_conta_a_tentativa()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await store.AddAsync(Job.Create("texto", "gpt-4o-mini", Agora));

        var reservados = await store.DequeueBatchAsync(10, Lease, Agora);

        var job = reservados.ShouldHaveSingleItem();
        job.Status.ShouldBe(JobStatus.Processing);
        job.Attempts.ShouldBe(1);
        job.LockedUntil.ShouldBe(Agora.Add(Lease));
    }

    [Fact]
    public async Task Reserva_ignora_job_agendado_para_o_futuro()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        var job = Job.Create("texto", "gpt-4o-mini", Agora);
        await store.AddAsync(job);

        // Simula um job que falhou e esta cumprindo backoff.
        var reservado = await store.DequeueBatchAsync(10, Lease, Agora);
        reservado.ShouldHaveSingleItem().Fail("falhou", Agora, new RetryPolicy(3, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1)));
        await store.UpdateAsync(reservado[0]);

        var antesDoPrazo = await store.DequeueBatchAsync(10, Lease, Agora.AddMinutes(1));
        antesDoPrazo.ShouldBeEmpty();

        var depoisDoPrazo = await store.DequeueBatchAsync(10, Lease, Agora.AddHours(2));
        depoisDoPrazo.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Workers_concorrentes_nunca_reservam_o_mesmo_job()
    {
        const int totalJobs = 60;
        const int workers = 6;

        await using (var seed = fixture.CreateScope())
        {
            var store = seed.ServiceProvider.GetRequiredService<IJobStore>();

            for (var i = 0; i < totalJobs; i++)
            {
                await store.AddAsync(Job.Create($"texto numero {i}", "gpt-4o-mini", Agora));
            }
        }

        // Cada worker roda no proprio escopo, com a propria conexao — que e a condicao
        // para o SKIP LOCKED ter o que pular.
        var corridas = Enumerable.Range(0, workers).Select(async _ =>
        {
            var reservados = new List<Guid>();

            await using var scope = fixture.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

            while (true)
            {
                var lote = await store.DequeueBatchAsync(5, Lease, Agora);

                if (lote.Count == 0)
                {
                    break;
                }

                reservados.AddRange(lote.Select(j => j.Id));
            }

            return reservados;
        });

        var porWorker = await Task.WhenAll(corridas);
        var todos = porWorker.SelectMany(x => x).ToList();

        // A assercao que importa: nenhum id reservado duas vezes, e nenhum job esquecido.
        todos.Count.ShouldBe(totalJobs);
        todos.Distinct().Count().ShouldBe(totalJobs);
    }

    [Fact]
    public async Task Lease_vencido_e_retomado_e_lease_valido_nao()
    {
        await using var scope = fixture.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();

        await store.AddAsync(Job.Create("abandonado", "gpt-4o-mini", Agora));
        await store.AddAsync(Job.Create("em andamento", "gpt-4o-mini", Agora));

        await store.DequeueBatchAsync(10, Lease, Agora);

        // Um minuto depois o lease de 2 minutos ainda vale.
        var cedoDemais = await store.ReclaimExpiredAsync(10, Lease, Agora.AddMinutes(1));
        cedoDemais.ShouldBeEmpty();

        // Dez minutos depois, os dois estao vencidos.
        var vencidos = await store.ReclaimExpiredAsync(10, Lease, Agora.AddMinutes(10));
        vencidos.Count.ShouldBe(2);

        // Voltam ainda em Processing, com lease renovado: quem decide o destino e o dominio.
        vencidos.ShouldAllBe(j => j.Status == JobStatus.Processing);
        vencidos.ShouldAllBe(j => j.LockedUntil == Agora.AddMinutes(10).Add(Lease));
    }
}
