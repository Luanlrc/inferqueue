using System.Text.Json;
using InferQueue.Core.Jobs;
using Shouldly;

namespace InferQueue.UnitTests;

public sealed class JobTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    private static RetryPolicy Policy(int maxAttempts = 3)
        => new(maxAttempts, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10));

    [Fact]
    public void Create_nasce_pendente_e_sem_tentativas()
    {
        var job = Job.Create("texto", "gpt-4o-mini", Agora);

        job.Status.ShouldBe(JobStatus.Pending);
        job.Attempts.ShouldBe(0);
        job.NextAttemptAt.ShouldBe(Agora);
        job.LockedUntil.ShouldBeNull();
        job.InputHash.ShouldBe(JobHash.Compute("texto", "gpt-4o-mini"));
    }

    [Fact]
    public void MarkDone_grava_resultado_como_json_valido()
    {
        var job = JobFactory.InState(JobStatus.Processing);

        job.MarkDone("sentimento positivo", promptTokens: 10, completionTokens: 5, costUsd: 0.000123m, Agora);

        job.Status.ShouldBe(JobStatus.Done);
        job.CompletedAt.ShouldBe(Agora);
        job.LockedUntil.ShouldBeNull();
        job.CostUsd.ShouldBe(0.000123m);

        // A coluna e jsonb: se isto nao for JSON valido, o insert quebra no banco.
        var parsed = JsonDocument.Parse(job.Result!);
        parsed.RootElement.GetProperty("content").GetString().ShouldBe("sentimento positivo");
    }

    [Fact]
    public void MarkDone_so_vale_para_job_em_processamento()
    {
        var job = JobFactory.InState(JobStatus.Pending);

        Should.Throw<InvalidOperationException>(() =>
            job.MarkDone("x", 1, 1, null, Agora));
    }

    [Fact]
    public void Fail_com_tentativas_sobrando_volta_para_a_fila_com_espera()
    {
        var job = JobFactory.InState(JobStatus.Processing, attempts: 1);

        job.Fail("429 do provedor", Agora, Policy(maxAttempts: 3));

        job.Status.ShouldBe(JobStatus.Pending);
        job.NextAttemptAt.ShouldBeGreaterThan(Agora);
        job.LockedUntil.ShouldBeNull();
        job.CompletedAt.ShouldBeNull();
        job.Error.ShouldBe("429 do provedor");
    }

    [Fact]
    public void Fail_na_ultima_tentativa_vai_para_a_dead_letter()
    {
        var job = JobFactory.InState(JobStatus.Processing, attempts: 3);

        job.Fail("caiu de novo", Agora, Policy(maxAttempts: 3));

        job.Status.ShouldBe(JobStatus.Dead);
        job.CompletedAt.ShouldBe(Agora);
    }

    [Fact]
    public void Fail_permanente_mata_o_job_mesmo_com_tentativas_sobrando()
    {
        var job = JobFactory.InState(JobStatus.Processing, attempts: 1);

        job.Fail("401 chave invalida", Agora, Policy(maxAttempts: 5), isRetryable: false);

        // Insistir num 401 so gastaria tentativa e atrasaria a fila.
        job.Status.ShouldBe(JobStatus.Dead);
        job.Attempts.ShouldBe(1);
    }

    [Fact]
    public void Requeue_zera_o_historico_de_um_job_morto()
    {
        var job = JobFactory.InState(JobStatus.Processing, attempts: 3);
        job.Fail("morreu", Agora, Policy(maxAttempts: 3));

        job.Requeue(Agora.AddHours(1));

        job.Status.ShouldBe(JobStatus.Pending);
        job.Attempts.ShouldBe(0);
        job.Error.ShouldBeNull();
        job.CompletedAt.ShouldBeNull();
        job.NextAttemptAt.ShouldBe(Agora.AddHours(1));
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Done)]
    public void Requeue_so_vale_para_job_na_dead_letter(JobStatus status)
    {
        var job = JobFactory.InState(status);

        Should.Throw<InvalidOperationException>(() => job.Requeue(Agora));
    }
}
