using KiaraFin.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Reports;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/reports")]
public sealed class ReportsController(ReportService service) : ControllerBase
{
    [HttpGet("monthly")]
    public async Task<ActionResult<PagedResponse<MonthlyReportItem>>> Monthly(
        Guid householdId, [FromQuery] ReportFilter filter, CancellationToken cancellationToken) =>
        Ok(await service.MonthlyAsync(householdId, filter, cancellationToken));

    [HttpGet("by-category")]
    public async Task<ActionResult<PagedResponse<CategoryReportItem>>> ByCategory(
        Guid householdId, [FromQuery] ReportFilter filter, CancellationToken cancellationToken) =>
        Ok(await service.ByCategoryAsync(householdId, filter, cancellationToken));

    [HttpGet("by-responsible")]
    public async Task<ActionResult<PagedResponse<ResponsibleReportItem>>> ByResponsible(
        Guid householdId, [FromQuery] ReportFilter filter, CancellationToken cancellationToken) =>
        Ok(await service.ByResponsibleAsync(householdId, filter, cancellationToken));

    [HttpGet("shared-vs-individual")]
    public async Task<ActionResult<PagedResponse<ScopeReportItem>>> SharedVsIndividual(
        Guid householdId, [FromQuery] ReportFilter filter, CancellationToken cancellationToken) =>
        Ok(await service.SharedVsIndividualAsync(householdId, filter, cancellationToken));
}
