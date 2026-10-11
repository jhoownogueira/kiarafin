namespace KiaraFin.Api.Features.Identity;

public enum HouseholdRole
{
    Owner,
    Member
}

public sealed class User
{
    private User() { }

    public User(string name, string email, string normalizedEmail, string passwordHash)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public ICollection<HouseholdMembership> Memberships { get; private set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public sealed class Household
{
    private Household() { }

    public Household(string name, Guid createdByUserId)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public ICollection<HouseholdMembership> Memberships { get; private set; } = [];
    public ICollection<HouseholdInvitation> Invitations { get; private set; } = [];
}

public sealed class HouseholdMembership
{
    private HouseholdMembership() { }

    public HouseholdMembership(Guid householdId, Guid userId, HouseholdRole role)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public HouseholdRole Role { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public Household Household { get; private set; } = null!;
    public User User { get; private set; } = null!;
}

public sealed class HouseholdInvitation
{
    private HouseholdInvitation() { }

    public HouseholdInvitation(
        Guid householdId,
        Guid invitedByUserId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? invitedEmail)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        InvitedByUserId = invitedByUserId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        InvitedEmail = string.IsNullOrWhiteSpace(invitedEmail)
            ? null
            : invitedEmail.Trim().ToLowerInvariant();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string? InvitedEmail { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Household Household { get; private set; } = null!;

    public bool IsActive(DateTime utcNow) =>
        AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public void Accept(Guid userId, DateTime utcNow)
    {
        AcceptedByUserId = userId;
        AcceptedAtUtc = utcNow;
    }
}

public sealed class RefreshToken
{
    private RefreshToken() { }

    public RefreshToken(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public User User { get; private set; } = null!;

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public void Revoke(DateTime utcNow, Guid? replacementId = null)
    {
        if (RevokedAtUtc is not null)
            return;

        RevokedAtUtc = utcNow;
        ReplacedByTokenId = replacementId;
    }
}
