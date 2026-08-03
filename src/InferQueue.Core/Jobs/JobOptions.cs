using System.ComponentModel.DataAnnotations;

namespace InferQueue.Core.Jobs;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    /// <summary>
    /// Por quanto tempo o resultado de um job concluido pode ser reaproveitado por uma
    /// requisicao identica. Zero desliga o reaproveitamento de resultado — jobs em
    /// andamento continuam sendo deduplicados, porque disso depende o indice unico.
    /// </summary>
    [Range(0, 8760)]
    public int ResultReuseWindowHours { get; set; } = 24;

    public TimeSpan ResultReuseWindow => TimeSpan.FromHours(ResultReuseWindowHours);
}
