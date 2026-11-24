using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Cards;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/cards")]
public sealed class CardsController(CardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CardSummaryResponse>>> List(
        Guid householdId, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(householdId, cancellationToken));

    [HttpGet("{cardId:guid}")]
    public async Task<ActionResult<CardDetailResponse>> Get(
        Guid householdId, Guid cardId, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(householdId, cardId, cancellationToken));
}
