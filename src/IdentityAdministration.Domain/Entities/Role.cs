namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Rol de seguridad con alcance por tenant (Roles).
/// Define un conjunto de permisos asignables a usuarios.
/// </summary>
public sealed class Role
{
    private readonly List<RolePermission> _rolePermissions = [];
    private readonly List<UserRole> _userRoles = [];

    private Role() { } // EF Core

    /// <summary>Crea un nuevo rol para el tenant indicado.</summary>
    public static Role Create(Guid tenantId, string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Role
        {
            TenantId = tenantId,
            Name = name,
            Description = description
        };
    }

    public int Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    // ── Navegación ──────────────────────────────────────────────────────────
    public Tenant? Tenant { get; private set; }
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;
}
