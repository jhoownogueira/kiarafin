using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Households;

[ApiController]
[Authorize]
[Route("api/households")]
public sealed class HouseholdsController(HouseholdService householdService) : ControllerBase
{
    [HttpPost("{householdId:guid}/invitations")]
    public async Task<ActionResult<InvitationResponse>> CreateInvitation(
        Guid householdId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var invitation = await householdService.CreateInvitationAsync(householdId, request, cancellationToken);
        return Created($"/api/households/{householdId}/invitations/{invitation.Id}", invitation);
    }

    [HttpPost("invitations/accept")]
    public async Task<ActionResult<HouseholdResponse>> AcceptInvitation(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await householdService.AcceptInvitationAsync(request, cancellationToken));

    [HttpGet("{householdId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<HouseholdMemberResponse>>> GetMembers(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Ok(await householdService.GetMembersAsync(householdId, cancellationToken));
}
