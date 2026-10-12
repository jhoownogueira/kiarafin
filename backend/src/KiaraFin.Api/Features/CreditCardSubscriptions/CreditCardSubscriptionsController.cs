using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.CreditCardSubscriptions;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/credit-cards/{cardId:guid}/subscriptions")]
public sealed class CreditCardSubscriptionsController(CreditCardSubscriptionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreditCardSubscriptionResponse>>> List(
        Guid householdId, Guid cardId, CancellationToken ct) =>
        Ok(await service.ListAsync(householdId, cardId, ct));

    [HttpPost]
    public async Task<ActionResult<CreditCardSubscriptionResponse>> Create(
        Guid householdId, Guid cardId, CreateCreditCardSubscriptionRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(householdId, cardId, request, ct);
        return CreatedAtAction(nameof(List), new { householdId, cardId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditCardSubscriptionResponse>> Update(
        Guid householdId, Guid cardId, Guid id,
        UpdateCreditCardSubscriptionRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(householdId, cardId, id, request, ct));

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<CreditCardSubscriptionResponse>> Pause(
        Guid householdId, Guid cardId, Guid id, CancellationToken ct) =>
        Ok(await service.PauseAsync(householdId, cardId, id, ct));

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<CreditCardSubscriptionResponse>> Resume(
        Guid householdId, Guid cardId, Guid id, CancellationToken ct) =>
        Ok(await service.ResumeAsync(householdId, cardId, id, ct));

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<CreditCardSubscriptionResponse>> End(
        Guid householdId, Guid cardId, Guid id,
        EndCreditCardSubscriptionRequest request, CancellationToken ct) =>
        Ok(await service.EndAsync(householdId, cardId, id, request.EndDate, ct));

    [HttpPost("generate/{year:int}/{month:int}")]
    public async Task<ActionResult<CreditCardSubscriptionGenerationResponse>> Generate(
        Guid householdId, Guid cardId, int year, int month, CancellationToken ct) =>
        Ok(await service.GenerateMonthAsync(householdId, cardId, year, month, ct));
}
