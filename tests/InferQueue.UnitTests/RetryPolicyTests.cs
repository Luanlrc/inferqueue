using InferQueue.Core.Jobs;
using Shouldly;

namespace InferQueue.UnitTests;

public sealed class RetryPolicyTests
{
    private static RetryPolicy Policy(int maxAttempts = 5, int baseSeconds = 10, int maxSeconds = 600)
        => new(maxAttempts, TimeSpan.FromSeconds(baseSeconds), TimeSpan.FromSeconds(maxSeconds));

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    public void DelayFor_dobra_a_cada_tentativa_dentro_do_jitter(int attempt, double esperadoSemJitter)
    {
        var delay = Policy().DelayFor(attempt).TotalSeconds;

        // O jitter e de +-20%, entao a assercao e sobre a faixa, nao sobre um valor exato.
        delay.ShouldBeInRange(esperadoSemJitter * 0.8, esperadoSemJitter * 1.2);
    }

    [Fact]
    public void DelayFor_respeita_o_teto()
    {
        var delay = Policy(baseSeconds: 10, maxSeconds: 60).DelayFor(attempt: 20).TotalSeconds;

        // Sem o teto, 2^19 * 10s daria mais de 60 dias.
        delay.ShouldBeInRange(60 * 0.8, 60 * 1.2);
    }

    [Fact]
    public void DelayFor_nao_estoura_com_tentativa_absurda()
    {
        // O expoente e limitado justamente para o calculo nao virar infinito e quebrar
        // o TimeSpan. Um job so chegaria aqui por dado corrompido, mas nao pode derrubar o worker.
        Should.NotThrow(() => Policy().DelayFor(attempt: int.MaxValue));
    }

    [Fact]
    public void DelayFor_rejeita_tentativa_invalida()
        => Should.Throw<ArgumentOutOfRangeException>(() => Policy().DelayFor(attempt: 0));

    [Fact]
    public void Construtor_rejeita_teto_menor_que_a_base()
        => Should.Throw<ArgumentOutOfRangeException>(() =>
            new RetryPolicy(3, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10)));

    [Fact]
    public void Jitter_produz_valores_diferentes()
    {
        var policy = Policy();

        var amostras = Enumerable.Range(0, 50)
            .Select(_ => policy.DelayFor(3))
            .Distinct()
            .Count();

        // Se todos sairem iguais, o jitter nao esta sendo aplicado e um lote inteiro
        // rejeitado por 429 voltaria a bater no mesmo instante.
        amostras.ShouldBeGreaterThan(1);
    }
}
