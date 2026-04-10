using Microsoft.EntityFrameworkCore;
using IdentityAdministration.Domain.Entities;
using IdentityAdministration.Domain.Interfaces.Repositories;
using IdentityAdministration.Infrastructure.Persistence;

namespace IdentityAdministration.Infrastructure.Repositories;

internal sealed class TenantRepository(IdentityDbContext context) : ITenantRepository
{
    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Tenants
            .Include(t => t.Status)
            .Include(t => t.AuthConfigs)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Tenant?> GetByDomainNameAsync(
        string domainName,
        CancellationToken cancellationToken = default) =>
        await context.Tenants
            .Include(t => t.Status)
            .Include(t => t.AuthConfigs)
            .IgnoreQueryFilters() // Los tenants son lookups globales (sin filtro de tenant propio)
            .FirstOrDefaultAsync(
                t => t.DomainName == domainName.ToLowerInvariant(),
                cancellationToken);
}
