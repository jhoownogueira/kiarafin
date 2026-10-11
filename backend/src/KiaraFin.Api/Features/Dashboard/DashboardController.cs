using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Dashboard;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/dashboard")]
public sealed class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(
        Guid householdId, [FromQuery] string? month, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(householdId, month, cancellationToken));
}
