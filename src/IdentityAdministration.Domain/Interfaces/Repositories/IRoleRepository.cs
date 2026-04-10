using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Domain.Interfaces.Repositories;

/// <summary>
/// Puerto de repositorio para la entidad Role.
/// </summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Todos los roles del tenant con sus RolePermissions incluidas.</summary>
    Task<IReadOnlyList<Role>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que todos los IDs existan y pertenezcan al tenant indicado.
    /// Usado para validar asignaciones de roles en CreateUser y AssignRoles.
    /// </summary>
    Task<bool> AllBelongToTenantAsync(Guid tenantId, IEnumerable<int> roleIds, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);

    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza los permisos de un rol (borra los existentes e inserta los nuevos).</summary>
    Task ReplacePermissionsAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
