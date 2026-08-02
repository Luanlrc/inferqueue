using Microsoft.AspNetCore.Diagnostics;

namespace InferQueue.Api.ErrorHandling;

/// <summary>
/// Corpo malformado e erro de quem chamou, nao do servidor. Sem este handler o
/// UseExceptionHandler transforma qualquer BadHttpRequestException em 500, o que
/// faz o cliente achar que a API quebrou quando na verdade o JSON dele estava torto.
/// </summary>
internal sealed class BadHttpRequestExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            // Nao e comigo: deixa o pipeline seguir para o tratamento padrao (500).
            return false;
        }

        httpContext.Response.StatusCode = badRequest.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = badRequest.StatusCode,
                Title = "Requisicao invalida.",
                Detail = badRequest.Message
            }
        });
    }
}
