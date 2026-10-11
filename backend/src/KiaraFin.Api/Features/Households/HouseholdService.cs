using System.Data;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Households;

public sealed class HouseholdService(
    AppDbContext dbContext,
    ICurrentUser currentUser,
    ITokenService tokenService)
{
    public async Task<InvitationResponse> CreateInvitationAsync(
        Guid householdId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, HouseholdRole.Owner, cancellationToken);
        var invitedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim().ToLowerInvariant();

        if (invitedEmail is not null)
        {
            var alreadyMember = await dbContext.HouseholdMemberships.AnyAsync(
                x => x.HouseholdId == householdId && x.User.NormalizedEmail == invitedEmail.ToUpperInvariant(),
                cancellationToken);
            if (alreadyMember)
                throw new ConflictException("Esta pessoa já faz parte do núcleo financeiro.");
        }

        var rawToken = tokenService.CreateOpaqueToken();
        var invitation = new HouseholdInvitation(
            householdId,
            currentUser.UserId,
            tokenService.HashOpaqueToken(rawToken),
            DateTime.UtcNow.AddDays(7),
            invitedEmail);
        dbContext.HouseholdInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new InvitationResponse(invitation.Id, rawToken, invitation.ExpiresAtUtc, invitation.InvitedEmail);
    }

    public async Task<HouseholdResponse> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var tokenHash = tokenService.HashOpaqueToken(request.Token);
        var invitation = await dbContext.HouseholdInvitations
            .Include(x => x.Household)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new AppValidationException("Convite inválido ou expirado.");
        var utcNow = DateTime.UtcNow;
        if (!invitation.IsActive(utcNow))
            throw new AppValidationException("Convite inválido ou expirado.");

        var user = await dbContext.Users.SingleAsync(x => x.Id == currentUser.UserId, cancellationToken);
        if (invitation.InvitedEmail is not null &&
            !string.Equals(invitation.InvitedEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Este convite foi emitido para outro e-mail.");

        var membership = await dbContext.HouseholdMemberships.SingleOrDefaultAsync(
            x => x.HouseholdId == invitation.HouseholdId && x.UserId == user.Id,
            cancellationToken);
        if (membership is null)
        {
            membership = new HouseholdMembership(invitation.HouseholdId, user.Id, HouseholdRole.Member);
            dbContext.HouseholdMemberships.Add(membership);
        }
        invitation.Accept(user.Id, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new HouseholdResponse(invitation.HouseholdId, invitation.Household.Name, membership.Role);
    }

    public async Task<IReadOnlyList<HouseholdMemberResponse>> GetMembersAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, null, cancellationToken);
        return await dbContext.HouseholdMemberships
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.JoinedAtUtc)
            .Select(x => new HouseholdMemberResponse(
                x.UserId,
                x.User.Name,
                x.User.Email,
                x.Role,
                x.JoinedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task RequireMembershipAsync(
        Guid householdId,
        HouseholdRole? requiredRole,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.HouseholdMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.HouseholdId == householdId && x.UserId == currentUser.UserId,
                cancellationToken);
        if (membership is null)
            throw new NotFoundException("Núcleo financeiro não encontrado.");
        if (requiredRole is not null && membership.Role != requiredRole)
            throw new ForbiddenException("Você não possui permissão para gerenciar convites deste núcleo.");
    }
}
