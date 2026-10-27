using KiaraFin.Api.Common;
using KiaraFin.Api.Features.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Transactions;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/transactions")]
public sealed class TransactionsController(TransactionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> List(
        Guid householdId, int page = 1, int pageSize = Pagination.DefaultPageSize,
        DateOnly? from = null, DateOnly? to = null, Guid? accountId = null,
        Guid? categoryId = null, Guid? responsibleUserId = null,
        FinancialTransactionStatus? status = null, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(householdId, page, pageSize, from, to, accountId,
            categoryId, responsibleUserId, status, cancellationToken));

    [HttpGet("{transactionId:guid}")]
    public async Task<ActionResult<TransactionResponse>> Get(
        Guid householdId, Guid transactionId, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(householdId, transactionId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(
        Guid householdId, CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = await service.CreateAsync(householdId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { householdId, transactionId = transaction.Id }, transaction);
    }

    [HttpPut("{transactionId:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(
        Guid householdId, Guid transactionId, UpdateTransactionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(householdId, transactionId, request, cancellationToken));

    [HttpPost("{transactionId:guid}/settle")]
    public async Task<ActionResult<TransactionResponse>> Settle(
        Guid householdId, Guid transactionId, SettleTransactionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SettleAsync(householdId, transactionId, request, cancellationToken));

    [HttpPatch("{transactionId:guid}/metadata")]
    public async Task<ActionResult<TransactionResponse>> UpdateMetadata(
        Guid householdId, Guid transactionId, UpdateTransactionMetadataRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateMetadataAsync(householdId, transactionId, request, cancellationToken));

    [HttpPost("{transactionId:guid}/cancel")]
    public async Task<ActionResult<TransactionResponse>> Cancel(
        Guid householdId, Guid transactionId, CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(householdId, transactionId, cancellationToken));

    [HttpPost("transfers")]
    public async Task<ActionResult<TransactionResponse>> Transfer(
        Guid householdId, CreateTransferRequest request, CancellationToken cancellationToken) =>
        Ok(await service.TransferAsync(householdId, request, cancellationToken));
}
