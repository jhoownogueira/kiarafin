using System.ComponentModel.DataAnnotations;
using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Categories;

public sealed record CreateCategoryRequest(
    [Required, StringLength(80, MinimumLength = 2)] string Name,
    CategoryType Type,
    [Required, StringLength(30)] string Color,
    [Required, StringLength(60)] string Icon);

public sealed record UpdateCategoryRequest(
    [Required, StringLength(80, MinimumLength = 2)] string Name,
    [Required, StringLength(30)] string Color,
    [Required, StringLength(60)] string Icon);

public sealed record CategoryResponse(
    Guid Id, string Name, CategoryType Type, string Color, string Icon, bool IsSystem, bool IsActive);
