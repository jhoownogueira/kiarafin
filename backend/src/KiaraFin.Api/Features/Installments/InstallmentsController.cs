using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Installments;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/installments")]
public sealed class InstallmentsController(InstallmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InstallmentPlanResponse>>> List(Guid householdId, CancellationToken ct) =>
        Ok(await service.ListAsync(householdId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InstallmentPlanResponse>> Get(Guid householdId, Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(householdId, id, ct));

    [HttpPost]
    public async Task<ActionResult<InstallmentPlanResponse>> Create(
        Guid householdId, CreateInstallmentPlanRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(householdId, request, ct);
        return CreatedAtAction(nameof(Get), new { householdId, id = result.Id }, result);
    }
}
