using InferQueue.Core.Jobs;
using Shouldly;

namespace InferQueue.UnitTests;

public sealed class JobHashTests
{
    [Fact]
    public void Mesma_entrada_e_mesmo_modelo_dao_o_mesmo_hash()
        => JobHash.Compute("texto", "gpt-4o-mini")
            .ShouldBe(JobHash.Compute("texto", "gpt-4o-mini"));

    [Fact]
    public void Modelo_diferente_muda_o_hash()
    {
        // O mesmo texto em modelos diferentes e trabalho diferente e nao deve deduplicar.
        JobHash.Compute("texto", "gpt-4o-mini")
            .ShouldNotBe(JobHash.Compute("texto", "gpt-4o"));
    }

    [Fact]
    public void Campos_nao_se_confundem_na_fronteira()
    {
        // Sem o prefixo de tamanho, os dois virariam a mesma string concatenada
        // e dois pedidos diferentes compartilhariam hash.
        JobHash.Compute(inputText: "b:c", model: "a")
            .ShouldNotBe(JobHash.Compute(inputText: "c", model: "a:b"));
    }
}
