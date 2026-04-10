using IdentityAdministration.Domain.Enums;

namespace IdentityAdministration.Domain.Entities;

/// <summary>
/// Configuración de proveedor de identidad OIDC por tenant (Tenant_Auth_Configs).
/// Soporta Microsoft Entra ID y Google Workspace.
/// </summary>
public sealed class TenantAuthConfig
{
    private TenantAuthConfig() { } // EF Core

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>MICROSOFT o GOOGLE.</summary>
    public ProviderType ProviderType { get; private set; }

    /// <summary>ClientId registrado en el proveedor de identidad.</summary>
    public string ClientId { get; private set; } = default!;

    /// <summary>
    /// URL del issuer OIDC.
    /// Google: https://accounts.google.com
    /// Microsoft: https://login.microsoftonline.com/{tenant_id}/v2.0
    /// </summary>
    public string? IssuerUrl { get; private set; }

    /// <summary>Indica si esta configuración está activa para autenticación.</summary>
    public bool IsActive { get; private set; }

    // ── Navegación ──────────────────────────────────────────────────────────
    public Tenant? Tenant { get; private set; }
}
