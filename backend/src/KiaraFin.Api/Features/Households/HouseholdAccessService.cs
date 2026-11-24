using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Households;

public sealed class HouseholdAccessService(AppDbContext dbContext, ICurrentUser currentUser)
{
    public Guid UserId => currentUser.UserId;

    public async Task<HouseholdMembership> RequireMembershipAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        return await dbContext.HouseholdMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.HouseholdId == householdId && x.UserId == currentUser.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Núcleo financeiro não encontrado.");
    }
}
