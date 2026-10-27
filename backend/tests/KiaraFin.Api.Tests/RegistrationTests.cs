using KiaraFin.Api.Features.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public async Task Public_registration_is_forbidden_when_disabled()
    {
        var controller = new AuthController(
            null!,
            null!,
            null!,
            null!,
            Options.Create(new RegistrationOptions { Enabled = false }));

        var response = await controller.Register(
            new RegisterRequest("Novo usuário", "novo@example.test", "password123", "password123"),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Cadastro desativado", problem.Title);
        Assert.Equal("O cadastro de novos usuários está desativado nesta instalação.", problem.Detail);
    }
}
