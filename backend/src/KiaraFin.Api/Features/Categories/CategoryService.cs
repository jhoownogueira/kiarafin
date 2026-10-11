using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Categories;

public sealed class CategoryService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<PagedResponse<CategoryResponse>> ListAsync(
        Guid householdId, int page, int pageSize, CategoryType? type, bool? active,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = dbContext.Categories.AsNoTracking().Where(x => x.HouseholdId == householdId);
        if (type is not null)
            query = query.Where(x => x.Type == type);
        if (active is not null)
            query = query.Where(x => x.IsActive == active);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Type).ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Type, x.Color, x.Icon, x.IsSystem, x.IsActive))
            .ToListAsync(cancellationToken);
        return new PagedResponse<CategoryResponse>(items, page, pageSize, total);
    }

    public async Task<CategoryResponse> CreateAsync(
        Guid householdId, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        if (await NameExistsAsync(householdId, request.Type, request.Name, null, cancellationToken))
            throw new ConflictException("Já existe uma categoria com este nome e tipo.");
        var category = new Category(householdId, request.Name, request.Type, request.Color, request.Icon);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid householdId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var category = await FindAsync(householdId, categoryId, cancellationToken);
        if (await NameExistsAsync(householdId, category.Type, request.Name, categoryId, cancellationToken))
            throw new ConflictException("Já existe uma categoria com este nome e tipo.");
        category.Update(request.Name, request.Color, request.Icon, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(category);
    }

    public async Task SetActiveAsync(
        Guid householdId, Guid categoryId, bool active, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var category = await FindAsync(householdId, categoryId, cancellationToken);
        category.SetActive(active, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> NameExistsAsync(
        Guid householdId, CategoryType type, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToUpper();
        return dbContext.Categories.AnyAsync(
            x => x.HouseholdId == householdId && x.Type == type && x.Id != exceptId && x.Name.ToUpper() == normalized,
            cancellationToken);
    }

    private async Task<Category> FindAsync(Guid householdId, Guid categoryId, CancellationToken cancellationToken) =>
        await dbContext.Categories.SingleOrDefaultAsync(
            x => x.Id == categoryId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Categoria não encontrada.");

    private static CategoryResponse Map(Category x) =>
        new(x.Id, x.Name, x.Type, x.Color, x.Icon, x.IsSystem, x.IsActive);
}
