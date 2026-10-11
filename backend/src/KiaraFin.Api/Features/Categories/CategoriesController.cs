using KiaraFin.Api.Common;
using KiaraFin.Api.Features.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiaraFin.Api.Features.Categories;

[ApiController, Authorize]
[Route("api/households/{householdId:guid}/categories")]
public sealed class CategoriesController(CategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> List(
        Guid householdId, int page = 1, int pageSize = Pagination.DefaultPageSize,
        CategoryType? type = null, bool? active = null, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(householdId, page, pageSize, type, active, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        Guid householdId, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await service.CreateAsync(householdId, request, cancellationToken);
        return Created($"/api/households/{householdId}/categories/{category.Id}", category);
    }

    [HttpPut("{categoryId:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid householdId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(householdId, categoryId, request, cancellationToken));

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid householdId, Guid categoryId, CancellationToken cancellationToken)
    {
        await service.SetActiveAsync(householdId, categoryId, false, cancellationToken);
        return NoContent();
    }

    [HttpPost("{categoryId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid householdId, Guid categoryId, CancellationToken cancellationToken)
    {
        await service.SetActiveAsync(householdId, categoryId, true, cancellationToken);
        return NoContent();
    }
}
