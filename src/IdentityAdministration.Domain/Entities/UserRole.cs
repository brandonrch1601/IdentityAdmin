namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Tabla de unión entre Users y Roles (User_Roles).
/// Composite PK: (user_id, role_id).
/// </summary>
public sealed class UserRole
{
    private UserRole() { } // EF Core

    public static UserRole Create(Guid userId, int roleId) =>
        new() { UserId = userId, RoleId = roleId };

    public Guid UserId { get; private set; }
    public int RoleId { get; private set; }

    // ── Navegación ──────────────────────────────────────────────────────────
    public User? User { get; private set; }
    public Role? Role { get; private set; }
}
