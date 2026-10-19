using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KiaraFin.Api.Features.Pluggy;

namespace KiaraFin.Api.Common.Exceptions;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            AppValidationException => (400, "Dados inválidos", exception.Message),
            ArgumentException => (400, "Dados inválidos", exception.Message),
            AuthenticationException => (401, "Não autenticado", exception.Message),
            ForbiddenException => (403, "Acesso negado", exception.Message),
            NotFoundException => (404, "Recurso não encontrado", exception.Message),
            ConflictException => (409, "Conflito", exception.Message),
            DbUpdateConcurrencyException => (409, "Conflito", "Os dados foram alterados por outra operação. Tente novamente."),
            DbUpdateException => (409, "Conflito", "Não foi possível concluir a operação devido a um conflito de dados."),
            PluggyConsentExpiredException => (409, "Consentimento expirado", exception.Message),
            PluggyIncompleteResponseException => (502, "Resposta incompleta da Pluggy", exception.Message),
            PluggyUnavailableException => (503, "Pluggy indisponível", exception.Message),
            _ => (500, "Erro inesperado", "Ocorreu um erro inesperado.")
        };

        if (status == 500)
            logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
        else
            logger.LogWarning("Request failed with {StatusCode} for {Path}: {Message}", status, httpContext.Request.Path, exception.Message);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            }
        });
    }
}
