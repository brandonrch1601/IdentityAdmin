using IdentityAdministration.Domain.Common;
using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Domain.Interfaces.Repositories;

/// <summary>
/// Puerto de repositorio para la entidad User.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Obtiene un usuario con su Status cargado.
    /// Aplica Global Query Filter de tenant.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca por ExternalId (OID/Sub). Usa IgnoreQueryFilters para el flujo de login
    /// donde el TenantContext aún no está inicializado.
    /// </summary>
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    /// <summary>Lista paginada con filtros opcionales por status y email.</summary>
    Task<PagedResult<User>> GetPagedAsync(
        Guid tenantId,
        string? statusCode,
        string? email,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna la lista plana de códigos de permisos del usuario,
    /// resolviendo User → UserRoles → Roles → RolePermissions → Permissions.
    /// </summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza todos los roles asignados al usuario.</summary>
    Task ReplaceRolesAsync(Guid userId, IEnumerable<int> roleIds, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
