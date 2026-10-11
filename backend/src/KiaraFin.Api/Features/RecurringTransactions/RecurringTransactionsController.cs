using KiaraFin.Api.Features.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.RecurringTransactions;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/recurring-transactions")]
public sealed class RecurringTransactionsController(RecurringTransactionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringTransactionResponse>>> List(Guid householdId, CancellationToken ct) =>
        Ok(await service.ListAsync(householdId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecurringTransactionResponse>> Get(Guid householdId, Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(householdId, id, ct));

    [HttpPost]
    public async Task<ActionResult<RecurringTransactionResponse>> Create(
        Guid householdId, CreateRecurringTransactionRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(householdId, request, ct);
        return CreatedAtAction(nameof(Get), new { householdId, id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecurringTransactionResponse>> Update(
        Guid householdId, Guid id, UpdateRecurringTransactionRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(householdId, id, request, ct));

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<RecurringTransactionResponse>> Pause(Guid householdId, Guid id, CancellationToken ct) =>
        Ok(await service.PauseAsync(householdId, id, ct));

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<RecurringTransactionResponse>> Resume(Guid householdId, Guid id, CancellationToken ct) =>
        Ok(await service.ResumeAsync(householdId, id, ct));

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<RecurringTransactionResponse>> End(
        Guid householdId, Guid id, EndRecurringTransactionRequest request, CancellationToken ct) =>
        Ok(await service.EndAsync(householdId, id, request.EndDate, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid householdId, Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(householdId, id, ct);
        return NoContent();
    }

    [HttpPost("generate/{year:int}/{month:int}")]
    public async Task<ActionResult<RecurrenceGenerationResponse>> Generate(
        Guid householdId, int year, int month, CancellationToken ct) =>
        Ok(await service.GenerateMonthAsync(householdId, year, month, ct));
}
