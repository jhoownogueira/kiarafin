using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Auth;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class IdentityFoundationTests
{
    [Fact]
    public void Refresh_tokens_are_random_and_only_their_hash_is_stable()
    {
        var service = TokenService();
        var first = service.CreateOpaqueToken();
        var second = service.CreateOpaqueToken();

        Assert.NotEqual(first, second);
        Assert.Equal(64, Convert.FromBase64String(first).Length);
        Assert.Equal(service.HashOpaqueToken(first), service.HashOpaqueToken(first));
        Assert.DoesNotContain(first, service.HashOpaqueToken(first), StringComparison.Ordinal);
    }

    [Fact]
    public void Invitation_expires_and_can_only_be_accepted_once()
    {
        var invitation = new HouseholdInvitation(
            Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow.AddMinutes(5), null);

        Assert.True(invitation.IsActive(DateTime.UtcNow));
        invitation.Accept(Guid.NewGuid(), DateTime.UtcNow);
        Assert.False(invitation.IsActive(DateTime.UtcNow));
    }

    [Fact]
    public async Task Members_from_another_household_are_not_visible()
    {
        await using var db = NewContext();
        var first = NewUser("first@example.test");
        var second = NewUser("second@example.test");
        var firstHousehold = new Household("Primeiro", first.Id);
        var secondHousehold = new Household("Segundo", second.Id);
        db.AddRange(first, second, firstHousehold, secondHousehold);
        db.HouseholdMemberships.AddRange(
            new HouseholdMembership(firstHousehold.Id, first.Id, HouseholdRole.Owner),
            new HouseholdMembership(secondHousehold.Id, second.Id, HouseholdRole.Owner));
        await db.SaveChangesAsync();
        var service = new HouseholdService(db, new StubCurrentUser(first.Id), TokenService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetMembersAsync(secondHousehold.Id, CancellationToken.None));
    }

    [Fact]
    public void Household_endpoints_require_authentication()
    {
        var authorization = typeof(HouseholdsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true);

        Assert.Single(authorization);
    }

    [Fact]
    public void Register_contract_matches_the_frontend_contract()
    {
        var names = typeof(RegisterRequest).GetProperties().Select(x => x.Name).ToArray();
        Assert.Equal(["Name", "Email", "Password", "ConfirmPassword"], names);
        var authNames = typeof(AuthResponse).GetProperties().Select(x => x.Name).ToArray();
        Assert.Equal(["AccessToken", "User"], authNames);
    }

    private static User NewUser(string email)
    {
        var normalized = email.ToUpperInvariant();
        var user = new User("Test user", email, normalized, string.Empty);
        user.UpdatePasswordHash("synthetic-hash");
        return user;
    }

    private static AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"kiarafin-tests-{Guid.NewGuid():N}")
            .Options);

    private static TokenService TokenService() => new(Options.Create(new JwtOptions
    {
        Issuer = "KiaraFin.Tests",
        Audience = "KiaraFin.Tests",
        Key = "synthetic-test-signing-key-with-at-least-32-bytes",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    }));

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}
