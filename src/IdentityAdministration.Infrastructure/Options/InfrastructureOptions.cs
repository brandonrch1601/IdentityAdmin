namespace IdentityAdministration.Infrastructure.Options;

/// <summary>
/// Configuración del JWT interno del SaaS.
/// Sección: "Jwt" en appsettings.json.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Issuer del JWT interno. Ej: https://identity.tecasoft.cr</summary>
    public string Issuer { get; init; } = default!;

    /// <summary>Audience del JWT. Ej: -services</summary>
    public string Audience { get; init; } = default!;

    /// <summary>Tiempo de expiración en minutos. Default: 60.</summary>
    public int ExpirationMinutes { get; init; } = 60;
}

/// <summary>
/// Configuración de Azure Key Vault para firma de JWT con RSA.
/// Sección: "AzureKeyVault" en appsettings.json.
/// </summary>
public sealed class AzureKeyVaultOptions
{
    public const string SectionName = "AzureKeyVault";

    /// <summary>URI del vault. Ej: https://-vault.vault.azure.net/</summary>
    public string VaultUri { get; init; } = default!;

    /// <summary>Nombre de la clave RSA en Key Vault para firmar tokens.</summary>
    public string TokenSigningKeyName { get; init; } = "-token-signing";
}
