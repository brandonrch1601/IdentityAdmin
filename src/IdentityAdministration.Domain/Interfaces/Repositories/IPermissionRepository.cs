using IdentityAdministration.Domain.Entities;

namespace IdentityAdministration.Domain.Interfaces.Repositories;

/// <summary>
/// Puerto de repositorio para los permisos globales del sistema.
/// </summary>
public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que todos los IDs de permiso existan en la tabla Permissions.
    /// </summary>
    Task<bool> AllExistAsync(IEnumerable<int> permissionIds, CancellationToken cancellationToken = default);
}
