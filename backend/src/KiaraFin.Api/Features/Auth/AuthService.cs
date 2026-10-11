using System.Data;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Categories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext dbContext,
    IPasswordService passwordService,
    ITokenService tokenService,
    DefaultCategorySeeder categorySeeder)
{
    public async Task<AuthSession> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new AppValidationException("A confirmação da senha não confere.");

        var normalizedEmail = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new ConflictException("Este e-mail já está cadastrado.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new User(request.Name, request.Email, normalizedEmail, string.Empty);
        user.UpdatePasswordHash(passwordService.Hash(user, request.Password));
        var firstName = request.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var household = new Household($"Finanças de {firstName}", user.Id);
        var membership = new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner);

        dbContext.Users.Add(user);
        dbContext.Households.Add(household);
        dbContext.HouseholdMemberships.Add(membership);
        await categorySeeder.EnsureSeededAsync(household.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var session = await CreateSessionAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is null)
            throw InvalidCredentials();

        var verification = passwordService.Verify(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            throw InvalidCredentials();
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.UpdatePasswordHash(passwordService.Hash(user, request.Password));

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthSession> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashOpaqueToken(rawToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var current = await dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (current is null)
            throw new AuthenticationException("Sessão inválida ou expirada.");

        var utcNow = DateTime.UtcNow;
        if (!current.IsActive(utcNow))
        {
            var activeFamilyTokens = await dbContext.RefreshTokens
                .Where(x => x.FamilyId == current.FamilyId && x.RevokedAtUtc == null && x.ExpiresAtUtc > utcNow)
                .ToListAsync(cancellationToken);
            foreach (var familyToken in activeFamilyTokens)
                familyToken.Revoke(utcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new AuthenticationException("Sessão inválida ou expirada.");
        }

        var rawReplacement = tokenService.CreateOpaqueToken();
        var replacement = new RefreshToken(
            current.UserId,
            current.FamilyId,
            tokenService.HashOpaqueToken(rawReplacement),
            tokenService.GetRefreshExpirationUtc());
        current.Revoke(utcNow, replacement.Id);
        dbContext.RefreshTokens.Add(replacement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AuthenticationException("Sessão inválida ou expirada.");
        }

        return new AuthSession(
            new AuthResponse(tokenService.CreateAccessToken(current.User).Token, await MapUserAsync(current.User, cancellationToken)),
            rawReplacement,
            replacement.ExpiresAtUtc);
    }

    public async Task LogoutAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return;

        var tokenHash = tokenService.HashOpaqueToken(rawToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (token is null || !token.IsActive(DateTime.UtcNow))
            return;
        token.Revoke(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");
        return await MapUserAsync(user, cancellationToken);
    }

    private async Task<AuthSession> CreateSessionAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var rawRefreshToken = tokenService.CreateOpaqueToken();
        var refreshToken = new RefreshToken(
            user.Id,
            Guid.NewGuid(),
            tokenService.HashOpaqueToken(rawRefreshToken),
            tokenService.GetRefreshExpirationUtc());
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AuthSession(
            new AuthResponse(accessToken.Token, await MapUserAsync(user, cancellationToken)),
            rawRefreshToken,
            refreshToken.ExpiresAtUtc);
    }

    private async Task<UserResponse> MapUserAsync(User user, CancellationToken cancellationToken)
    {
        var households = await dbContext.HouseholdMemberships
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .OrderBy(x => x.JoinedAtUtc)
            .Select(x => new HouseholdSummary(x.HouseholdId, x.Household.Name, x.Role))
            .ToListAsync(cancellationToken);
        return new UserResponse(user.Id, user.Name, user.Email, households);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static AuthenticationException InvalidCredentials() => new("E-mail ou senha inválidos.");
}
