using System.Text.Json;

namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Raíz de agregado que representa una organización cliente del SaaS (Tenants).
/// </summary>
public sealed class Tenant
{
    private readonly List<TenantAuthConfig> _authConfigs = [];
    private readonly List<User> _users = [];
    private readonly List<Role> _roles = [];

    private Tenant() { } // EF Core

    public Guid Id { get; private set; }

    /// <summary>Razón social o nombre comercial del tenant.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Número de identificación fiscal (cédula jurídica, RUC, etc.).</summary>
    public string IdentificationNumber { get; private set; } = default!;

    /// <summary>
    /// Dominio de correo corporativo. Usado en Home Realm Discovery.
    /// Ejemplo: "banco.cr"
    /// </summary>
    public string DomainName { get; private set; } = default!;

    public int? StatusId { get; private set; }

    /// <summary>Configuración de marca (colores, logo) almacenada como JSONB.</summary>
    public JsonDocument? BrandingConfig { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // ── Navegación ──────────────────────────────────────────────────────────
    public CatStatus? Status { get; private set; }
    public IReadOnlyCollection<TenantAuthConfig> AuthConfigs => _authConfigs;
    public IReadOnlyCollection<User> Users => _users;
    public IReadOnlyCollection<Role> Roles => _roles;
}
