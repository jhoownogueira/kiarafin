using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Forecasts;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/forecasts")]
public sealed class ForecastsController(ForecastService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ForecastEntryResponse>>> List(
        Guid householdId, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(householdId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ForecastEntryResponse>> Get(
        Guid householdId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(householdId, id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ForecastEntryResponse>> Create(
        Guid householdId, SaveForecastEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(householdId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { householdId, id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ForecastEntryResponse>> Update(
        Guid householdId, Guid id, SaveForecastEntryRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(householdId, id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(householdId, id, cancellationToken);
        return NoContent();
    }
}
