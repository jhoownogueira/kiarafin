using KiaraFin.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Accounts;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/accounts")]
public sealed class AccountsController(AccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AccountResponse>>> List(
        Guid householdId, int page = 1, int pageSize = Pagination.DefaultPageSize,
        bool? active = null, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(householdId, page, pageSize, active, cancellationToken));

    [HttpGet("{accountId:guid}")]
    public async Task<ActionResult<AccountResponse>> Get(
        Guid householdId, Guid accountId, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(householdId, accountId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(
        Guid householdId, CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await service.CreateAsync(householdId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { householdId, accountId = account.Id }, account);
    }

    [HttpPut("{accountId:guid}")]
    public async Task<ActionResult<AccountResponse>> Update(
        Guid householdId, Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(householdId, accountId, request, cancellationToken));

    [HttpDelete("{accountId:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        await service.SetActiveAsync(householdId, accountId, false, cancellationToken);
        return NoContent();
    }

    [HttpPost("{accountId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        await service.SetActiveAsync(householdId, accountId, true, cancellationToken);
        return NoContent();
    }

    [HttpGet("{accountId:guid}/history")]
    public async Task<ActionResult<PagedResponse<AccountMovementResponse>>> History(
        Guid householdId, Guid accountId, int page = 1,
        int pageSize = Pagination.DefaultPageSize, CancellationToken cancellationToken = default) =>
        Ok(await service.HistoryAsync(householdId, accountId, page, pageSize, cancellationToken));
}
