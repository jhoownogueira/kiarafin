using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KiaraFin.Api.Common.Exceptions;

namespace KiaraFin.Api.Common.Security;

public interface ICurrentUser
{
    Guid UserId { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(subject, out var userId)
                ? userId
                : throw new AuthenticationException("Sessão inválida ou expirada.");
        }
    }
}
