using KiaraFin.Api.Features.Installments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.CreditCards;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/credit-cards")]
public sealed class CreditCardsController(CreditCardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreditCardResponse>>> List(Guid householdId, CancellationToken ct) =>
        Ok(await service.ListAsync(householdId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreditCardResponse>> Get(Guid householdId, Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(householdId, id, ct));

    [HttpPost]
    public async Task<ActionResult<CreditCardResponse>> Create(
        Guid householdId, CreateCreditCardRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(householdId, request, ct);
        return CreatedAtAction(nameof(Get), new { householdId, id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreditCardResponse>> Update(
        Guid householdId, Guid id, UpdateCreditCardRequest request, CancellationToken ct) =>
        Ok(await service.UpdateAsync(householdId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid householdId, Guid id, CancellationToken ct)
    {
        await service.DeactivateAsync(householdId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/purchases")]
    public async Task<ActionResult<InstallmentPlanResponse>> Purchase(
        Guid householdId, Guid id, CreditCardPurchaseRequest request, CancellationToken ct) =>
        Ok(await service.CreatePurchaseAsync(householdId, id, request, ct));

    [HttpGet("{id:guid}/invoices")]
    public async Task<ActionResult<IReadOnlyList<InvoiceSummaryResponse>>> Invoices(
        Guid householdId, Guid id, CancellationToken ct) =>
        Ok(await service.ListInvoicesAsync(householdId, id, ct));

    [HttpGet("{id:guid}/invoices/{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceDetailResponse>> Invoice(
        Guid householdId, Guid id, Guid invoiceId, CancellationToken ct) =>
        Ok(await service.GetInvoiceAsync(householdId, id, invoiceId, ct));

    [HttpPost("{id:guid}/invoices/{invoiceId:guid}/pay")]
    public async Task<ActionResult<InvoicePaymentResponse>> PayInvoice(
        Guid householdId, Guid id, Guid invoiceId, PayInvoiceRequest request, CancellationToken ct) =>
        Ok(await service.PayInvoiceAsync(householdId, id, invoiceId, request, ct));
}
