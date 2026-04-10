namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Permiso granular de la plataforma (Permissions).
/// Es global: no tiene alcance por tenant.
/// Ejemplos: DOC_VIEW, DOC_UPLOAD, EXP_APPROVE, USER_ADMIN.
/// </summary>
public sealed class Permission
{
    private Permission() { } // EF Core

    public int Id { get; private set; }

    /// <summary>Código único del permiso. Ej: 'DOC_VIEW'.</summary>
    public string Code { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    // ── Navegación ──────────────────────────────────────────────────────────
    // Lista mutable interna — EF Core necesita ICollection<T> para Add() al materializar.
    // Se expone como IReadOnlyCollection<T> para evitar mutación externa.
    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;
}
