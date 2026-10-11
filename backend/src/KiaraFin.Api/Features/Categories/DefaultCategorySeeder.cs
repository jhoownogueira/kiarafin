using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Categories;

public sealed class DefaultCategorySeeder(AppDbContext dbContext)
{
    private static readonly (string Name, CategoryType Type, string Color, string Icon)[] Defaults =
    [
        ("Salário", CategoryType.Income, "#22A06B", "briefcase-business"),
        ("Rendimentos", CategoryType.Income, "#14B8A6", "trending-up"),
        ("Reembolsos", CategoryType.Income, "#2D8CFF", "rotate-ccw"),
        ("Outras receitas", CategoryType.Income, "#64748B", "circle-plus"),
        ("Alimentação", CategoryType.Expense, "#F59E0B", "utensils"),
        ("Mercado", CategoryType.Expense, "#84CC16", "shopping-basket"),
        ("Moradia", CategoryType.Expense, "#8B5CF6", "house"),
        ("Transporte", CategoryType.Expense, "#3B82F6", "car"),
        ("Saúde", CategoryType.Expense, "#EF4444", "heart-pulse"),
        ("Lazer", CategoryType.Expense, "#EC4899", "party-popper"),
        ("Pets", CategoryType.Expense, "#F97316", "paw-print"),
        ("Compras", CategoryType.Expense, "#6366F1", "shopping-bag"),
        ("Outras despesas", CategoryType.Expense, "#64748B", "ellipsis")
    ];

    public async Task EnsureSeededAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Categories
            .Where(x => x.HouseholdId == householdId)
            .Select(x => new { x.Name, x.Type })
            .ToListAsync(cancellationToken);
        var local = dbContext.Categories.Local.Where(x => x.HouseholdId == householdId)
            .Select(x => (x.Name, x.Type));
        var keys = existing.Select(x => (x.Name, x.Type)).Concat(local).ToHashSet();
        foreach (var item in Defaults.Where(x => !keys.Contains((x.Name, x.Type))))
            dbContext.Categories.Add(new Category(householdId, item.Name, item.Type, item.Color, item.Icon, true));
    }
}
