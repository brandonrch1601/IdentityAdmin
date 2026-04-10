namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Tabla de unión entre Roles y Permissions (Role_Permissions).
/// Composite PK: (role_id, permission_id).
/// </summary>
public sealed class RolePermission
{
    private RolePermission() { } // EF Core

    public static RolePermission Create(int roleId, int permissionId) =>
        new() { RoleId = roleId, PermissionId = permissionId };

    public int RoleId { get; private set; }
    public int PermissionId { get; private set; }

    // ── Navegación ──────────────────────────────────────────────────────────
    public Role? Role { get; private set; }
    public Permission? Permission { get; private set; }
}
