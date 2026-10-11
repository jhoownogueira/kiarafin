using System.ComponentModel.DataAnnotations;
using KiaraFin.Api.Features.Identity;

namespace KiaraFin.Api.Features.Households;

public sealed record CreateInvitationRequest(
    [EmailAddress, StringLength(320)] string? Email);

public sealed record AcceptInvitationRequest(
    [Required, StringLength(256, MinimumLength = 40)] string Token);

public sealed record InvitationResponse(Guid Id, string Token, DateTime ExpiresAtUtc, string? InvitedEmail);
public sealed record HouseholdResponse(Guid Id, string Name, HouseholdRole Role);
public sealed record HouseholdMemberResponse(Guid UserId, string Name, string Email, HouseholdRole Role, DateTime JoinedAtUtc);
