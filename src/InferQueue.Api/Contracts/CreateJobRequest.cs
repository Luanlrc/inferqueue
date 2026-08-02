namespace InferQueue.Api.Contracts;

/// <param name="Input">Texto que sera enviado para a LLM.</param>
/// <param name="Model">Modelo a usar. Se omitido, cai no padrao configurado.</param>
public sealed record CreateJobRequest(string? Input, string? Model);
