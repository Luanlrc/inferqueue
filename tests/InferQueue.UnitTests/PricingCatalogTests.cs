using InferQueue.Core.Llm;
using Shouldly;

namespace InferQueue.UnitTests;

public sealed class PricingCatalogTests
{
    private static PricingCatalog Catalog() => new(new Dictionary<string, ModelPrice>
    {
        ["gpt-4o-mini"] = new() { InputPer1MTokens = 0.15m, OutputPer1MTokens = 0.60m }
    });

    [Fact]
    public void Estimate_soma_entrada_e_saida_pelo_preco_do_milhao()
    {
        // 1.000.000 de entrada = US$ 0,15; 1.000.000 de saida = US$ 0,60.
        var custo = Catalog().Estimate("gpt-4o-mini", 1_000_000, 1_000_000);

        custo.ShouldBe(0.75m);
    }

    [Fact]
    public void Estimate_arredonda_para_as_seis_casas_da_coluna()
    {
        // 13 * 0,15/1M + 12 * 0,60/1M = 0,00000915 -> 0,000009
        var custo = Catalog().Estimate("gpt-4o-mini", 13, 12);

        custo.ShouldBe(0.000009m);
    }

    [Fact]
    public void Estimate_devolve_nulo_para_modelo_desconhecido()
    {
        // Nulo e nao zero: zero sumiria dentro de um SUM e faria o relatorio de
        // custo parecer menor do que realmente e.
        Catalog().Estimate("modelo-que-nao-existe", 1000, 1000).ShouldBeNull();
    }

    [Fact]
    public void Knows_distingue_modelo_cadastrado()
    {
        Catalog().Knows("gpt-4o-mini").ShouldBeTrue();
        Catalog().Knows("o3-mini").ShouldBeFalse();
    }
}
