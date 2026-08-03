namespace InferQueue.Core.Llm;

/// <param name="Content">Texto devolvido pelo modelo.</param>
/// <param name="PromptTokens">Tokens consumidos pela entrada.</param>
/// <param name="CompletionTokens">Tokens gerados na saida.</param>
public sealed record LlmCompletion(string Content, int PromptTokens, int CompletionTokens);
