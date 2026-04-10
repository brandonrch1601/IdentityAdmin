using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Domain.Interfaces.Repositories;

/// <summary>
/// Puerto de repositorio para la entidad Tenant.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un tenant por su dominio corporativo.
    /// Incluye TenantAuthConfigs activas para Home Realm Discovery.
    /// </summary>
    Task<Tenant?> GetByDomainNameAsync(string domainName, CancellationToken cancellationToken = default);
}
