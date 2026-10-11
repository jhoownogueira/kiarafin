using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Features.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KiaraFin.Api.Common.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}

public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordVerificationResult Verify(User user, string passwordHash, string password);
}

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    public string Hash(User user, string password) => _hasher.HashPassword(user, password);
    public PasswordVerificationResult Verify(User user, string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(user, passwordHash, password);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);
    string CreateOpaqueToken();
    string HashOpaqueToken(string token);
    DateTime GetRefreshExpirationUtc();
}

public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult CreateAccessToken(User user)
    {
        if (Encoding.UTF8.GetByteCount(_options.Key) < 32)
            throw new InvalidOperationException("Jwt:Key deve possuir pelo menos 32 bytes.");

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);
        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }

    public string CreateOpaqueToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashOpaqueToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public DateTime GetRefreshExpirationUtc() =>
        DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
}

public interface IRequestOriginValidator
{
    void EnsureAllowed(HttpRequest request);
}

public sealed class RequestOriginValidator(IConfiguration configuration) : IRequestOriginValidator
{
    private readonly HashSet<string> _allowed = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?.Select(x => x.TrimEnd('/'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

    public void EnsureAllowed(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSite) &&
            string.Equals(fetchSite.ToString(), "cross-site", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Origem da requisição não permitida.");

        if (request.Headers.TryGetValue("Origin", out var origin) &&
            !_allowed.Contains(origin.ToString().TrimEnd('/')))
            throw new ForbiddenException("Origem da requisição não permitida.");
    }
}
