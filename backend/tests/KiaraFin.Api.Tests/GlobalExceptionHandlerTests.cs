using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Features.Pluggy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string> PluggyErrors => new()
    {
        { new PluggyConsentExpiredException("O consentimento Pluggy expirou."), 409, "Consentimento expirado" },
        { new PluggyIncompleteResponseException("Paginação incompleta."), 502, "Resposta incompleta da Pluggy" },
        { new PluggyUnavailableException("Falha de autenticação na Pluggy."), 503, "Pluggy indisponível" }
    };

    [Theory]
    [MemberData(nameof(PluggyErrors))]
    public async Task Pluggy_errors_return_actionable_problem_details(Exception exception, int status, string title)
    {
        var problemDetails = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(problemDetails, NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, exception, default);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(status, problemDetails.Problem?.Status);
        Assert.Equal(title, problemDetails.Problem?.Title);
        Assert.Equal(exception.Message, problemDetails.Problem?.Detail);
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Problem { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
