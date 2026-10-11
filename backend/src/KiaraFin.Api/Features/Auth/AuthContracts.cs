using System.ComponentModel.DataAnnotations;
using KiaraFin.Api.Features.Identity;

namespace KiaraFin.Api.Features.Auth;

public sealed record RegisterRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required] string ConfirmPassword);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(128, MinimumLength = 1)] string Password);

public sealed record HouseholdSummary(Guid Id, string Name, HouseholdRole Role);
public sealed record UserResponse(Guid Id, string Name, string Email, IReadOnlyList<HouseholdSummary> Households);
public sealed record AuthResponse(string AccessToken, UserResponse User);

public sealed record AuthSession(
    AuthResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
