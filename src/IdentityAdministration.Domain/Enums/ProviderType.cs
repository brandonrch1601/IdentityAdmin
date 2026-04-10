namespace IdentityAdministration.Domain.Enums;

/// <summary>
/// Proveedor de identidad externo soportado.
/// Se almacena como VARCHAR(20) en la tabla Tenant_Auth_Configs.
/// </summary>
public enum ProviderType
{
    /// <summary>Microsoft Entra ID (Azure Active Directory).</summary>
    Microsoft = 1,

    /// <summary>Google Identity (Google Workspace).</summary>
    Google = 2
}
