using System.ComponentModel.DataAnnotations;

namespace InferQueue.Core.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>
    /// Chave da OpenAI. Vazia faz o sistema cair no cliente fake — util para rodar
    /// local sem gastar. Nunca deve ser versionada; use `dotnet user-secrets`.
    /// </summary>
    public string? ApiKey { get; set; }

    [Required]
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>Modelo usado quando o cliente nao especifica um.</summary>
    [Required]
    public string DefaultModel { get; set; } = "gpt-4o-mini";

    [Required]
    public string SystemPrompt { get; set; } =
        "Voce analisa o sentimento de textos. Responda em uma frase curta, "
        + "classificando como positivo, negativo ou neutro e justificando brevemente.";

    /// <summary>Teto de tempo para uma unica chamada ao provedor.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Preco por modelo. Modelo ausente daqui tem o job processado normalmente, mas
    /// fica sem custo registrado — e aparece separado no relatorio de uso.
    /// </summary>
    public Dictionary<string, ModelPrice> Pricing { get; set; } = [];
}
